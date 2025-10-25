using Microsoft.AspNetCore.Http; // Required for IResult and HttpContext
// REMOVED: using Microsoft.AspNetCore.Mvc; // Not needed for IResult
using Microsoft.Data.SqlClient;
using RoMars.Test.StreamingDataAPI;
using System.Diagnostics;
using System.Text.Json;

// --- 1. Custom Streaming Result (Zero-Allocation Core with DBNull Handling) ---

/// <summary>
/// A custom IResult that performs zero-allocation JSON serialization.
/// It uses Utf8JsonWriter to write raw DbDataReader values directly to the response stream.
/// </summary>
// Change: Implement IResult instead of IActionResult
public class StreamingJsonResult : IResult
{
    private readonly ProductRepository _repository;
    private readonly ILogger _logger;

    public StreamingJsonResult(ProductRepository repository, ILogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// The required method for the IResult interface.
    /// It takes the raw HttpContext and is responsible for writing the response.
    /// </summary>
    // Change: Method signature for IResult
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        // Change: Use the parameter httpContext directly
        var response = httpContext.Response;
        response.ContentType = "application/json; charset=utf-8";

        SqlConnection? connection = null;
        SqlDataReader? reader = null;

        var streamTimer = Stopwatch.StartNew();
        int rowCount = 0;
        // Change: Get connection ID from httpContext
        var connectionId = httpContext.Connection.Id;

        // Change: Use httpContext.RequestAborted for the CancellationToken
        _logger.LogTrace("Connection ID {ConnectionId}: Starting ExecuteAsync. Thread: {ThreadId}", connectionId, Environment.CurrentManagedThreadId);

        try
        {
            // Get the raw reader and connection (resilience retry is handled in the repository)
            (connection, reader) = await _repository.ExecuteStreamingQueryAsync(50, httpContext.RequestAborted);

            // Use Utf8JsonWriter for maximum serialization performance
            await using (var writer = new Utf8JsonWriter(response.BodyWriter.AsStream(),
                new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartArray();

                // Read column ordinals once (performance optimization)
                int idOrdinal = reader.GetOrdinal("Id");
                int nameOrdinal = reader.GetOrdinal("Name");
                int priceOrdinal = reader.GetOrdinal("Price");
                _logger.LogDebug("Connection ID {ConnectionId}: Column ordinals retrieved.", connectionId);

                // Change: Use httpContext.RequestAborted for the CancellationToken
                while (await reader.ReadAsync(httpContext.RequestAborted))
                {
                    writer.WriteStartObject();

                    // ROBUSTNESS: Always check for DBNull before reading the data type directly

                    // Id (using 0 as default if null)
                    writer.WriteNumber("id", reader.IsDBNull(idOrdinal) ? 0 : reader.GetInt32(idOrdinal));

                    // Name (writing JSON null if DBNull)
                    writer.WritePropertyName("name");
                    if (reader.IsDBNull(nameOrdinal))
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        writer.WriteStringValue(reader.GetString(nameOrdinal));
                    }

                    // Price (writing JSON null if DBNull)
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

                    if (rowCount % 10000 == 0)
                    {
                        _logger.LogTrace("Connection ID {ConnectionId}: Row {RowCount} streamed. Current duration: {ElapsedMs}ms.",
                            connectionId, rowCount, streamTimer.Elapsed.TotalMilliseconds);
                    }
                }

                writer.WriteEndArray();
                await writer.FlushAsync();
            }
        }
        // Change: Use httpContext.RequestAborted for cancellation check
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning("Connection ID {ConnectionId}: Streaming canceled by client disconnect after {RowCount} rows.",
                connectionId, rowCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection ID {ConnectionId}: CRITICAL ERROR during streaming after {RowCount} rows.",
                connectionId, rowCount);
            throw;
        }
        finally
        {
            // CRITICAL: Ensure resources are released back to the pool immediately.
            reader?.Dispose();
            connection?.Dispose();
            streamTimer.Stop();

            _logger.LogInformation("Connection ID {ConnectionId}: STREAM COMPLETE. Total duration: {TotalMs}ms. Rows sent: {RowCount}",
                connectionId, streamTimer.Elapsed.TotalMilliseconds, rowCount);
        }
    }
}