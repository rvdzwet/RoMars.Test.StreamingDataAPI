using System.Data; // Required for SqlDbType

namespace RoMars.StreamingJsonOutput.Host
{
    /// <summary>
    /// Defines the static schema for the DocumentMetadata table, including all 105 columns.
    /// This centralizes column definitions, ensuring consistency across data generation,
    /// table creation, querying, and JSON streaming. It adheres to the Single Responsibility
    /// Principle by solely managing the schema definition.
    /// </summary>
    public static class DocumentMetadataSchema
    {
        public class ColumnInfo
        {
            public string Name { get; }
            public Type ClrType { get; }
            public SqlDbType SqlType { get; }
            public int Length { get; } // For NVARCHAR columns
            public int Precision { get; } // For DECIMAL columns
            public int Scale { get; } // For DECIMAL columns

            public ColumnInfo(string name, Type clrType, SqlDbType sqlType, int length = 0, int precision = 0, int scale = 0)
            {
                Name = name;
                ClrType = clrType;
                SqlType = sqlType;
                Length = length;
                Precision = precision;
                Scale = scale;
            }
        }

        private static readonly List<ColumnInfo> _columns;
        public static IReadOnlyList<ColumnInfo> Columns => _columns;
        public static int TotalColumnCount => _columns.Count;

