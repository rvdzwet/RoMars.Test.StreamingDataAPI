using Microsoft.Extensions.Logging;
using RoMars.DataStreaming.Json.Attributes;
using RoMars.DataStreaming.Json.LoggerExtensions;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoMars.DataStreaming.Json
{
    // Delegate for writing a single property value from a pre-read PropertyValue to Utf8JsonWriter
    internal delegate void JsonPropertyWriter(Utf8JsonWriter writer, PropertyValue propertyValue);

    // Delegate for reading a single property value from DbDataReader
    internal delegate void DataReaderValueReader(DbDataReader reader, int ordinal, out PropertyValue propertyValue);

    // Represents a value read from a DbDataReader
    internal record PropertyValue(object? Value, bool IsNull);

    // Base record for all serialization plan instructions
    internal abstract record WriteInstruction;

    // Instructions for structuring the JSON output
    internal record WriteStartObjectInstruction(string JsonPropertyName, bool IsFlattened) : WriteInstruction;
    internal record WriteEndObjectInstruction() : WriteInstruction;
    internal record WriteStartArrayInstruction(string JsonPropertyName) : WriteInstruction;
    internal record WriteEndArrayInstruction() : WriteInstruction;

    // Instructions for writing pre-read values to JSON
    internal record WriteValueInstruction(string JsonPropertyName, int PreReadValueIndex, JsonPropertyWriter Writer) : WriteInstruction;
    internal record WriteArrayElementValueInstruction(int PreReadValueIndex, JsonPropertyWriter Writer) : WriteInstruction;

    // Instruction for reading a value from DbDataReader, used during the initial data reading pass
    internal record DataReaderReadValueInstruction(string PropertyName, int DataReaderOrdinal, DataReaderValueReader Reader, int PreReadValueIndex) : WriteInstruction;


    /// <summary>
    /// Analyzes an interface (DTO) decorated with custom attributes to create a reusable
    /// serialization plan for streaming data directly from a DbDataReader to a Utf8JsonWriter,
    /// without intermediate object instantiation.
    /// Supports nested interfaces, property renaming, flattened objects, and arrays from patterned columns.
    /// </summary>
    /// <typeparam name="TInterface">The interface type representing the desired JSON structure.</typeparam>
    public sealed class DataReaderJsonWriterStrategy<TInterface> where TInterface : class
    {
        private readonly ILogger _logger;
        // Combined plan that contains both data reading and JSON writing instructions, in sequential order.
        private readonly IReadOnlyList<WriteInstruction> _plan;
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<WriteInstruction>> _planCache = new();

        // Optimized delegates for writing primitive types to JSON (JsonPropertyWriter)
        private static readonly ConcurrentDictionary<Type, JsonPropertyWriter> _jsonTypeWriters = new();
        // Optimized delegates for reading primitive types from DbDataReader (DataReaderValueReader)
        private static readonly ConcurrentDictionary<Type, DataReaderValueReader> _dataReaderTypeReaders = new();

        public DataReaderJsonWriterStrategy(DbDataReader sampleReader, ILogger<DataReaderJsonWriterStrategy<TInterface>> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogJsonWriterStrategyInitialized(typeof(TInterface).Name);

            // Populate static type writers once
            if (_jsonTypeWriters.IsEmpty || _dataReaderTypeReaders.IsEmpty)
            {
                InitializeTypeDelegates(logger);
                logger.LogTypeDelegatesInitialized();
            }

            // Get or create serialization plan from cache
            _plan = _planCache.GetOrAdd(typeof(TInterface), type =>
            {
                if (_planCache.TryGetValue(type, out var cachedPlan))
                {
                    _logger.LogPlanCacheHit(type.Name);
                    return cachedPlan;
                }
                _logger.LogPlanCacheMiss(type.Name);
                return BuildSerializationPlan(sampleReader, logger);
            });
        }

        // The public method to execute the serialization plan for a single row
        public void Write(Utf8JsonWriter writer, DbDataReader reader)
        {
            // The plan implicitly assumes that DataReaderReadValueInstruction instances are sorted by DataReaderOrdinal.
            // A separate collection is used to store pre-read values, indexed by PreReadValueIndex.
            PropertyValue[] preReadValues = Array.Empty<PropertyValue>();
            
            // First pass: Read all values from the DbDataReader in sequential access order
            foreach (var instruction in _plan)
            {
                if (instruction is DataReaderReadValueInstruction readValueInstruction)
                {
                    // Ensure preReadValues array is sized correctly
                    if (preReadValues.Length <= readValueInstruction.PreReadValueIndex)
                    {
                        Array.Resize(ref preReadValues, readValueInstruction.PreReadValueIndex + 1);
                    }
                    readValueInstruction.Reader(reader, readValueInstruction.DataReaderOrdinal, out preReadValues[readValueInstruction.PreReadValueIndex]);
                }
            }

            // Second pass: Write JSON using the pre-read values
            foreach (var instruction in _plan)
            {
                switch (instruction)
                {
                    case WriteStartObjectInstruction startObj:
                        if (!startObj.IsFlattened && !string.IsNullOrEmpty(startObj.JsonPropertyName))
                        {
                            writer.WritePropertyName(startObj.JsonPropertyName);
                        }
                        writer.WriteStartObject();
                        break;
                    case WriteEndObjectInstruction:
                        writer.WriteEndObject();
                        break;
                    case WriteStartArrayInstruction startArray:
                        if (!string.IsNullOrEmpty(startArray.JsonPropertyName))
                        {
                            writer.WritePropertyName(startArray.JsonPropertyName);
                        }
                        writer.WriteStartArray();
                        break;
                    case WriteEndArrayInstruction:
                        writer.WriteEndArray();
                        break;
                    case WriteValueInstruction value:
                        writer.WritePropertyName(value.JsonPropertyName);
                        value.Writer(writer, preReadValues[value.PreReadValueIndex]);
                        break;
                    case WriteArrayElementValueInstruction arrayValue:
                        arrayValue.Writer(writer, preReadValues[arrayValue.PreReadValueIndex]);
                        break;
                    case DataReaderReadValueInstruction _:
                        // Data reader instructions are processed in the first pass
                        break;
                }
            }
        }

        private IReadOnlyList<WriteInstruction> BuildSerializationPlan(DbDataReader reader, ILogger logger_instance)
        {
            var jsonWriteInstructions = new List<WriteInstruction>();
            var dataReaderReadInstructions = new List<DataReaderReadValueInstruction>();
            var preReadValueIndexCounter = 0;

            BuildPlanForType(typeof(TInterface), jsonWriteInstructions, dataReaderReadInstructions, reader, logger_instance, ref preReadValueIndexCounter, outermost: true);

            // Sort data reader instructions by ordinal for sequential access and combine with JSON write instructions
            var combinedPlan = new List<WriteInstruction>();
            combinedPlan.AddRange(dataReaderReadInstructions.OrderBy(x => x.DataReaderOrdinal));
            combinedPlan.AddRange(jsonWriteInstructions);

            return combinedPlan;
        }

        private void BuildPlanForType(
            Type currentType,
            List<WriteInstruction> jsonWriteInstructions,
            List<DataReaderReadValueInstruction> dataReaderReadInstructions,
            DbDataReader reader,
            ILogger logger_instance,
            ref int preReadValueIndexCounter,
            string? parentJsonPropertyName = null,
            bool isFlattened = false,
            bool outermost = false)
        {
            if (!currentType.IsInterface)
            {
                logger_instance.LogReflectionError(currentType.Name, "N/A", "Only interfaces are supported for DTO definition.", new ArgumentException("Only interfaces are supported."));
                throw new ArgumentException($"Type {currentType.Name} must be an interface.");
            }

            if (!outermost && !isFlattened)
            {
                string jsonPropName = parentJsonPropertyName ?? GetJsonPropertyName(currentType.Name);
                jsonWriteInstructions.Add(new WriteStartObjectInstruction(jsonPropName, false));
            }

            foreach (var property in currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    string jsonPropertyName = GetJsonPropertyName(property.Name, property);
                    logger_instance.LogBuildingPlanForProperty(property.Name, currentType.Name);

                    // Handle IEnumerable (arrays from pattern)
                    var arrayPatternAttr = property.GetCustomAttribute<DataReaderArrayPatternAttribute>();
                    if (arrayPatternAttr != null)
                    {
                        logger_instance.LogPropertyIsArrayPattern(property.Name, arrayPatternAttr.ColumnPrefix);
                        if (!IsEnumerableOfPrimitive(property.PropertyType))
                        {
                            logger_instance.LogUnsupportedArrayPropertyType(property.Name, property.PropertyType.Name);
                            continue; // Skip unsupported array types
                        }

                        jsonWriteInstructions.Add(new WriteStartArrayInstruction(jsonPropertyName));
                        var arrayElementReadValues = new List<(int ordinal, DataReaderValueReader readerDelegate, JsonPropertyWriter writerDelegate, int preReadIndex)>();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string dataReaderColumnName = reader.GetName(i);
                            if (dataReaderColumnName.StartsWith(arrayPatternAttr.ColumnPrefix, StringComparison.OrdinalIgnoreCase))
                            {
                                if (_dataReaderTypeReaders.TryGetValue(reader.GetFieldType(i), out var readerDelegate) &&
                                    _jsonTypeWriters.TryGetValue(reader.GetFieldType(i), out var writerDelegate))
                                {
                                    arrayElementReadValues.Add((i, readerDelegate, writerDelegate, preReadValueIndexCounter++));
                                }
                                else
                                {
                                    // Fallback for unhandled types in arrays
                                    logger_instance.LogUnsupportedPropertyType(property.Name, currentType.Name);
                                    // Make sure GetValue is used for fallback, as GetStringValue might fail for non-string types
                                    arrayElementReadValues.Add((i,
                                        delegate (DbDataReader r, int ord, out PropertyValue out_pv) { out_pv = r.IsDBNull(ord) ? new PropertyValue(null, true) : new PropertyValue(r.GetValue(ord), false); },
                                        (w, pv) => { if (pv.IsNull) w.WriteNullValue(); else w.WriteStringValue(pv.Value?.ToString()); },
                                        preReadValueIndexCounter++));
                                }
                            }
                        }

                        logger_instance.LogArrayPatternMatch(arrayElementReadValues.Count, arrayPatternAttr.ColumnPrefix, property.Name);

                        // Add DataReaderReadValueInstruction for array elements (sorted by ordinal)
                        foreach (var (arrayColumnOrdinal, readerDelegate, writerDelegate, preReadIndex) in arrayElementReadValues.OrderBy(v => v.ordinal))
                        {
                            dataReaderReadInstructions.Add(new DataReaderReadValueInstruction($"{property.Name}[{arrayColumnOrdinal}]", arrayColumnOrdinal, readerDelegate, preReadIndex));
                            jsonWriteInstructions.Add(new WriteArrayElementValueInstruction(preReadIndex, writerDelegate));
                        }
                        
                        jsonWriteInstructions.Add(new WriteEndArrayInstruction());
                        continue; // Move to next property
                    }

                    // Handle Nested Interfaces (objects or flattened objects)
                    if (property.PropertyType.IsInterface)
                    {
                        var flattenAttr = property.GetCustomAttribute<JsonFlattenAttribute>();
                        bool currentIsFlattened = (flattenAttr != null || outermost);
                        logger_instance.LogPropertyIsNestedInterface(property.Name, currentType.Name, currentIsFlattened);

                        if (flattenAttr == null && !outermost) // If it's a nested interface and not a flattened one, it must be nested object
                        {
                            BuildPlanForType(property.PropertyType, jsonWriteInstructions, dataReaderReadInstructions, reader, logger_instance, ref preReadValueIndexCounter, jsonPropertyName, isFlattened: false);
                        }
                        else // If it's flattened, or the outermost (root is always flattened conceptually)
                        {
                            BuildPlanForType(property.PropertyType, jsonWriteInstructions, dataReaderReadInstructions, reader, logger_instance, ref preReadValueIndexCounter, jsonPropertyName, isFlattened: true);
                        }
                        continue; // Move to next property
                    }

                    // Handle primitive properties directly mapped from DataReader
                    var dataReaderColumnAttr = property.GetCustomAttribute<DataReaderColumnAttribute>();
                    string dataReaderColumnNameToMap = dataReaderColumnAttr?.ColumnName ?? property.Name;
                    int ordinal = -1;
                    try
                    {
                        ordinal = reader.GetOrdinal(dataReaderColumnNameToMap);
                        logger_instance.LogPropertyIsPrimitive(property.Name, dataReaderColumnNameToMap, ordinal);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        logger_instance.LogColumnNotFound(dataReaderColumnNameToMap, property.Name, currentType.Name);
                        continue; // Skip if column not found
                    }

                    if (_dataReaderTypeReaders.TryGetValue(property.PropertyType, out var dataReaderAction) &&
                        _jsonTypeWriters.TryGetValue(property.PropertyType, out var jsonWriterAction))
                    {
                        int currentPreReadIndex = preReadValueIndexCounter++;
                        dataReaderReadInstructions.Add(new DataReaderReadValueInstruction(property.Name, ordinal, dataReaderAction, currentPreReadIndex));
                        jsonWriteInstructions.Add(new WriteValueInstruction(jsonPropertyName, currentPreReadIndex, jsonWriterAction));
                    }
                    else
                    {
                        // Fallback: If type not in our optimized readers/writers, try string conversion
                        logger_instance.LogUnsupportedPropertyType(property.Name, currentType.Name);
                        int currentPreReadIndex = preReadValueIndexCounter++;
                        dataReaderReadInstructions.Add(new DataReaderReadValueInstruction(property.Name, ordinal,
                            delegate (DbDataReader r, int ord, out PropertyValue out_pv) { out_pv = r.IsDBNull(ord) ? new PropertyValue(null, true) : new PropertyValue(r.GetValue(ord), false); },
                            currentPreReadIndex));
                        jsonWriteInstructions.Add(new WriteValueInstruction(jsonPropertyName, currentPreReadIndex,
                            (w, pv) => { if (pv.IsNull) w.WriteNullValue(); else w.WriteStringValue(pv.Value?.ToString()); }));
                    }
                }
                catch (Exception ex)
                {
                    logger_instance.LogReflectionError(currentType.Name, property.Name, ex.Message, ex);
                    throw; // Re-throw to indicate a critical setup error
                }
            }

            if (!outermost && !isFlattened)
            {
                jsonWriteInstructions.Add(new WriteEndObjectInstruction());
            }
        }

        private string GetJsonPropertyName(string defaultName, PropertyInfo? property = null)
        {
            var jsonPropertyName = property?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            return jsonPropertyName ?? defaultName;
        }

        private bool IsEnumerableOfPrimitive(Type type)
        {
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IEnumerable<>))
            {
                return false;
            }
            var itemType = type.GetGenericArguments()[0];
            return itemType.IsPrimitive || itemType == typeof(string) || itemType == typeof(decimal) ||
                   itemType == typeof(DateTime) || itemType == typeof(Guid);
        }

        private static void InitializeTypeDelegates(ILogger logger)
        {
            // JsonPropertyWriter Initializations
            _jsonTypeWriters.TryAdd(typeof(bool), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteBooleanValue((bool)pv.Value!); });
            _jsonTypeWriters.TryAdd(typeof(byte), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteNumberValue((byte)pv.Value!); });
            _jsonTypeWriters.TryAdd(typeof(char), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteStringValue(((char)pv.Value!).ToString()); });
            _jsonTypeWriters.TryAdd(typeof(DateTime), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteStringValue((DateTime)pv.Value!); });
            _jsonTypeWriters.TryAdd(typeof(decimal), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteNumberValue((decimal)pv.Value!); });
            _jsonTypeWriters.TryAdd(typeof(double), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteNumberValue((double)pv.Value!); });
            _jsonTypeWriters.TryAdd(typeof(float), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteNumberValue((float)pv.Value!); });
            _jsonTypeWriters.TryAdd(typeof(Guid), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteStringValue((Guid)pv.Value!); });
            _jsonTypeWriters.TryAdd(typeof(short), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteNumberValue((short)pv.Value!); });
            _jsonTypeWriters.TryAdd(typeof(int), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteNumberValue((int)pv.Value!); });
            _jsonTypeWriters.TryAdd(typeof(long), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteNumberValue((long)pv.Value!); });
            _jsonTypeWriters.TryAdd(typeof(string), (writer, pv) => { if (pv.IsNull) writer.WriteNullValue(); else writer.WriteStringValue((string)pv.Value!); });

            // DataReaderValueReader Initializations
            _dataReaderTypeReaders.TryAdd(typeof(bool), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetBoolean(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(byte), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetByte(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(char), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetChar(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(DateTime), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetDateTime(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(decimal), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetDecimal(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(double), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetDouble(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(float), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetFloat(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(Guid), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetGuid(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(short), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetInt16(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(int), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetInt32(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(long), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetInt64(i), false); });
            _dataReaderTypeReaders.TryAdd(typeof(string), delegate (DbDataReader r, int i, out PropertyValue out_pv) { out_pv = r.IsDBNull(i) ? new PropertyValue(null, true) : new PropertyValue(r.GetString(i), false); });

            logger.LogRegisterWriter(typeof(bool).Name, "WriteBooleanValue");
            logger.LogRegisterWriter(typeof(byte).Name, "WriteNumberValue");
            logger.LogRegisterWriter(typeof(char).Name, "WriteStringValue");
            logger.LogRegisterWriter(typeof(DateTime).Name, "WriteStringValue");
            logger.LogRegisterWriter(typeof(decimal).Name, "WriteNumberValue");
            logger.LogRegisterWriter(typeof(double).Name, "WriteNumberValue");
            logger.LogRegisterWriter(typeof(float).Name, "WriteNumberValue");
            logger.LogRegisterWriter(typeof(Guid).Name, "WriteStringValue");
            logger.LogRegisterWriter(typeof(short).Name, "WriteNumberValue");
            logger.LogRegisterWriter(typeof(int).Name, "WriteNumberValue");
            logger.LogRegisterWriter(typeof(long).Name, "WriteNumberValue");
            logger.LogRegisterWriter(typeof(string).Name, "WriteStringValue");
        }
    }
}
