using System.Data;

namespace RoMars.Test.StreamingDataAPI
{
    /// <summary>
    /// Custom high-performance IDataReader implementation to stream product data.
    /// This class adheres to the Single Responsibility Principle, focusing solely on
    /// generating synthetic product data for seeding. It also implements the
    /// IProductDataGenerator interface, adhering to the Open/Closed principle
    /// by allowing new data generation strategies to be introduced without
    /// modifying consumers.
    /// </summary>
    public class ProductDataReader : IDataReader, IProductDataGenerator
    {
        // Performance Comment: Using a field-backed property for TotalRecords ensures
        // direct access without re-calculation, which is crucial for high-frequency
        // data generation operations like bulk seeding.
        public long TotalRecords => _totalRecords;

        private readonly long _totalRecords;
        private long _currentIndex;
        // Performance Comment: Random.Shared is used for thread-safe random number generation
        // in .NET 6+, avoiding contention and seeding issues that can arise with
        // multiple Random instances in a multi-threaded context.
        private readonly Random _random = Random.Shared;

        // Performance Comment: Pre-allocating the object array '_values' prevents repeated
        // memory allocations during each 'GetValues' call, reducing GC pressure
        // and improving performance in high-throughput scenarios like data streaming.
        private readonly object[] _values = new object[3];

        // Arrays for random name generation (adjective + noun + index)
        private readonly string[] Adjectives = { "Smart", "Turbo", "Ultimate", "Silent", "Premium", "Modular", "Dynamic", "Quantum" };
        private readonly string[] Nouns = { "Widget", "System", "Engine", "Drive", "Unit", "Sensor", "Core", "Module" };

        public ProductDataReader(long totalRecords)
        {
            _totalRecords = totalRecords;
            _currentIndex = 0;
        }

        // Performance Comment: This 'Read' method incrementally advances a counter.
        // Its simplicity (just a counter increment and comparison) is key to its
        // performance, allowing for extremely fast pseudo-data generation without
        // I/O overhead or complex logic for each record.
        public bool Read()
        {
            _currentIndex++;
            return _currentIndex <= _totalRecords;
        }

        // Performance Comment: The 'GetValue' method efficiently computes each field
        // based on the current index and pre-defined arrays. Direct switch-case
        // based on ordinal is faster than reflection or string-based lookups.
        // Random operations are lightweight and optimized for speed.
        public object GetValue(int i)
        {
            // The mapping must be ordered: 0 (Id), 1 (Name), 2 (Price)
            switch (i)
            {
                case 0: // Id (BIGINT)
                    return _currentIndex;

                case 1: // Name (NVARCHAR)
                        // Generate a descriptive, random name with the unique index
                    string adjective = Adjectives[_random.Next(Adjectives.Length)];
                    string noun = Nouns[_random.Next(Nouns.Length)];
                    return $"{adjective} {noun} #{_currentIndex}";

                case 2: // Price (DECIMAL)
                        // Generate a random double between 1.00 and 1000.00 and round to two decimal places for realism.
                    double randomValue = 1.00 + _random.NextDouble() * 999.00;
                    decimal price = Math.Round((decimal)randomValue, 2);
                    return price;

                default:
                    throw new IndexOutOfRangeException();
            }
        }

        // --- IDataReader Implementation Details ---
        // Performance Comment: FieldCount is a constant, ensuring immediate return
        // without computation, critical for performance-sensitive data readers.
        public int FieldCount => 3;

        // Performance Comment: For synthetic data, explicitly stating no DBNulls
        // avoids unnecessary checks or more complex logic, optimizing access paths.
        public bool IsDBNull(int i) => false;

        // Performance Comment: Directly accessing and assigning values from GetValue(i)
        // minimizes overhead compared to iterating or using less efficient methods.
        public int GetValues(object[] values)
        {
            values[0] = GetValue(0);
            values[1] = GetValue(1);
            values[2] = GetValue(2);
            return 3;
        }

        // Minimal required implementation primarily for SqlBulkCopy
        // Performance Comment: Using a switch expression (`=> i switch`) is a concise
        // and efficient way to map ordinals to names, avoiding the overhead of IF/ELSE IF.
        public string GetName(int i) => i switch
        {
            0 => "Id",
            1 => "Name",
            2 => "Price",
            _ => throw new IndexOutOfRangeException()
        };
        // Performance Comment: Directly maps to GetFieldType, efficiently providing metadata.
        public string GetDataTypeName(int i) => GetFieldType(i).Name;
        // Performance Comment: Using a switch expression for type mapping is efficient.
        // It provides direct type information without complex runtime checks.
        public Type GetFieldType(int i) => i switch
        {
            0 => typeof(long),
            1 => typeof(string),
            2 => typeof(decimal),
            _ => throw new IndexOutOfRangeException()
        };

        // Other IDataReader members (mostly unused by SqlBulkCopy, minimal implementation for completeness)
        public void Close() { }
        public int Depth => 0;
        public DataTable GetSchemaTable() => throw new NotSupportedException("Schema table is not supported for this data generator.");
        public bool IsClosed => _currentIndex > _totalRecords;
        public bool NextResult() => false; // Only one result set is generated
        public int RecordsAffected => (int)_currentIndex;

        public object this[string name] => throw new NotImplementedException("Indexed access by name is not implemented for this data generator.");
        public object this[int i] => throw new NotImplementedException("Indexed access by ordinal is not implemented, use GetValue(i).");

        public void Dispose() { } // No unmanaged resources
        public bool GetBoolean(int i) => throw new NotSupportedException("Boolean type not supported.");
        public byte GetByte(int i) => throw new NotSupportedException("Byte type not supported.");
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotSupportedException("Byte array retrieval not supported.");
        public char GetChar(int i) => throw new NotSupportedException("Char type not supported.");
        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => throw new NotSupportedException("Char array retrieval not supported.");
        public IDataReader GetData(int i) => throw new NotSupportedException("Nested data readers not supported.");
        public string GetString(int i) => (string)GetValue(i);
        public decimal GetDecimal(int i) => (decimal)GetValue(i);
        public DateTime GetDateTime(int i) => throw new NotSupportedException("DateTime type not supported.");
        public double GetDouble(int i) => throw new NotSupportedException("Double type not supported.");
        public float GetFloat(int i) => throw new NotSupportedException("Float type not supported.");
        public Guid GetGuid(int i) => throw new NotSupportedException("Guid type not supported.");
        public short GetInt16(int i) => throw new NotSupportedException("Int16 type not supported.");
        public int GetInt32(int i) => throw new NotSupportedException("Int32 type not supported.");
        public long GetInt64(int i) => (long)GetValue(i); // Performance Comment: Directly casts GetValue(i) for efficient access.
        public int GetOrdinal(string name) => name switch
        {
            "Id" => 0,
            "Name" => 1,
            "Price" => 2,
            _ => throw new IndexOutOfRangeException($"Column '{name}' not found.")
        };
    }
}
