using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace RoMars.Test.StreamingDataAPI
{

    /// <summary>
    /// Static utility class for bulk-seeding the Products table.
    /// This uses SqlBulkCopy combined with a custom IDataReader for zero-allocation
    /// streaming of data from C# to SQL Server, achieving massive insertion performance.
    /// </summary>
    public static partial class Seeder
    {
        // Total number of records to be generated and inserted. (1 Billion)
        private const long RecordCount = 10_000_000;

        // Size of the batch for SqlBulkCopy operation. A larger batch reduces network round trips.
        private const int BatchSize = 1_000_000;

        /// <summary>
        /// Executes the bulk copy operation to seed the Products table with 1 billion records.
        /// </summary>
        /// <param name="connectionString">The connection string for the database.</param>
        /// <param name="logger">The logger instance for diagnostics.</param>
        public static async Task SeedProductsAsync(string connectionString, ILogger logger)
        {
            logger.LogInformation("Starting database seeding of {Count:N0} records. This will take significant time and resources.", RecordCount);

            // Ensure the table exists before attempting bulk copy
            await EnsureTableExistsAsync(connectionString, logger);

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var bulkCopyTimer = Stopwatch.StartNew();

            try
            {
                 using (var bulkCopy = new SqlBulkCopy(connection))
                {
                    bulkCopy.DestinationTableName = "Products";
                    bulkCopy.BatchSize = BatchSize;
                    bulkCopy.NotifyAfter = BatchSize; // Log progress every 10 million rows

                    bulkCopy.SqlRowsCopied += (sender, e) =>
                    {
                        logger.LogInformation("Seeding progress: {RowsCopied:N0} rows copied.", e.RowsCopied);
                    };

                    // The custom IDataReader streams the data directly, preventing large memory allocation.
                    using var reader = new ProductDataReader(RecordCount);

                    logger.LogTrace("Starting SqlBulkCopy write to server...");
                    await bulkCopy.WriteToServerAsync(reader);
                    logger.LogTrace("SqlBulkCopy finished writing to server.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SqlBulkCopy failed during seeding.");
            }

            bulkCopyTimer.Stop();
            logger.LogInformation("Database seeding complete. Total time: {Elapsed}", bulkCopyTimer.Elapsed);
        }

        /// <summary>
        /// Creates the Products table if it does not already exist.
        /// </summary>
        private static async Task EnsureTableExistsAsync(string connectionString, ILogger logger)
        {
            const string createTableSql = @"
            IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL 
                DROP TABLE dbo.Products;
            CREATE TABLE dbo.Products (
                Id INT PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL,
                Price DECIMAL(18, 2) NOT NULL
            );";

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand(createTableSql, connection);

            logger.LogInformation("Ensuring Products table is created/recreated.");
            await command.ExecuteNonQueryAsync();
        }
    }
}