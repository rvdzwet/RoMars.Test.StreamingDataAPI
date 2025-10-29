using Microsoft.Extensions.Logging;
using System.Reflection;

namespace RoMars.DataStreaming.Json.LoggerExtensions
{
    /// <summary>
    /// Extension methods for <see cref="ILogger"/> to provide structured, strongly-typed
    /// logging messages for the data streaming JSON components.
    /// This improves searchability and analysis of logs.
    /// </summary>
    internal static partial class DataStreamingJsonLoggerExtensions
    {
        [LoggerMessage(
            EventId = 1000,
            Level = LogLevel.Information,
            Message = "JsonWriterStrategy initialized for Type: {TargetTypeName}.")]
        internal static partial void LogJsonWriterStrategyInitialized(this ILogger logger, string targetTypeName);

        [LoggerMessage(
            EventId = 1001,
            Level = LogLevel.Warning,
            Message = "Property '{PropertyName}' in '{DeclaringTypeName}' is of a type not directly supported " +
                      "for zero-allocation DbDataReader streaming. Falling back to GetValue()?.ToString().")]
        internal static partial void LogUnsupportedPropertyType(this ILogger logger, string propertyName, string declaringTypeName);

        [LoggerMessage(
            EventId = 1002,
            Level = LogLevel.Warning,
            Message = "Column '{ColumnName}' for property '{PropertyName}' in '{DeclaringTypeName}' not found in DbDataReader. Skipping property.")]
        internal static partial void LogColumnNotFound(this ILogger logger, string columnName, string propertyName, string declaringTypeName);

        [LoggerMessage(
            EventId = 1003,
            Level = LogLevel.Error,
            Message = "Error during JSON streaming for row {RowCount}. Details: {ErrorMessage}")]
        internal static partial void LogStreamingError(this ILogger logger, int rowCount, string errorMessage, Exception ex);

        [LoggerMessage(
            EventId = 1004,
            Level = LogLevel.Information,
            Message = "JSON streaming started for {TargetTypeName}. ConnectionId: {ConnectionId}, ThreadId: {ThreadId}.")]
        internal static partial void LogStreamingStart(this ILogger logger, string targetTypeName, string connectionId, int threadId);

        [LoggerMessage(
            EventId = 1005,
            Level = LogLevel.Information,
            Message = "JSON streaming completed for {TargetTypeName}. Duration: {DurationMs:N2}ms, Rows: {RowCount:N0}. ConnectionId: {ConnectionId}, ThreadId: {ThreadId}.")]
        internal static partial void LogStreamingComplete(this ILogger logger, string targetTypeName, double durationMs, int rowCount, string connectionId, int threadId);

        [LoggerMessage(
            EventId = 1006,
            Level = LogLevel.Warning,
            Message = "JSON streaming canceled for {TargetTypeName} after {RowCount:N0} rows. ConnectionId: {ConnectionId}, ThreadId: {ThreadId}.")]
        internal static partial void LogStreamingCanceled(this ILogger logger, string targetTypeName, int rowCount, string connectionId, int threadId);

        [LoggerMessage(
            EventId = 1007,
            Level = LogLevel.Debug,
            Message = "Processed {RowCount:N0} rows for {TargetTypeName}. Elapsed: {ElapsedMs:N2}ms. ConnectionId: {ConnectionId}, ThreadId: {ThreadId}.")]
        internal static partial void LogRowBatchProcessed(this ILogger logger, string targetTypeName, int rowCount, double elapsedMs, string connectionId, int threadId);

        [LoggerMessage(
            EventId = 1008,
            Level = LogLevel.Critical,
            Message = "Reflection error while processing type {Type} property {Property}. Details: {ErrorMessage}")]
        internal static partial void LogReflectionError(this ILogger logger, string type, string property, string errorMessage, Exception ex);

        [LoggerMessage(
            EventId = 1009,
            Level = LogLevel.Debug,
            Message = "Registering writer for type {ClrType} using method {MethodName}.")]
        internal static partial void LogRegisterWriter(this ILogger logger, string clrType, string methodName);

        [LoggerMessage(
            EventId = 1010,
            Level = LogLevel.Error,
            Message = "DataReader field type '{FieldType}' for column '{ColumnName}' is not supported by default writers and cannot be toString()ed due to being null. Skipping this column.")]
        internal static partial void LogReaderWriterFailedNull(this ILogger logger, string fieldType, string columnName);

        [LoggerMessage(
            EventId = 1011,
            Level = LogLevel.Trace,
            Message = "Found {ColumnsMatched} columns for array pattern '{ColumnPrefix}' for property '{PropertyName}'.")]
        internal static partial void LogArrayPatternMatch(this ILogger logger, int columnsMatched, string columnPrefix, string propertyName);

        [LoggerMessage(
            EventId = 1012,
            Level = LogLevel.Error,
            Message = "Interface '{InterfaceName}' cannot be used as a top-level DTO because it has {PropertiesWithoutDataReaderColumn} properties without a [DataReaderColumn] attribute and is not marked as [JsonFlatten].")]
        internal static partial void LogInterfaceMissingDataReaderColumn(this ILogger logger, string interfaceName, int propertiesWithoutDataReaderColumn);

        [LoggerMessage(
            EventId = 1013,
            Level = LogLevel.Error,
            Message = "Unsupported property type for array '{PropertyName}': {PropertyType}. Array elements must be primitive types (string, int, decimal, etc.).")]
        internal static partial void LogUnsupportedArrayPropertyType(this ILogger logger, string propertyName, string propertyType);

        [LoggerMessage(
            EventId = 1014,
            Level = LogLevel.Error,
            Message = "Property '{PropertyName}' in '{DeclaringTypeName}' is an interface but is not marked with [JsonFlatten]. Nested interfaces must be marked as [JsonFlatten] to be processed.")]
        internal static partial void LogNestedInterfaceNotFlattened(this ILogger logger, string propertyName, string declaringTypeName);
    }
}
