using Microsoft.Data.SqlClient;
using RoMars.StreamingJsonOutput.Framework;
using RoMars.StreamingJsonOutput.Host.Extensions; // New Import
using System.Data; // Required for SqlDbType
using System.Diagnostics; // Required for Stopwatch
using System.Text; // For StringBuilder

namespace RoMars.StreamingJsonOutput.Host
{
    /// <summary>
    /// Manages the creation and recreation of the DocumentMetadata table within the database.
    /// This class now includes verbose logging for detailed operation tracking.
    /// </summary>
    public class DocumentMetadataTableManager : IDocumentMetadataTableManager
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<DocumentMetadataTableManager> _logger;

        public DocumentMetadataTableManager(IDbConnectionFactory connectionFactory, ILogger<DocumentMetadataTableManager> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
            // Using LoggerExtensions for consistent structured logging
            _logger.LogTableManagerInitialized();
        }

        /// <summary>
        /// Creates the DocumentMetadata table and relevant indexes if they do not already exist (idempotent operation).
        /// </summary>
        public async Task EnsureTableExistsAsync()
        {
            _logger.LogDebug("Starting table schema existence check.");

            var createTableSql = BuildCreateTableSql();
            var createIndexSql = BuildCreateIndexSql();

            _logger.LogTrace("Generated CREATE TABLE SQL: {Sql}", createTableSql.Trim());
            _logger.LogTrace("Generated CREATE INDEX SQL: {Sql}", createIndexSql.Trim());

            using var connection = (SqlConnection)_connectionFactory.CreateConnection();
            _logger.LogDebug("Created database connection object.");

            await connection.OpenAsync();
            _logger.LogDebug("Successfully opened connection to database. Connection String: {Connection}", connection.ConnectionString);

            _logger.LogInformation("Ensuring DocumentMetadata table schema and index exist. ThreadID: {ThreadId}", Environment.CurrentManagedThreadId);
            var timer = Stopwatch.StartNew();

            // 1. Ensure Table Exists
            using (var command = new SqlCommand(createTableSql, connection))
            {
                _logger.LogTrace(2000, "Executing command to ensure table existence...");
                int rowsAffected = await command.ExecuteNonQueryAsync();
                _logger.LogDebug(2001, "CREATE TABLE command executed. Rows affected (expected 0/1): {RowsAffected}", rowsAffected);
            }

            // 2. Ensure Index Exists
            using (var command = new SqlCommand(createIndexSql, connection))
            {
                _logger.LogTrace(2002, "Executing command to ensure index existence...");
                int rowsAffected = await command.ExecuteNonQueryAsync();
                _logger.LogDebug(2003, "CREATE INDEX command executed. Rows affected (expected 0/1): {RowsAffected}", rowsAffected);
            }

            timer.Stop();
            _logger.LogTrace(2004, "DocumentMetadata table schema ensured in {ElapsedMs}ms. ThreadID: {ThreadId}", timer.Elapsed.TotalMilliseconds, Environment.CurrentManagedThreadId);
            _logger.LogDebug(2005, "Finished table schema existence check.");
        }

        /// <summary>
        /// Gets the current number of records in the DocumentMetadata table.
        /// </summary>
        public async Task<long> GetCurrentRecordCountAsync()
        {
            _logger.LogDebug("Starting record count retrieval.");

            const string countSql = "SELECT COUNT(*) FROM dbo.DocumentMetadata;";
            _logger.LogTrace(2014, "COUNT SQL: {Sql}", countSql); // New EventId

            using var connection = _connectionFactory.CreateConnection();
            _logger.LogDebug(2015, "Created database connection for counting."); // New EventId

            await connection.OpenAsync();
            _logger.LogDebug(2016, "Successfully opened connection for counting."); // New EventId

            using var command = new SqlCommand(countSql, (SqlConnection)connection);

            var timer = Stopwatch.StartNew();
            _logger.LogTrace(2017, "Executing COUNT query..."); // New EventId

            object? result = await command.ExecuteScalarAsync();
            int count = (int?)result ?? 0; // Handle potential null and cast

            timer.Stop();

            _logger.LogTrace(2006, "Counted {Count:N0} records in DocumentMetadata table in {ElapsedMs}ms. ThreadID: {ThreadId}", count, timer.Elapsed.TotalMilliseconds, Environment.CurrentManagedThreadId);
            _logger.LogDebug(2007, "Finished record count retrieval. Total records: {Count:N0}", count);

            return count;
        }

