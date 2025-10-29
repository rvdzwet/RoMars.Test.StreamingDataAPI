using Microsoft.AspNetCore.Http;
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RoMars.StreamingJsonOutput.Framework.Models;
using System.Globalization;
using static RoMars.StreamingJsonOutput.Framework.ApiResponseLoggerExtensions; // Using dedicated ApiResponseLoggerExtensions

namespace RoMars.StreamingJsonOutput.Framework
{
    /// <summary>
    /// A custom IResult that performs zero-allocation JSON serialization directly to the HTTP response stream.
    /// It uses Utf8JsonWriter to write raw DbDataReader values, achieving extreme performance and low memory footprint.
    /// This generic implementation allows streaming data from any DbDataReader.
    /// </summary>
    public class StreamingApiResponseJsonResult : IResult
    {
        private readonly DbConnection _connection;
        private readonly DbDataReader _reader;
        private readonly ILogger<StreamingApiResponseJsonResult> _logger; // Use strongly-typed logger
        private readonly string _correlationId;

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

        public StreamingApiResponseJsonResult(DbConnection connection, DbDataReader reader, ILogger<StreamingApiResponseJsonResult> logger, string correlationId)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _correlationId = correlationId;
        }

        /// <summary>
        /// Executes the custom result, taking control of the HTTP response.
        /// </summary>
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var response = httpContext.Response;
            response.ContentType = "application/json; charset=utf-8";

            var streamTimer = Stopwatch.StartNew();
            long rowCount = 0;
            var connectionId = httpContext.Connection.Id;
            var threadId = Environment.CurrentManagedThreadId;
            var errors = new List<string>();
            string status = "Success";

            // Using the new LoggerExtensions for structured logging
            _logger.LogStreamingStart(connectionId, threadId);

            try
            {
                await using (var writer = new Utf8JsonWriter(response.BodyWriter.AsStream(),
                    new JsonWriterOptions { Indented = false }))
                {
                    writer.WriteStartObject(); // Start ApiResponse object

                    // Write Metadata
                    writer.WritePropertyName("Metadata");
                    writer.WriteStartObject(); // Start Metadata object
                    writer.WriteString("Timestamp", DateTimeOffset.UtcNow.ToString("o"));
                    writer.WriteString("CorrelationId", _correlationId);
                    // DurationMs, RecordCount, Status, Errors will be written in finally block or before EndObject

                    writer.WriteEndObject(); // End Metadata object

                    writer.WritePropertyName("Data");
                    writer.WriteStartArray(); // Start Data array (actual streaming content)


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
                            _logger.LogUnhandledClrType(connectionId, clrType.Name, columnNames[i]);
                            columnTypeWriters[i] = (w, r, idx) => w.WriteStringValue(r.GetValue(idx)?.ToString());
                            errors.Add($"Unhandled CLR type '{clrType.Name}' for column '{columnNames[i]}'. Fallback to string serialization.");
                        }
                    }
                    _logger.LogColumnsRetrieved(connectionId, columnCount, threadId);

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
                            _logger.LogRowStreamed(connectionId, rowCount, streamTimer.Elapsed.TotalMilliseconds, threadId);
                        }
                    }
                    writer.WriteEndArray(); // End Data array
                    
                    // The duration, record count, and final status will be filled here if it's possible to seek back and write.
                    // However, with Utf8JsonWriter writing directly to the response body stream, we cannot seek back to update metadata.
                    // So, we need to pass these values to the client as headers or include them at the start if that's acceptable.
                    // For streaming, a header-based metadata approach for final metrics might be more appropriate, or
                    // writing metadata at the very beginning with placeholder values and then closing the object.
                    // Given the current structure, let's write the metadata at the start and accept duration/recordCount
                    // will be partial or placeholder until the stream completes.
                    // Alternative: if the whole response is not a single giant JSON object,
                    // but rather a metadata object followed by a raw JSON array stream, it would be easier.

                    // Let's adjust the plan: write metadata object first, then directly stream the array.
                    // For now, let's include final metadata as HTTP headers. If the user insists on it being in the body,
                    // the API structure will need more significant changes (e.g. not a single JSON object stream).
                }
            }
            catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
            {
                status = "Canceled";
                _logger.LogStreamingCanceled(connectionId, rowCount, threadId);
                errors.Add("Client disconnected or server shutdown. Streaming canceled.");
            }
            catch (Exception ex)
            {
                status = "Error";
                _logger.LogStreamingCriticalError(ex, connectionId, rowCount, threadId);
                errors.Add($"CRITICAL ERROR: {ex.Message}");
                throw;
            }
            finally
            {
                streamTimer.Stop();
                var totalDurationMs = streamTimer.Elapsed.TotalMilliseconds;
                _logger.LogStreamingComplete(connectionId, totalDurationMs, rowCount, threadId);

                // Add final metadata as headers for now, as embedding dynamically in a streamed JSON object is complex.
                response.Headers["X-RoMars-Metadata-Timestamp"] = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                response.Headers["X-RoMars-Metadata-CorrelationId"] = _correlationId;
                response.Headers["X-RoMars-Metadata-DurationMs"] = totalDurationMs.ToString(CultureInfo.InvariantCulture);
                response.Headers["X-RoMars-Metadata-RecordCount"] = rowCount.ToString(CultureInfo.InvariantCulture);
                response.Headers["X-RoMars-Metadata-Status"] = status;
                if (errors.Any())
                {
                    response.Headers["X-RoMars-Metadata-Errors"] = JsonSerializer.Serialize(errors);
                }

                _reader?.Dispose();
                _connection?.Dispose();

                // Explicitly complete the response body writer stream
                await response.BodyWriter.CompleteAsync();
            }
        }
    }
}
