using Microsoft.AspNetCore.Http;
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RoMars.StreamingJsonOutput.Framework
{
    /// <summary>
    /// A custom IResult that performs zero-allocation JSON serialization directly to the HTTP response stream.
    /// It uses Utf8JsonWriter to write raw DbDataReader values, achieving extreme performance and low memory footprint.
    /// This generic implementation allows streaming data from any DbDataReader.
    /// </summary>
    public class StreamingDbDataReaderJsonResult : IResult
    {
        private readonly DbConnection _connection;
        private readonly DbDataReader _reader;
        private readonly ILogger<StreamingDbDataReaderJsonResult> _logger;

        // Optimized type-specific writer actions
        private static readonly Dictionary<Type, Action<Utf8JsonWriter, DbDataReader, int>> TypeWriters =
            new Dictionary<Type, Action<Utf8JsonWriter, DbDataReader, int>>
            {
                { typeof(bool), (writer, reader, i) => writer.WriteBooleanValue(reader.GetBoolean(i)) },
                { typeof(byte), (writer, reader, i) => writer.WriteNumberValue(reader.GetByte(i)) },
                { typeof(char), (writer, reader, i) => writer.WriteStringValue(reader.GetChar(i).ToString()) },
                { typeof(DateTime), (writer, reader, i) => writer.WriteStringValue(reader.GetDateTime(i)) },
                { typeof(decimal), (writer, reader, i) => writer.WriteNumberValue(reader.GetDecimal(i)) },
                { typeof(double), (writer, reader, i) => writer.WriteNumberValue(reader.GetDouble(i)) },
                { typeof(float), (writer, reader, i) => writer.WriteNumberValue(reader.GetFloat(i)) },
                { typeof(Guid), (writer, reader, i) => writer.WriteStringValue(reader.GetGuid(i)) },
                { typeof(short), (writer, reader, i) => writer.WriteNumberValue(reader.GetInt16(i)) },
                { typeof(int), (writer, reader, i) => writer.WriteNumberValue(reader.GetInt32(i)) },
                { typeof(long), (writer, reader, i) => writer.WriteNumberValue(reader.GetInt64(i)) },
                { typeof(string), (writer, reader, i) => writer.WriteStringValue(reader.GetString(i)) }
            };

        public StreamingDbDataReaderJsonResult(DbConnection connection, DbDataReader reader, ILogger<StreamingDbDataReaderJsonResult> logger)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the custom result, taking control of the HTTP response.
        /// </summary>
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var response = httpContext.Response;
            response.ContentType = "application/json; charset=utf-8";

            var streamTimer = Stopwatch.StartNew();
            int rowCount = 0;
            var connectionId = httpContext.Connection.Id;

            _logger.LogTrace("Connection ID {ConnectionId}: Starting streaming DbDataReader execution. Thread ID: {ThreadId}", connectionId, Environment.CurrentManagedThreadId);

            try
            {
                await using (var writer = new Utf8JsonWriter(response.BodyWriter.AsStream(),
                    new JsonWriterOptions { Indented = false }))
                {
                    writer.WriteStartArray();

                    var columnCount = _reader.FieldCount;
                    var columnNames = new string[columnCount];
                    var columnTypeWriters = new Action<Utf8JsonWriter, DbDataReader, int>[columnCount];

                    for (int i = 0; i < columnCount; i++)
                    {
                        columnNames[i] = _reader.GetName(i);
                        var clrType = _reader.GetFieldType(i);

                        if (TypeWriters.TryGetValue(clrType, out var typeWriter))
                        {
                            columnTypeWriters[i] = typeWriter;
                        }
                        else
                        {
                            // Fallback for unhandled types. This will incur boxing and string allocation.
                            _logger.LogWarning("Connection ID {ConnectionId}: Unhandled CLR type '{ClrType}' for column '{ColumnName}'. Falling back to GetValue().ToString().", connectionId, clrType.Name, columnNames[i]);
                            columnTypeWriters[i] = (w, r, idx) => w.WriteStringValue(r.GetValue(idx)?.ToString());
                        }
                    }
                    _logger.LogDebug("Connection ID {ConnectionId}: {ColumnCount} column ordinals and type writers retrieved dynamically. Thread ID: {ThreadId}", connectionId, columnCount, Environment.CurrentManagedThreadId);

                    while (await _reader.ReadAsync(httpContext.RequestAborted))
                    {
                        writer.WriteStartObject();

                        for (int i = 0; i < columnCount; i++)
                        {
                            writer.WritePropertyName(columnNames[i]);

                            if (_reader.IsDBNull(i))
                            {
                                writer.WriteNullValue();
                            }
                            else
                            {
                                columnTypeWriters[i](writer, _reader, i);
                            }
                        }
                        writer.WriteEndObject();
                        rowCount++;

                        if (rowCount % 10000 == 0)
                        {
                            _logger.LogTrace("Connection ID {ConnectionId}: Row {RowCount:N0} streamed. Current duration: {ElapsedMs}ms. Thread ID: {ThreadId}",
                                connectionId, rowCount, streamTimer.Elapsed.TotalMilliseconds, Environment.CurrentManagedThreadId);
                        }
                    }
                    writer.WriteEndArray();
                    await writer.FlushAsync(httpContext.RequestAborted);
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
                _reader?.Dispose();
                _connection?.Dispose();
                streamTimer.Stop();
                _logger.LogInformation("Connection ID {ConnectionId}: STREAM COMPLETE. Total duration: {TotalMs}ms. Rows sent: {RowCount:N0}. Thread ID: {ThreadId}",
                    connectionId, streamTimer.Elapsed.TotalMilliseconds, rowCount, Environment.CurrentManagedThreadId);
            }
        }
    }
}