        // --- Schema Generation Methods ---

        /// <summary>
        /// Dynamically builds the CREATE TABLE SQL statement wrapped in an existence check.
        /// FIX: This now uses 'IF OBJECT_ID(...) IS NULL' to prevent recreation.
        /// </summary>
        private string BuildCreateTableSql()
        {
            _logger.LogTrace("Building CREATE TABLE SQL statement...");
            var sb = new StringBuilder();

            // Correction: Only create the table IF it does not exist
            sb.AppendLine("IF OBJECT_ID('dbo.DocumentMetadata', 'U') IS NULL");
            sb.AppendLine("BEGIN");
            sb.AppendLine("CREATE TABLE dbo.DocumentMetadata (");

            int colCount = 0;
            foreach (var col in DocumentMetadataSchema.Columns)
            {
                string typeDefinition = GetSqlTypeDefinition(col);
                string constraints = (col.Name == "DocumentId" ? "PRIMARY KEY" : "NOT NULL");
                string colDefinition = $"{col.Name} {typeDefinition} {constraints}";
                sb.AppendLine($"    {colDefinition},");
                colCount++;
            }
            _logger.LogTrace("Added {Count} column definitions to CREATE TABLE statement.", colCount);

            // Remove trailing comma from the last column definition
            if (sb.Length > Environment.NewLine.Length + 1)
            {
                sb.Length -= Environment.NewLine.Length + 1; // Remove ",\n"
            }
            sb.AppendLine();
            sb.AppendLine(");");
            sb.AppendLine("END"); // End of the IF block

            return sb.ToString();
        }

        /// <summary>
        /// Dynamically builds the CREATE INDEX SQL statement, wrapped in an existence check.
        /// FIX: This prevents trying to create an index that already exists.
        /// </summary>
        private string BuildCreateIndexSql()
        {
            _logger.LogTrace("Building CREATE INDEX SQL statement...");
            // Check if the index exists before creating it
            return @"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentMetadata_MortgageAmount' AND object_id = OBJECT_ID('dbo.DocumentMetadata'))
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_DocumentMetadata_MortgageAmount ON dbo.DocumentMetadata (MortgageAmount);
                END;
            ";
        }

        /// <summary>
        /// Converts ColumnInfo to its SQL type definition string.
        /// </summary>
        private string GetSqlTypeDefinition(DocumentMetadataSchema.ColumnInfo col)
        {
            switch (col.SqlType)
            {
                case SqlDbType.NVarChar:
                    string def = $"NVARCHAR({(col.Length == 0 ? "MAX" : col.Length.ToString())})";
                    _logger.LogTrace(2008, "Mapping {Name} (NVarChar) to {Definition}", col.Name, def);
                    return def;
                case SqlDbType.Decimal:
                    def = $"DECIMAL({col.Precision}, {col.Scale})";
                    _logger.LogTrace(2009, "Mapping {Name} (Decimal) to {Definition}", col.Name, def);
                    return def;
                case SqlDbType.Int:
                    _logger.LogTrace(2010, "Mapping {Name} (Int) to INT", col.Name);
                    return "INT";
                case SqlDbType.BigInt:
                    _logger.LogTrace(2011, "Mapping {Name} (BigInt) to BIGINT", col.Name);
                    return "BIGINT";
                case SqlDbType.DateTime2:
                    _logger.LogTrace(2012, "Mapping {Name} (DateTime2) to DATETIME2", col.Name);
                    return "DATETIME2";
                default:
                    _logger.LogError(2013, "Unsupported SQL Type: {SqlType} for column {ColName}", col.SqlType, col.Name);
                    throw new NotSupportedException($"SQL Type {col.SqlType} not supported for schema generation.");
            }
        }
    }
}
