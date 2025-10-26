using Microsoft.Extensions.Logging;
using System.Data; // Required for SqlDbType in LoggerMessage

namespace RoMars.StreamingJsonOutput.Host.Extensions
{
    public static partial class LoggerExtensions
    {
        // Streaming DbDataReader results (EventId 1000-1099)
        private const int StreamingDbDataReaderEventIdBase = 1000;

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


        [LoggerMessage(EventId = 1007, Level = LogLevel.Information,
            Message = "Database has {CurrentCount:N0} records. Target is {TargetCount:N0}. Starting seeding...")]
        public static partial void LogSeedingStarted(this ILogger logger, long currentCount, long targetCount);

        [LoggerMessage(EventId = 1008, Level = LogLevel.Information,
            Message = "Database already has target records ({CurrentCount:N0}). Seeding skipped.")]
        public static partial void LogSeedingSkippedTarget(this ILogger logger, long currentCount);
        
        // DocumentMetadataTableManager Log Messages (EventId 2000-2099)
        [LoggerMessage(EventId = 2000, Level = LogLevel.Trace, Message = "Executing command to ensure table existence...")]
        public static partial void LogTableExistenceCommand(this ILogger logger);

        [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "CREATE TABLE command executed. Rows affected (expected 0/1): {RowsAffected}")]
        public static partial void LogCreateTableCommandExecuted(this ILogger logger, int rowsAffected);

        [LoggerMessage(EventId = 2002, Level = LogLevel.Trace, Message = "Executing command to ensure index existence...")]
        public static partial void LogIndexExistenceCommand(this ILogger logger);

        [LoggerMessage(EventId = 2003, Level = LogLevel.Debug, Message = "CREATE INDEX command executed. Rows affected (expected 0/1): {RowsAffected}")]
        public static partial void LogCreateIndexCommandExecuted(this ILogger logger, int rowsAffected);

        [LoggerMessage(EventId = 2004, Level = LogLevel.Trace, Message = "DocumentMetadata table schema ensured in {ElapsedMs}ms. ThreadID: {ThreadId}")]
        public static partial void LogTableSchemaEnsured(this ILogger logger, double elapsedMs, int threadId);

        [LoggerMessage(EventId = 2005, Level = LogLevel.Debug, Message = "Finished table schema existence check.")]
        public static partial void LogFinishedTableSchemaCheck(this ILogger logger);

        [LoggerMessage(EventId = 2006, Level = LogLevel.Trace, Message = "Counted {Count:N0} records in DocumentMetadata table in {ElapsedMs}ms. ThreadID: {ThreadId}")]
        public static partial void LogRecordCounted(this ILogger logger, long count, double elapsedMs, int threadId);

        [LoggerMessage(EventId = 2007, Level = LogLevel.Debug, Message = "Finished record count retrieval. Total records: {Count:N0}")]
        public static partial void LogFinishedRecordCountRetrieval(this ILogger logger, long count);

        [LoggerMessage(EventId = 2008, Level = LogLevel.Trace, Message = "Mapping {Name} (NVarChar) to {Definition}")]
        public static partial void LogMappingNVarChar(this ILogger logger, string name, string definition);

        [LoggerMessage(EventId = 2009, Level = LogLevel.Trace, Message = "Mapping {Name} (Decimal) to {Definition}")]
        public static partial void LogMappingDecimal(this ILogger logger, string name, string definition);

        [LoggerMessage(EventId = 2010, Level = LogLevel.Trace, Message = "Mapping {Name} (Int) to INT")]
        public static partial void LogMappingInt(this ILogger logger, string name);

        [LoggerMessage(EventId = 2011, Level = LogLevel.Trace, Message = "Mapping {Name} (BigInt) to BIGINT")]
        public static partial void LogMappingBigInt(this ILogger logger, string name);

        [LoggerMessage(EventId = 2012, Level = LogLevel.Trace, Message = "Mapping {Name} (DateTime2) to DATETIME2")]
        public static partial void LogMappingDateTime2(this ILogger logger, string name);

        [LoggerMessage(EventId = 2013, Level = LogLevel.Error, Message = "Unsupported SQL Type: {SqlType} for column {ColName}")]
        public static partial void LogUnsupportedSqlType(this ILogger logger, System.Data.SqlDbType sqlType, string colName);

        [LoggerMessage(EventId = StreamingDbDataReaderEventIdBase + 4, Level = LogLevel.Warning,
            Message = "Connection ID {ConnectionId}: Streaming canceled by client disconnect or server shutdown after {RowCount:N0} rows. Thread ID: {ThreadId}")]
        public static partial void LogStreamingCanceled(this ILogger logger, string connectionId, long rowCount, int threadId);

        [LoggerMessage(EventId = StreamingDbDataReaderEventIdBase + 5, Level = LogLevel.Error,
            Message = "Connection ID {ConnectionId}: CRITICAL ERROR during streaming after {RowCount:N0} rows. Thread ID: {ThreadId}")]
        public static partial void LogStreamingCriticalError(this ILogger logger, Exception ex, string connectionId, long rowCount, int threadId);

