using System.Data;

namespace RoMars.StreamingJsonOutput.Host
{
    /// <summary>
    /// Custom high-performance IDataReader implementation to stream generated document metadata.
    /// This class adheres to the Single Responsibility Principle, focusing solely on
    /// generating synthetic document metadata for seeding. It implements the
    /// IDocumentMetadataGenerator interface, adhering to the Open/Closed principle
    /// by allowing new data generation strategies without modifying existing consumers.
    /// </summary>
    /// <remarks>
    /// Performance Comment: This custom IDataReader is designed for zero-allocation data generation.
    /// It avoids creating a collection of objects in memory for the entire dataset,
    /// instead generating one record at a time directly to the SqlBulkCopy stream.
    /// </remarks>
    public class DocumentMetadataDataReader : IDataReader
    {
        public long TotalRecords => _recordsToGenerate; // Number of records this instance is generating

        private readonly long _recordsToGenerate;
        private long _currentIndex;
        private readonly long _startingId;
        // Performance Comment: Random.Shared is used for thread-safe random number generation
        // in .NET 6+, avoiding contention and seeding issues that can arise with
        // multiple Random instances in a multi-threaded context.
        private readonly Random _random = Random.Shared;

        // Performance Comment: Pre-allocating the object array '_values' prevents repeated
        // memory allocations during each 'GetValues' call, reducing GC pressure
        // and improving performance in high-throughput scenarios like data streaming.
        private readonly object[] _values; // Size will be determined by schema

        private static readonly IReadOnlyList<DocumentMetadataSchema.ColumnInfo> _schema = DocumentMetadataSchema.Columns;

        public DocumentMetadataDataReader(long startingId, long recordsToGenerate)
        {
            _startingId = startingId;
            _recordsToGenerate = recordsToGenerate;
            _currentIndex = 0;
            _values = new object[_schema.Count]; // Initialize _values array based on schema count
        }

        // Performance Comment: This 'Read' method incrementally advances a counter.
        // Its simplicity (just a counter increment and comparison) is key to its
        // performance, allowing for extremely fast pseudo-data generation without
        // I/O overhead or complex logic for each record.
        public bool Read()
        {
            _currentIndex++;
            return _currentIndex <= _recordsToGenerate;
        }

        /// <summary>
        /// Generates a value for the specified column based on its type and current index.
        /// Performance Comment: The 'GetValue' method efficiently computes each field
        /// based on its type and the current iteration (`_currentIndex`). Direct type-specific
        /// data generation prevents boxing/unboxing overhead and provides faster access.
        /// String interpolations are carefully used to minimize new string allocations where possible,
        /// preferring a fixed set of adjectives/nouns and appending the index.
        /// </summary>
        public object GetValue(int i)
        {
            var column = _schema[i];
            int uniqueIdFragment = (int)((_startingId + _currentIndex) % 10000); // Use a fragment for varying categorical data

            if (column.Name == "DocumentId") return _startingId + _currentIndex;
            if (column.Name == "DocumentTitle") return $"{GenerateRandomWord()} Document #{_startingId + _currentIndex}";
            if (column.Name == "MortgageAmount") return Math.Round((decimal)(200000.00 + _random.NextDouble() * 800000.00), 2); // 200k to 1M

