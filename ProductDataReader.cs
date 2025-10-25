using System.Data;

namespace RoMars.Test.StreamingDataAPI
{

public static partial class Seeder
    {
        /// <summary>
        /// Custom high-performance IDataReader implementation to stream product data.
        /// </summary>
        private class ProductDataReader : IDataReader
        {
            private readonly long _totalRecords;
            private long _currentIndex;
            private readonly Random _random = Random.Shared;
            private readonly object[] _values = new object[3];

            // Arrays for random name generation (adjective + noun + index)
            private readonly string[] Adjectives = { "Smart", "Turbo", "Ultimate", "Silent", "Premium", "Modular", "Dynamic", "Quantum" };
            private readonly string[] Nouns = { "Widget", "System", "Engine", "Drive", "Unit", "Sensor", "Core", "Module" };

            public ProductDataReader(long totalRecords)
            {
                _totalRecords = totalRecords;
                _currentIndex = 0;
            }

            public bool Read()
            {
                _currentIndex++;
                return _currentIndex <= _totalRecords;
            }

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

            public int FieldCount => 3;

            public bool IsDBNull(int i) => false;

            public int GetValues(object[] values)
            {
                values[0] = GetValue(0);
                values[1] = GetValue(1);
                values[2] = GetValue(2);
                return 3;
            }

            // Minimal required implementation for SqlBulkCopy
            public string GetName(int i) => i switch
            {
                0 => "Id",
                1 => "Name",
                2 => "Price",
                _ => throw new IndexOutOfRangeException()
            };
            public string GetDataTypeName(int i) => GetFieldType(i).Name;
            public Type GetFieldType(int i) => i switch
            {
                0 => typeof(long),
                1 => typeof(string),
                2 => typeof(decimal),
                _ => throw new IndexOutOfRangeException()
            };

            // Other IDataReader members (mostly unused by SqlBulkCopy)
            public void Close() { }
            public int Depth => 0;
            public DataTable GetSchemaTable() => throw new NotSupportedException();
            public bool IsClosed => _currentIndex > _totalRecords;
            public bool NextResult() => false;
            public int RecordsAffected => (int)_currentIndex;

            public object this[string name] => throw new NotImplementedException();

            public object this[int i] => throw new NotImplementedException();

            public void Dispose() { }
            public bool GetBoolean(int i) => throw new NotSupportedException();
            public byte GetByte(int i) => throw new NotSupportedException();
            public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotSupportedException();
            public char GetChar(int i) => throw new NotSupportedException();
            public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => throw new NotSupportedException();
            public IDataReader GetData(int i) => throw new NotSupportedException();
            public string GetString(int i) => (string)GetValue(i);
            public decimal GetDecimal(int i) => (decimal)GetValue(i);
            public DateTime GetDateTime(int i) => throw new NotSupportedException();
            public double GetDouble(int i) => throw new NotSupportedException();
            public float GetFloat(int i) => throw new NotSupportedException();
            public Guid GetGuid(int i) => throw new NotSupportedException();
            public short GetInt16(int i) => throw new NotSupportedException();
            public int GetInt32(int i) => throw new NotSupportedException();
            public long GetInt64(int i) => (long)GetValue(i);
            public int GetOrdinal(string name) => name switch
            {
                "Id" => 0,
                "Name" => 1,
                "Price" => 2,
                _ => throw new IndexOutOfRangeException()
            };
        }
    }
}