        [LoggerMessage(EventId = StreamingDbDataReaderEventIdBase + 6, Level = LogLevel.Information,
            Message = "Connection ID {ConnectionId}: STREAM COMPLETE. Total duration: {TotalMs}ms. Rows sent: {RowCount:N0}. Thread ID: {ThreadId}")]
        public static partial void LogStreamingComplete(this ILogger logger, string connectionId, double totalMs, long rowCount, int threadId);

        // DocumentMetadataTableManager Log Messages (EventId 2000-2099)
        // These are already defined with their EventIds: 2000-2018.

        // Seeder Log Messages (EventId 3000-3099)
        // These are already defined with their EventIds: 3000-3006.

        // DocumentMetadataDataReader Log Messages (EventId 4000-4099)
        // These are already defined with their EventIds: 4000-4004.
        
        // New DocumentMetadataTableManager Log Messages
        [LoggerMessage(EventId = 2014, Level = LogLevel.Trace, Message = "COUNT SQL: {Sql}")]
        public static partial void LogCountSql(this ILogger logger, string sql);

        [LoggerMessage(EventId = 2015, Level = LogLevel.Debug, Message = "Created database connection for counting.")]
        public static partial void LogConnectionCreatedForCounting(this ILogger logger);

        [LoggerMessage(EventId = 2016, Level = LogLevel.Debug, Message = "Successfully opened connection for counting.")]
        public static partial void LogConnectionOpenedForCounting(this ILogger logger);

        [LoggerMessage(EventId = 2017, Level = LogLevel.Trace, Message = "Executing COUNT query...")]
        public static partial void LogExecutingCountQuery(this ILogger logger);

        [LoggerMessage(EventId = 2018, Level = LogLevel.Debug, Message = "DocumentMetadataTableManager initialized.")]
        public static partial void LogTableManagerInitialized(this ILogger logger);

        // Seeder Log Messages (EventId 3000-3099)
        [LoggerMessage(EventId = 3000, Level = LogLevel.Information,
            Message = "Starting database seeding, appending {RecordsToGenerate:N0} records. Initial count: {InitialCount:N0}, Target count: {TotalTargetCount:N0}. ThreadID: {ThreadId}")]
        public static partial void LogSeedingOperationStart(this ILogger logger, long recordsToGenerate, long initialCount, long totalTargetCount, int threadId);

        [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
            Message = "Seeding progress: {CurrentTotalRecords:N0} / {TotalTargetRecords:N0} records ({Percentage:F2}%). Batch: {BatchSize:N0}. ERT: {ERT}")]
        public static partial void LogSeedingProgress(this ILogger logger, long currentTotalRecords, long totalTargetRecords, double percentage, int batchSize, string ert);

        [LoggerMessage(EventId = 3002, Level = LogLevel.Trace,
            Message = "Generating next batch of {BatchSize:N0} records starting from index {StartIndex:N0}.")]
        public static partial void LogGeneratingBatch(this ILogger logger, long batchSize, long startIndex);

        [LoggerMessage(EventId = 3003, Level = LogLevel.Trace,
            Message = "SqlBulkCopy finished writing batch to server. Current count: {CurrentCount:N0}.")]
        public static partial void LogBatchWriteComplete(this ILogger logger, long currentCount);

        [LoggerMessage(EventId = 3004, Level = LogLevel.Warning,
            Message = "Seeding canceled by token after {TotalRowsCopied:N0} rows. ThreadID: {ThreadId}")]
        public static partial void LogSeedingCanceled(this ILogger logger, long totalRowsCopied, int threadId);

        [LoggerMessage(EventId = 3005, Level = LogLevel.Error,
            Message = "SqlBulkCopy failed during seeding after {TotalRowsCopied:N0} rows. ThreadID: {ThreadId}")]
        public static partial void LogSeedingFailed(this ILogger logger, Exception ex, long totalRowsCopied, int threadId);

        [LoggerMessage(EventId = 3006, Level = LogLevel.Information,
            Message = "Database seeding complete. Total new records seeded in this operation: {TotalRowsCopied:N0}. Total final records: {FinalCount:N0}. Total time: {Elapsed}. ThreadID: {ThreadId}")]
        public static partial void LogSeedingComplete(this ILogger logger, long totalRowsCopied, long finalCount, TimeSpan elapsed, int threadId);

        // DocumentMetadataDataReader Log Messages (EventId 4000-4099)
        private const int DataReaderEventIdBase = 4000;

        public static readonly EventId LogDataReaderInitialized = new(
            id: DataReaderEventIdBase + 1,
            name: "DataReaderInitialized");
        public static readonly EventId LogDataReaderRead = new(
            id: DataReaderEventIdBase + 2,
            name: "DataReaderRead");
        public static readonly EventId LogDataReaderGetValue = new(
            id: DataReaderEventIdBase + 3,
            name: "DataReaderGetValue");
        public static readonly EventId LogDataReaderUnhandledColumn = new(
            id: DataReaderEventIdBase + 4,
            name: "DataReaderUnhandledColumn");

        // General Application Log Messages (EventId 5000-5099)
        private const int GeneralAppEventIdBase = 5000;

        public static readonly EventId LogApplicationTerminatedUnexpectedly = new(
            id: GeneralAppEventIdBase + 1,
            name: "ApplicationTerminatedUnexpectedly");
    }
}