        static DocumentMetadataSchema()
        {
            _columns = new List<ColumnInfo>
            {
                // Existing Core Columns (3)
                new ColumnInfo("DocumentId", typeof(long), SqlDbType.BigInt),
                new ColumnInfo("DocumentTitle", typeof(string), SqlDbType.NVarChar, length: 255),
                new ColumnInfo("MortgageAmount", typeof(decimal), SqlDbType.Decimal, precision: 18, scale: 2),

                // Text/String Columns (60 total including DocumentType, FileType, etc.)
                new ColumnInfo("DocumentType", typeof(string), SqlDbType.NVarChar, length: 100),
                new ColumnInfo("FileType", typeof(string), SqlDbType.NVarChar, length: 50),
                new ColumnInfo("CustomerName", typeof(string), SqlDbType.NVarChar, length: 255),
                new ColumnInfo("CustomerAddress_Line1", typeof(string), SqlDbType.NVarChar, length: 255),
                new ColumnInfo("CustomerAddress_Line2", typeof(string), SqlDbType.NVarChar, length: 255),
                new ColumnInfo("CustomerCity", typeof(string), SqlDbType.NVarChar, length: 100),
                new ColumnInfo("CustomerState", typeof(string), SqlDbType.NVarChar, length: 50),
                new ColumnInfo("CustomerZip", typeof(string), SqlDbType.NVarChar, length: 20),
                new ColumnInfo("LoanNumber", typeof(string), SqlDbType.NVarChar, length: 50),
                new ColumnInfo("PropertyAddress_Street", typeof(string), SqlDbType.NVarChar, length: 255),
                new ColumnInfo("PropertyAddress_City", typeof(string), SqlDbType.NVarChar, length: 100),
                new ColumnInfo("PropertyAddress_State", typeof(string), SqlDbType.NVarChar, length: 50),
                new ColumnInfo("PropertyAddress_Zip", typeof(string), SqlDbType.NVarChar, length: 20),
                new ColumnInfo("OriginalFilename", typeof(string), SqlDbType.NVarChar, length: 255),
                new ColumnInfo("SourceSystem", typeof(string), SqlDbType.NVarChar, length: 100),
                new ColumnInfo("WorkflowStatus", typeof(string), SqlDbType.NVarChar, length: 50),
                new ColumnInfo("ReviewerName", typeof(string), SqlDbType.NVarChar, length: 100),
            };

            // Add Tags (20)
            for (int i = 1; i <= 20; i++)
            {
                _columns.Add(new ColumnInfo($"Tag_{i:D2}", typeof(string), SqlDbType.NVarChar, length: 100));
            }

            // Add Comments (30) - Use NVARCHAR(MAX) for potentially longer text
            for (int i = 1; i <= 30; i++)
            {
                _columns.Add(new ColumnInfo($"Comment_{i:D2}", typeof(string), SqlDbType.NVarChar, length: 4000)); // Use 4000 for NVarChar(MAX) simulation
            }

            // Numeric Columns (20)
            _columns.AddRange(new[]
            {
                new ColumnInfo("PageCount", typeof(int), SqlDbType.Int),
                new ColumnInfo("DocumentSizeKB", typeof(int), SqlDbType.Int),
                new ColumnInfo("VersionNumber", typeof(int), SqlDbType.Int),
                new ColumnInfo("RetentionYears", typeof(int), SqlDbType.Int),
                new ColumnInfo("CreditScore", typeof(int), SqlDbType.Int),
                new ColumnInfo("LoanTermMonths", typeof(int), SqlDbType.Int),
                new ColumnInfo("InterestRate", typeof(decimal), SqlDbType.Decimal, precision: 5, scale: 3),
                new ColumnInfo("LTVRatio", typeof(decimal), SqlDbType.Decimal, precision: 5, scale: 2),
                new ColumnInfo("PropertyAppraisalValue", typeof(decimal), SqlDbType.Decimal, precision: 18, scale: 2),
                new ColumnInfo("InsurancePremium", typeof(decimal), SqlDbType.Decimal, precision: 10, scale: 2),
                new ColumnInfo("ProcessingTimeMinutes", typeof(int), SqlDbType.Int),
                new ColumnInfo("ComplianceScore", typeof(decimal), SqlDbType.Decimal, precision: 5, scale: 2),
                new ColumnInfo("RiskRating", typeof(int), SqlDbType.Int),
                new ColumnInfo("AuditCount", typeof(int), SqlDbType.Int),
                new ColumnInfo("AssociatedFees", typeof(decimal), SqlDbType.Decimal, precision: 18, scale: 2),
                new ColumnInfo("EscrowBalance", typeof(decimal), SqlDbType.Decimal, precision: 18, scale: 2),
                new ColumnInfo("PropertyTaxAmount", typeof(decimal), SqlDbType.Decimal, precision: 18, scale: 2),
            });
             // Add a few more numeric columns to reach 20 total (+3 core, +17 string (basic), +20 tags, +30 comments => 70 existing, 3 core + 50 string + 30 numeric + 22 datetime = 105)
            _columns.Add(new ColumnInfo("DocumentHash_CRC32", typeof(long), SqlDbType.BigInt)); // Changed to long
            _columns.Add(new ColumnInfo("DocumentHash_MD5", typeof(string), SqlDbType.NVarChar, length: 32));
            _columns.Add(new ColumnInfo("DocumentScore", typeof(int), SqlDbType.Int));

            // Date/Datetime Columns (12)
            _columns.AddRange(new[]
            {
                new ColumnInfo("CreationDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("LastModifiedDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("ReviewDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("ApprovalDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("ExpirationDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("OriginalUploadDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("LastAccessedDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("RetentionEndDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("NextReviewDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("FundingDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("DisbursementDate", typeof(DateTime), SqlDbType.DateTime2),
                new ColumnInfo("ClosingDate", typeof(DateTime), SqlDbType.DateTime2),
            });
            // Total should be 3 (core) + 17 (basic string) + 20 (tags) + 30 (comments) + 20 (numeric) + 12 (date) = 102.
            // Oh, I need 105 columns in total. So the additional 102 makes it 105. My math was off.
            // I had 3 core + 17 basic string + 20 tags + 30 comments + 20 numeric + 12 date = 102.
            // That totals 102 *additional* columns + 3 core = 105. The list must be correct now.

            // The original plan for "Date/Datetime Columns (approx. 22)" vs current list (12)
            // Let me adjust the string columns slightly to hit that 105 exactly.
            // Current count: 3 (core) + 17 (basic string) + 20 (tags) + 30 (comments) + 20 (numeric) + 12 (date) = 102.
            // I need to add 3 more columns. I'll add them as DateTime for "effective dates"
            _columns.Add(new ColumnInfo($"EffectiveDate_01", typeof(DateTime), SqlDbType.DateTime2));
            _columns.Add(new ColumnInfo($"EffectiveDate_02", typeof(DateTime), SqlDbType.DateTime2));
            _columns.Add(new ColumnInfo($"EffectiveDate_03", typeof(DateTime), SqlDbType.DateTime2));
            // New Total: 105 columns.
        }

        public static ColumnInfo GetColumnInfo(int ordinal)
        {
            if (ordinal < 0 || ordinal >= _columns.Count)
            {
                throw new IndexOutOfRangeException($"Column ordinal {ordinal} is out of range. Total columns: {_columns.Count}");
            }
            return _columns[ordinal];
        }

        public static ColumnInfo GetColumnInfo(string name)
        {
            var column = _columns.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (column == null)
            {
                throw new ArgumentException($"Column '{name}' not found in schema.");
            }
            return column;
        }

        public static int GetOrdinal(string name)
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            throw new ArgumentException($"Column '{name}' not found in schema.");
        }
    }
}
