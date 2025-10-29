using Microsoft.Extensions.Logging;
using System;

namespace RoMars.StreamingJsonOutput.Framework
{
    /// <summary>
    /// Extension methods for <see cref="ILogger"/> to provide structured, strongly-typed
    /// logging messages specifically for <see cref="StreamingApiResponseJsonResult"/>.
    /// </summary>
    public static partial class ApiResponseLoggerExtensions
    {
        // Streaming ApiResponseJsonResult Events (EventId 1300-1399)
        private const int StreamingApiResponseEventIdBase = 1300;

        [LoggerMessage(EventId = StreamingApiResponseEventIdBase + 0, Level = LogLevel.Trace,
            Message = "Connection ID {ConnectionId}: Starting streaming API Response execution. Thread ID: {ThreadId}")]
        public static partial void LogStreamingStart(this ILogger logger, string connectionId, int threadId);

        [LoggerMessage(EventId = StreamingApiResponseEventIdBase + 1, Level = LogLevel.Debug,
            Message = "Connection ID {ConnectionId}: {ColumnCount} column ordinals and type writers retrieved dynamically. Thread ID: {ThreadId}")]
        public static partial void LogColumnsRetrieved(this ILogger logger, string connectionId, int columnCount, int threadId);

        [LoggerMessage(EventId = StreamingApiResponseEventIdBase + 2, Level = LogLevel.Trace,
            Message = "Connection ID {ConnectionId}: Row {RowCount:N0} streamed. Current duration: {ElapsedMs}ms. Thread ID: {ThreadId}")]
        public static partial void LogRowStreamed(this ILogger logger, string connectionId, long rowCount, double elapsedMs, int threadId);

        [LoggerMessage(EventId = StreamingApiResponseEventIdBase + 3, Level = LogLevel.Warning,
            Message = "Connection ID {ConnectionId}: Unhandled CLR type '{ClrType}' for column '{ColumnName}'. Falling back to GetValue().ToString().")]
        public static partial void LogUnhandledClrType(this ILogger logger, string connectionId, string clrType, string columnName);

        [LoggerMessage(EventId = StreamingApiResponseEventIdBase + 4, Level = LogLevel.Warning,
            Message = "Connection ID {ConnectionId}: Streaming canceled by client disconnect or server shutdown after {RowCount:N0} rows. Thread ID: {ThreadId}")]
        public static partial void LogStreamingCanceled(this ILogger logger, string connectionId, long rowCount, int threadId);

        [LoggerMessage(EventId = StreamingApiResponseEventIdBase + 5, Level = LogLevel.Error,
            Message = "Connection ID {ConnectionId}: CRITICAL ERROR during streaming after {RowCount:N0} rows. Thread ID: {ThreadId}")]
        public static partial void LogStreamingCriticalError(this ILogger logger, Exception ex, string connectionId, long rowCount, int threadId);

        [LoggerMessage(EventId = StreamingApiResponseEventIdBase + 6, Level = LogLevel.Information,
            Message = "Connection ID {ConnectionId}: STREAM COMPLETE. Total duration: {TotalMs}ms. Rows sent: {RowCount:N0}. Thread ID: {ThreadId}")]
        public static partial void LogStreamingComplete(this ILogger logger, string connectionId, double totalMs, long rowCount, int threadId);
    }
}
