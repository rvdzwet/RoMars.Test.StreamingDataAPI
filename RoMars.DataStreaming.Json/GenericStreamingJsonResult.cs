using Microsoft.AspNetCore.Http;
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RoMars.DataStreaming.Json.LoggerExtensions;

namespace RoMars.DataStreaming.Json
{
    /// <summary>
    /// A custom IResult that performs zero-allocation JSON serialization directly to the HTTP response stream.
    /// It uses <see cref="DataReaderJsonWriterStrategy{TInterface}"/> to dynamically map and write
    /// DbDataReader values to a structured JSON format defined by an interface, achieving extreme
    /// performance and low memory footprint. This generic implementation allows streaming data
    /// from any DbDataReader to any interface-defined JSON structure.
    /// </summary>
    /// <typeparam name="TInterface">The interface type that defines the desired JSON output structure.</typeparam>
    public class GenericStreamingJsonResult<TInterface> : IResult where TInterface : class
    {
        private readonly DbConnection _connection;
        private readonly DbDataReader _reader;
        private readonly ILogger<GenericStreamingJsonResult<TInterface>> _logger;
        private readonly DataReaderJsonWriterStrategy<TInterface> _serializerStrategy;

        public GenericStreamingJsonResult(
            DbConnection connection,
            DbDataReader reader,
            ILogger<GenericStreamingJsonResult<TInterface>> logger,
            DataReaderJsonWriterStrategy<TInterface> serializerStrategy)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serializerStrategy = serializerStrategy ?? throw new ArgumentNullException(nameof(serializerStrategy));
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
            var threadId = Environment.CurrentManagedThreadId;

            string targetTypeName = typeof(TInterface).Name;
            _logger.LogStreamingStart(targetTypeName, connectionId, threadId);

            try
            {
                await using (var writer = new Utf8JsonWriter(response.BodyWriter.AsStream(), new JsonWriterOptions { Indented = false }))
                {
                    _logger.LogWriterCreated();
                    writer.WriteStartArray();

                    while (await _reader.ReadAsync(httpContext.RequestAborted))
                    {
                        writer.WriteStartObject(); // Start object for current row
                        _serializerStrategy.Write(writer, _reader);
                        writer.WriteEndObject();   // End object for current row
                        rowCount++;

                        if (rowCount % 5 == 0)
                        {
                            _logger.LogRowBatchProcessed(targetTypeName, rowCount, streamTimer.Elapsed.TotalMilliseconds, connectionId, threadId);
                        }
                    }
                    writer.WriteEndArray();
                    await writer.FlushAsync(httpContext.RequestAborted);
                    _logger.LogWriterFlushed();
                }
            }
            catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogStreamingCanceled(targetTypeName, rowCount, connectionId, threadId);
            }
            catch (Exception ex)
            {
                _logger.LogStreamingError(rowCount, ex.Message, ex);
                throw; // Re-throw the exception to ensure it's handled by higher-level error mechanisms
            }
            finally
            {
                _reader?.Dispose();
                _logger.LogDataReaderDisposed();
                _connection?.Dispose(); // Ensure connection is disposed
                _logger.LogConnectionDisposed();
                streamTimer.Stop();
                _logger.LogStreamingComplete(targetTypeName, streamTimer.Elapsed.TotalMilliseconds, rowCount, connectionId, threadId);
            }
        }
    }
}
