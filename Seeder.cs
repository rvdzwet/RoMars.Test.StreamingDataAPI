using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data; // Required for IDataReader
using System.Diagnostics;

namespace RoMars.Test.StreamingDataAPI
{
    /// <summary>
    /// Handles the bulk-seeding process for the Products table.
    /// This class uses SqlBulkCopy combined with a custom IProductDataGenerator (IDataReader)
    /// for zero-allocation streaming of data from C# to SQL Server, achieving massive insertion performance.
    /// </summary>
    /// <remarks>
    /// This class adheres to the Single Responsibility Principle by focusing solely on
    /// seeding the database. It depends on abstractions (IProductDataGenerator, IProductTableManager, IDbConnectionFactory)
    /// which promotes the Dependency Inversion Principle and Open/Closed Principle.
    /// </remarks>
    public class Seeder
    {
        private readonly IProductDataGenerator _dataGenerator;
        private readonly IProductTableManager _tableManager;
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<Seeder> _logger;
        private readonly long _recordCount;
        private const int BatchSize = 1_000_000; // Performance Comment: A larger batch reduces
                                                // network round trips and SQL Server overhead
                                                // during bulk copy operations.

        public Seeder(ILogger<Seeder> logger,
                      IProductDataGenerator dataGenerator,
                      IProductTableManager tableManager,
                      IDbConnectionFactory connectionFactory,
                      long recordCount = 10_000_000) // Default to 10 million for seeding
        {
            _logger = logger;
            _dataGenerator = dataGenerator;
            _tableManager = tableManager;
            _connectionFactory = connectionFactory;
            _recordCount = recordCount;
        }

        /// <summary>
        /// Executes the bulk copy operation to seed the Products table.
        /// Performance Comment: SqlBulkCopy is highly optimized for inserting large
        /// amounts of data into SQL Server. It bypasses the traditional INSERT
        /// statement overhead, writing directly to the database.
        /// </summary>
        public async Task SeedProductsAsync()
        {
            _logger.LogInformation("Starting database seeding of {Count:N0} records. This will take significant time and resources. ThreadID: {ThreadId}",
                                  _recordCount, Environment.CurrentManagedThreadId);

            await _tableManager.EnsureTableExistsAsync();

            using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync();

            var bulkCopyTimer = Stopwatch.StartNew();

            try
            {
                using (var bulkCopy = new SqlBulkCopy((SqlConnection)connection)) // Cast to SqlConnection for SqlBulkCopy
                {
                    bulkCopy.DestinationTableName = "Products";
                    bulkCopy.BatchSize = BatchSize;
                    bulkCopy.NotifyAfter = BatchSize; // Log progress every batch

                    bulkCopy.SqlRowsCopied += (sender, e) =>
                    {
                        var elapsed = bulkCopyTimer.Elapsed.TotalMilliseconds;
                        _logger.LogInformation("Seeding progress: {RowsCopied:N0} rows copied. Elapsed: {ElapsedMs}ms",
                            e.RowsCopied, elapsed);
                    };

                    // Performance Comment: Using a custom IDataReader (IProductDataGenerator)
                    // with SqlBulkCopy prevents loading all data into memory at once.
                    // Data is streamed directly from the generator to SQL Server,
                    // resulting in zero-allocation and massive performance gains for large datasets.
                    await bulkCopy.WriteToServerAsync((IDataReader)_dataGenerator);
                    _logger.LogTrace("SqlBulkCopy finished writing to server.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SqlBulkCopy failed during seeding. ThreadID: {ThreadId}", Environment.CurrentManagedThreadId);
                throw; // Re-throw to indicate a critical startup failure
            }
            finally
            {
                bulkCopyTimer.Stop();
                _logger.LogInformation("Database seeding complete. Total time: {Elapsed}. Seeded {Count:N0} records. ThreadID: {ThreadId}",
                                      bulkCopyTimer.Elapsed, _recordCount, Environment.CurrentManagedThreadId);
            }
        }
    }
}
