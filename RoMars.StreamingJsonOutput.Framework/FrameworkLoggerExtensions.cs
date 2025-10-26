using Microsoft.Extensions.Logging;
using System; // For Guid

namespace RoMars.StreamingJsonOutput.Framework
{
    public static partial class FrameworkLoggerExtensions
    {
        // Category for Database Query Execution (EventId 1000-1099)
        private const int QueryExecutorEventIdBase = 1000;

        public static readonly EventId QueryInitiated = new(
            id: QueryExecutorEventIdBase + 1,
            name: "QueryInitiated");
        public static readonly EventId ConnectionCreated = new(
            id: QueryExecutorEventIdBase + 2,
            name: "ConnectionCreated");
        public static readonly EventId ConnectionOpened = new(
            id: QueryExecutorEventIdBase + 3,
            name: "ConnectionOpened");
        public static readonly EventId CommandNotSqlCommand = new(
            id: QueryExecutorEventIdBase + 4,
            name: "CommandNotSqlCommand");
        public static readonly EventId QuerySetupComplete = new(
            id: QueryExecutorEventIdBase + 5,
            name: "QuerySetupComplete");
        public static readonly EventId TransientDatabaseError = new(
            id: QueryExecutorEventIdBase + 6,
            name: "TransientDatabaseError");
        public static readonly EventId NonTransientDatabaseError = new(
            id: QueryExecutorEventIdBase + 7,
            name: "NonTransientDatabaseError");
        public static readonly EventId UnexpectedError = new(
            id: QueryExecutorEventIdBase + 8,
            name: "UnexpectedError");
        public static readonly EventId FailedToEstablishConnection = new(
            id: QueryExecutorEventIdBase + 9,
            name: "FailedToEstablishConnection");

        // Category for SqlConnectionFactory (EventId 1200-1299)
        public static readonly EventId LogConnectionFactoryInitialized = new(1201, "ConnectionFactoryInitialized");
        public static readonly EventId LogConnectionCreatedSuccessfully = new(1202, "ConnectionCreatedSuccessfully");
        public static readonly EventId LogEmptyConnectionString = new(1203, "EmptyConnectionString");

        // Streaming DbDataReader results (EventId 1100-1199)
        private const int StreamingDbDataReaderEventIdBase = 1100;

        [LoggerMessage(EventId = StreamingDbDataReaderEventIdBase + 0, Level = LogLevel.Trace,
            Message = "Connection ID {ConnectionId}: Starting streaming DbDataReader execution. Thread ID: {ThreadId}")]
        public static partial void LogStreamingStart(this ILogger logger, string connectionId, int threadId);

        [LoggerMessage(EventId = StreamingDbDataReaderEventIdBase + 1, Level = LogLevel.Debug,
            Message = "Connection ID {ConnectionId}: {ColumnCount} column ordinals and type writers retrieved dynamically. Thread ID: {ThreadId}")]
        public static partial void LogColumnsRetrieved(this ILogger logger, string connectionId, int columnCount, int threadId);

        [LoggerMessage(EventId = StreamingDbDataReaderEventIdBase + 2, Level = LogLevel.Trace,
            Message = "Connection ID {ConnectionId}: Row {RowCount:N0} streamed. Current duration: {ElapsedMs}ms. Thread ID: {ThreadId}")]
        public static partial void LogRowStreamed(this ILogger logger, string connectionId, long rowCount, double elapsedMs, int threadId);

        [LoggerMessage(EventId = StreamingDbDataReaderEventIdBase + 3, Level = LogLevel.Warning,
            Message = "Connection ID {ConnectionId}: Unhandled CLR type '{ClrType}' for column '{ColumnName}'. Falling back to GetValue().ToString().")]
        public static partial void LogUnhandledClrType(this ILogger logger, string connectionId, string clrType, string columnName);

        [LoggerMessage(EventId = StreamingDbDataReaderEventIdBase + 4, Level = LogLevel.Warning,
            Message = "Connection ID {ConnectionId}: Streaming canceled by client disconnect or server shutdown after {RowCount:N0} rows. Thread ID: {ThreadId}")]
        public static partial void LogStreamingCanceled(this ILogger logger, string connectionId, long rowCount, int threadId);

        [LoggerMessage(EventId = StreamingDbDataReaderEventIdBase + 5, Level = LogLevel.Error,
            Message = "Connection ID {ConnectionId}: CRITICAL ERROR during streaming after {RowCount:N0} rows. Thread ID: {ThreadId}")]
        public static partial void LogStreamingCriticalError(this ILogger logger, Exception ex, string connectionId, long rowCount, int threadId);

        [LoggerMessage(EventId = StreamingDbDataReaderEventIdBase + 6, Level = LogLevel.Information,
            Message = "Connection ID {ConnectionId}: STREAM COMPLETE. Total duration: {TotalMs}ms. Rows sent: {RowCount:N0}. Thread ID: {ThreadId}")]
        public static partial void LogStreamingComplete(this ILogger logger, string connectionId, double totalMs, long rowCount, int threadId);
    }
}