            // Text/String Columns
            if (column.Name.StartsWith("DocumentType")) return _random.NextDouble() < 0.5 ? "LoanApplication" : "MortgageAgreement";
            if (column.Name.StartsWith("FileType")) return _random.NextDouble() < 0.5 ? "PDF" : "JPG";
            if (column.Name.StartsWith("CustomerName")) return $"Customer_{uniqueIdFragment:D4}";
            if (column.Name.StartsWith("CustomerAddress_Line1")) return $"123 Main St, Apt {_random.Next(1, 100)}";
            if (column.Name.StartsWith("CustomerAddress_Line2")) return $"Building {(_random.Next(10) > 7 ? 'B' : 'A')}";
            if (column.Name.StartsWith("CustomerCity")) return _random.NextDouble() < 0.5 ? "New York" : "Los Angeles";
            if (column.Name.StartsWith("CustomerState")) return _random.NextDouble() < 0.5 ? "NY" : "CA";
            if (column.Name.StartsWith("CustomerZip")) return $"{_random.Next(10000, 99999):D5}";
            if (column.Name.StartsWith("LoanNumber")) return $"LN-{_currentIndex:D8}";
            if (column.Name.StartsWith("PropertyAddress_Street")) return $"{_random.Next(100, 999)} Oak Ave";
            if (column.Name.StartsWith("PropertyAddress_City")) return _random.NextDouble() < 0.5 ? "Houston" : "Chicago";
            if (column.Name.StartsWith("PropertyAddress_State")) return _random.NextDouble() < 0.5 ? "TX" : "IL";
            if (column.Name.StartsWith("PropertyAddress_Zip")) return $"{_random.Next(10000, 99999):D5}";
            if (column.Name.StartsWith("OriginalFilename")) return $"{GenerateRandomWord()}_{_currentIndex}.{(_random.NextDouble() < 0.5 ? "pdf" : "jpg")}";
            if (column.Name.StartsWith("SourceSystem")) return _random.NextDouble() < 0.5 ? "CRM" : "LOS";
            if (column.Name.StartsWith("WorkflowStatus")) return _random.NextDouble() < 0.5 ? "PendingReview" : "Approved";
            if (column.Name.StartsWith("ReviewerName")) return $"Reviewer_{_random.Next(1, 10):D2}";
            if (column.Name.StartsWith("Tag_")) return $"TagX{_random.Next(1, 5):D1}";
            if (column.Name.StartsWith("Comment_")) return $"Long comment for Document #{_currentIndex}: this describes various details about the document content. This is a semi-relevant text field for demonstration purposes.";
            if (column.Name == "DocumentHash_MD5") return GenerateMd5HashFragment();

            // Numeric Columns
            if (column.Name == "PageCount") return _random.Next(5, 500);
            if (column.Name == "DocumentSizeKB") return _random.Next(100, 100000);
            if (column.Name == "VersionNumber") return _random.Next(1, 10);
            if (column.Name == "RetentionYears") return _random.Next(5, 20);
            if (column.Name == "CreditScore") return _random.Next(300, 850);
            if (column.Name == "LoanTermMonths") return _random.Next(120, 360);
            if (column.Name == "InterestRate") return Math.Round((decimal)(2.500 + _random.NextDouble() * 5.000), 3); // 2.5% to 7.5%
            if (column.Name == "LTVRatio") return Math.Round((decimal)(0.60 + _random.NextDouble() * 0.35), 2); // 60% to 95%
            if (column.Name == "PropertyAppraisalValue") return Math.Round((decimal)(150000.00 + _random.NextDouble() * 1000000.00), 2);
            if (column.Name == "InsurancePremium") return Math.Round((decimal)(50.00 + _random.NextDouble() * 1000.00), 2);
            if (column.Name == "ProcessingTimeMinutes") return _random.Next(1, 120);
            if (column.Name == "ComplianceScore") return Math.Round((decimal)(70.00 + _random.NextDouble() * 30.00), 2);
            if (column.Name == "RiskRating") return _random.Next(1, 5);
            if (column.Name == "AuditCount") return _random.Next(0, 5);
            if (column.Name == "AssociatedFees") return Math.Round((decimal)(100.00 + _random.NextDouble() * 5000.00), 2);
            if (column.Name == "EscrowBalance") return Math.Round((decimal)(0.00 + _random.NextDouble() * 2000.00), 2);
            if (column.Name == "PropertyTaxAmount") return Math.Round((decimal)(1000.00 + _random.NextDouble() * 5000.00), 2);
            if (column.Name == "DocumentHash_CRC32") return (long)_random.Next(int.MinValue, int.MaxValue); // Placeholder, real CRC32 is more complex
            if (column.Name == "DocumentScore") return _random.Next(1, 100);

            // Date/Datetime Columns
            if (column.ClrType == typeof(DateTime))
            {
                int daysOffset = _random.Next(-365 * 5, 0); // Dates within last 5 years
                return DateTime.UtcNow.AddDays(daysOffset).Date;
            }

