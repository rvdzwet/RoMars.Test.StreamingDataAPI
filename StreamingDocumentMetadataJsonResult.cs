using Microsoft.AspNetCore.Http; // Required for IResult and HttpContext
using Microsoft.Data.SqlClient;
using RoMars.Test.StreamingDataAPI;
using System.Data.Common; // For DbConnection
using System.Diagnostics;
using System.Text.Json;

namespace RoMars.Test.StreamingDataAPI
{
    /// <summary>
    /// A custom IResult that performs zero-allocation JSON serialization directly to the HTTP response stream.
    /// It uses Utf8JsonWriter to write raw DbDataReader values, achieving extreme performance and low memory footprint.
    /// This adheres to the Single Responsibility Principle by focusing solely on serializing and streaming product data.
    /// </summary>
    public class StreamingJsonResult : IResult
    {
        private readonly ProductRepository _repository;
        private readonly ILogger<StreamingJsonResult> _logger;

        public StreamingJsonResult(ProductRepository repository, ILogger<StreamingJsonResult> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// Executes the custom result, taking control of the HTTP response.
        /// Performance Comment: This method is the core of the high-performance streaming.
        /// Key optimizations include:
        /// 1. `IResult` Implementation: Bypasses MVC framework overhead, allowing direct
        ///    control over the HTTP response stream for maximum efficiency.
        /// 2. `Utf8JsonWriter`: This is a low-level, high-performance JSON writer that
        ///    writes directly to `response.BodyWriter.AsStream()`. It avoids intermediate
        ///    string allocations and object serialization, operating directly on UTF-8 bytes.
        ///    This is significantly faster and uses less memory than `JsonConvert.SerializeObject`
        ///    or higher-level JSON serializers for large data streams.
        /// 3. Direct `DbDataReader` to `Utf8JsonWriter` Streaming: Data is read directly
        ///    from the database reader and written to the JSON writer without creating
        ///    an in-memory collection of objects (e.g., `List<Product>`). This is a
        ///    zero-allocation approach for the data payload itself.
        /// 4. `CommandBehavior.SequentialAccess` (configured in ProductRepository): Ensures
        ///    the `SqlDataReader` reads data in a forward-only, sequential manner,
        ///    optimizing database memory usage and fetch times.
        /// 5. Column Ordinal Lookup: `reader.GetOrdinal()` is called once outside the loop
        ///    to get column positions by name. This avoids repeated string comparisons
        ///    inside the tight streaming loop, which would incur significant overhead.
        /// 6. `response.BodyWriter.AsStream()`: Allows `Utf8JsonWriter` to write directly
        ///    to the underlying ASP.NET Core response stream, utilizing `PipeWriter` for
        ///    efficient buffered I/O without extra copying.
        /// 7. `httpContext.RequestAborted`: Seamlessly integrates with client-side cancellation.
        ///    If the client disconnects, `CancellationToken` is signaled, allowing the server
        ///    to stop streaming gracefully and release resources, preventing wasted work.
        /// 8. `finally` block for resource disposal: Guarantees that the `SqlDataReader` and
        ///    `DbConnection` are always disposed and returned to their respective pools,
        ///    even if errors occur or the client disconnects. This is critical for preventing
        ///    resource exhaustion and maintaining connection pool health under high load.
        /// </summary>
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var response = httpContext.Response;
            response.ContentType = "application/json; charset=utf-8";

            DbConnection? connection = null;
            SqlDataReader? reader = null;

            var streamTimer = Stopwatch.StartNew();
            int rowCount = 0;
            var connectionId = httpContext.Connection.Id;

            _logger.LogTrace("Connection ID {ConnectionId}: Starting streaming execution. Thread ID: {ThreadId}", connectionId, Environment.CurrentManagedThreadId);

            try
            {
                (connection, reader) = await _repository.ExecuteStreamingQueryAsync(50, httpContext.RequestAborted);

                await using (var writer = new Utf8JsonWriter(response.BodyWriter.AsStream(),
                    new JsonWriterOptions { Indented = false }))
                {
                    writer.WriteStartArray();

                    // Performance Comment: Reading column ordinals once outside the loop
                    // avoids repeated string lookups, which can be expensive in a hot path.
                    int idOrdinal = reader.GetOrdinal("Id");
                    int nameOrdinal = reader.GetOrdinal("Name");
                    int priceOrdinal = reader.GetOrdinal("Price");
                    _logger.LogDebug("Connection ID {ConnectionId}: Column ordinals retrieved. Thread ID: {ThreadId}", connectionId, Environment.CurrentManagedThreadId);

                    while (await reader.ReadAsync(httpContext.RequestAborted))
                    {
                        writer.WriteStartObject();

                        // Robustness: Always check for DBNull before reading.
                        // Performance Comment: Direct calls to GetInt64, GetString, GetDecimal
                        // are highly optimized for direct data retrieval from the reader.
                        // Avoiding boxing/unboxing with object GetValue() calls where possible.
                        writer.WriteNumber("id", reader.IsDBNull(idOrdinal) ? 0 : reader.GetInt64(idOrdinal)); // Use GetInt64 for BIGINT

                        writer.WritePropertyName("name");
                        if (reader.IsDBNull(nameOrdinal))
                        {
                            writer.WriteNullValue();
                        }
                        else
                        {
                            writer.WriteStringValue(reader.GetString(nameOrdinal));
                        }

                        writer.WritePropertyName("price");
                        if (reader.IsDBNull(priceOrdinal))
                        {
                            writer.WriteNullValue();
                        }
                        else
                        {
                            writer.WriteNumberValue(reader.GetDecimal(priceOrdinal));
                        }

                        writer.WriteEndObject();

                        rowCount++;

                        if (rowCount % 10000 == 0) // Log progress without excessive overhead
                        {
                            _logger.LogTrace("Connection ID {ConnectionId}: Row {RowCount:N0} streamed. Current duration: {ElapsedMs}ms. Thread ID: {ThreadId}",
                                connectionId, rowCount, streamTimer.Elapsed.TotalMilliseconds, Environment.CurrentManagedThreadId);
                        }
                    }

                    writer.WriteEndArray();
                    await writer.FlushAsync(httpContext.RequestAborted); // Flush with cancellation token
                }
            }
            catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogWarning("Connection ID {ConnectionId}: Streaming canceled by client disconnect or server shutdown after {RowCount:N0} rows. Thread ID: {ThreadId}",
                    connectionId, rowCount, Environment.CurrentManagedThreadId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection ID {ConnectionId}: CRITICAL ERROR during streaming after {RowCount:N0} rows. Thread ID: {ThreadId}",
                    connectionId, rowCount, Environment.CurrentManagedThreadId);
                throw;
            }
            finally
            {
                reader?.Dispose(); // Ensure reader is closed and resources released
                connection?.Dispose(); // Ensure connection is returned to the pool

                streamTimer.Stop();
                _logger.LogInformation("Connection ID {ConnectionId}: STREAM COMPLETE. Total duration: {TotalMs}ms. Rows sent: {RowCount:N0}. Thread ID: {ThreadId}",
                    connectionId, streamTimer.Elapsed.TotalMilliseconds, rowCount, Environment.CurrentManagedThreadId);
            }
        }
    }
}
