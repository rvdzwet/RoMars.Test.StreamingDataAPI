using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Diagnostics; // Required for Stopwatch
using System.Threading.Tasks;

namespace RoMars.Test.StreamingDataAPI
{
    /// <summary>
    /// Manages the creation and recreation of the Products table within the database.
    /// This class adheres to the Single Responsibility Principle by encapsulating
    /// all table schema management responsibilities. It implements IProductTableManager,
    /// promoting the Dependency Inversion Principle and allowing for different
    /// table management strategies (e.g., migrations) to be plugged in.
    /// </summary>
    public class ProductTableManager : IProductTableManager
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<ProductTableManager> _logger;

        public ProductTableManager(IDbConnectionFactory connectionFactory, ILogger<ProductTableManager> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        /// <summary>
        /// Creates the Products table and an index on the Price column if they do not already exist.
        /// Performance Comment: The creation of a non-clustered index on 'Price' is crucial
        /// for the `SELECT TOP 100 ... WHERE Price > @MinPrice ORDER BY Price` query.
        /// This index allows SQL Server to efficiently locate rows matching the price condition
        /// and order them without scanning the entire table, drastically improving query performance.
        /// Without an index, this operation would scale linearly with table size, leading to
        /// significant performance degradation on large datasets.
        /// </summary>
        public async Task EnsureTableExistsAsync()
        {
            const string createTableAndIndexSql = @"
             -- 1. DROP TABLE if exists
             IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL
                 DROP TABLE dbo.Products;

             -- 2. CREATE TABLE
             CREATE TABLE dbo.Products (
                 Id BIGINT PRIMARY KEY, -- Changed to BIGINT to accommodate large record counts
                 Name NVARCHAR(100) NOT NULL,
                 Price DECIMAL(18, 2) NOT NULL
             );

             -- 3. ADD INDEX ON PRICE
             CREATE NONCLUSTERED INDEX IX_Products_Price
             ON dbo.Products (Price);
             ";

            using var connection = (SqlConnection)_connectionFactory.CreateConnection(); // Cast to SqlConnection to create SqlCommand
            await connection.OpenAsync();
            using var command = new SqlCommand(createTableAndIndexSql, connection);

            _logger.LogInformation("Ensuring Products table is created/recreated and indexed. ThreadID: {ThreadId}", Environment.CurrentManagedThreadId);
            var timer = Stopwatch.StartNew();
            await command.ExecuteNonQueryAsync();
            timer.Stop();
            _logger.LogTrace("Products table schema ensured in {ElapsedMs}ms. ThreadID: {ThreadId}", timer.Elapsed.TotalMilliseconds, Environment.CurrentManagedThreadId);
        }
    }
}