            throw new IndexOutOfRangeException($"Unhandled column generation for ordinal {i}, name '{column.Name}'.");
        }

        // --- IDataReader and IDocumentMetadataGenerator Implementation Details ---

        // Performance Comment: FieldCount is a constant, ensuring immediate return
        // without computation, critical for performance-sensitive data readers.
        public int FieldCount => _schema.Count;

        // Performance Comment: For synthetic data, explicitly stating no DBNulls
        // avoids unnecessary checks or more complex logic, optimizing access paths.
        public bool IsDBNull(int i) => false;

        // Performance Comment: Directly accessing and assigning values from GetValue(i)
        // minimizes overhead compared to iterating or using less efficient methods.
        public int GetValues(object[] values)
        {
            for (int i = 0; i < _schema.Count; i++)
            {
                values[i] = GetValue(i);
            }
            return _schema.Count;
        }

        // Minimal required implementation primarily for SqlBulkCopy
        // Performance Comment: Direct lookup from the schema list is efficient.
        public string GetName(int i) => _schema[i].Name;
        // Performance Comment: Directly maps to GetFieldType, efficiently providing metadata.
        public string GetDataTypeName(int i) => _schema[i].ClrType.Name;
        // Performance Comment: Provides direct type information without complex runtime checks.
        public Type GetFieldType(int i) => _schema[i].ClrType;

        // Other IDataReader members (mostly unused by SqlBulkCopy, minimal implementation for completeness)
        public void Close() { }
        public int Depth => 0;
        public DataTable GetSchemaTable() => throw new NotSupportedException("Schema table is not supported for this data generator.");
        public bool IsClosed => _currentIndex > _recordsToGenerate;
        public bool NextResult() => false; // Only one result set is generated
        public int RecordsAffected => (int)_currentIndex;

        public object this[string name] => GetValue(GetOrdinal(name)); // Provides indexed access for convenience
        public object this[int i] => GetValue(i); // Provides indexed access for convenience

        public void Dispose() { } // No unmanaged resources
        public bool GetBoolean(int i) => (bool)GetValue(i);
        public byte GetByte(int i) => (byte)GetValue(i);
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotSupportedException("Byte array retrieval not supported directly.");
        public char GetChar(int i) => (char)GetValue(i);
        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => throw new NotSupportedException("Char array retrieval not supported directly.");
        public IDataReader GetData(int i) => throw new NotSupportedException("Nested data readers not supported for this generator.");
        public string GetString(int i) => GetValue(i).ToString();
        public decimal GetDecimal(int i) => (decimal)GetValue(i);
        public DateTime GetDateTime(int i) => (DateTime)GetValue(i);
        public double GetDouble(int i) => (double)GetValue(i);
        public float GetFloat(int i) => (float)GetValue(i);
        public Guid GetGuid(int i) => (Guid)GetValue(i); // Assuming GUID generation if needed
        public short GetInt16(int i) => (short)GetValue(i);
        public int GetInt32(int i) => (int)GetValue(i);
        public long GetInt64(int i) => (long)GetValue(i); // Performance Comment: Directly casts GetValue(i) for efficient access.
        public int GetOrdinal(string name)
        {
            for (int i = 0; i < _schema.Count; i++)
            {
                if (_schema[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            throw new IndexOutOfRangeException($"Column '{name}' not found in schema.");
        }

        // Helper for generating random words/strings
        private string GenerateRandomWord()
        {
            var words = new[] { "Mortgage", "Loan", "Application", "Approval", "Document", "Scan", "Agreement", "Policy", "Statement" };
            return words[_random.Next(words.Length)];
        }

        // Helper for generating a simplified MD5-like hash fragment
        private string GenerateMd5HashFragment()
        {
            byte[] buffer = new byte[16]; // 16 bytes = 32 hex chars
            _random.NextBytes(buffer);
            return BitConverter.ToString(buffer).Replace("-", "").ToLowerInvariant();
        }
    }
}
