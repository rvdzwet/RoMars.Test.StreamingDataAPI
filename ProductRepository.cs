using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common; // For DbConnection, important for IDbConnectionFactory
using System.Diagnostics;

namespace RoMars.Test.StreamingDataAPI
{
    /// <summary>
    /// Handles highly optimized, unbuffered data retrieval directly from the database using SQL Server specific features.
    /// Includes retry logic for transient connection failures (K8s resilience).
    /// This adheres to the Single Responsibility Principle by focusing solely on data access.
    /// It uses IDbConnectionFactory (Dependency Inversion) for flexible connection management.
    /// </summary>
    public class ProductRepository
    {
        // SQL query now uses a parameter placeholder: @MinPrice
        private const string SelectSql = "SELECT TOP 100 Id, Name, Price FROM Products WHERE Price > @MinPrice ORDER BY Price";

        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<ProductRepository> _logger;
        private const int MaxConnectionRetries = 3;
        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(200);

        public ProductRepository(IDbConnectionFactory connectionFactory, ILogger<ProductRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        /// <summary>
        /// Executes the streaming query asynchronously, including resilience logic for connection attempts.
        /// Performance Comment: This method is designed for extreme performance and resource efficiency.
        /// Key optimizations include:
        /// 1. Connection Pooling: `_connectionFactory.CreateConnection()` combined with `using` blocks
        ///    ensures connections are returned to the pool efficiently, minimizing connection overhead.
        /// 2. Parameterized Queries (`@MinPrice`): Prevents SQL injection and allows SQL Server
        ///    to reuse query plans, which is more efficient than string concatenation.
        /// 3. Prepared Statements (`command.PrepareAsync()`): Explicitly tells SQL Server to compile
        ///    and cache the query plan for reuse, reducing overhead on subsequent executions.
        /// 4. `CommandBehavior.SequentialAccess`: Essential for large results. It tells the reader
        ///    to retrieve data sequentially, avoiding loading entire rows into memory. This is
        ///    critical for streaming and zero-allocation processing.
        /// 5. `CommandBehavior.CloseConnection`: Ensures the `SqlConnection` is closed (and returned to pool)
        ///    as soon as the `SqlDataReader` is closed, preventing resource leaks.
        /// 6. Retry Logic: Handles transient network or database errors, improving application resilience
        ///    in distributed or cloud environments without user intervention. Exponential backoff
        ///    (`InitialRetryDelay * Math.Pow(2, attempt - 1)`) prevents overwhelming the database.
        /// </summary>
        public async Task<(DbConnection Connection, SqlDataReader Reader)> ExecuteStreamingQueryAsync(
            decimal minPrice,
            CancellationToken cancellationToken = default)
        {
            var totalTimer = Stopwatch.StartNew();
            _logger.LogInformation("Streaming query operation initiated by Thread ID: {ThreadId} for Price > {MinPrice}.",
                Environment.CurrentManagedThreadId, minPrice);

            DbConnection? connection = null;
            int attempt = 0;

            while (attempt < MaxConnectionRetries)
            {
                try
                {
                    attempt++;
                    connection = _connectionFactory.CreateConnection();
                    
                    var connectionOpenTimer = Stopwatch.StartNew();
                    await connection.OpenAsync(cancellationToken);
                    connectionOpenTimer.Stop();

                    _logger.LogTrace("Connection obtained and opened on attempt {Attempt} in {ElapsedMs}ms. Thread ID: {ThreadId}",
                        attempt, connectionOpenTimer.Elapsed.TotalMilliseconds, Environment.CurrentManagedThreadId);

                    using var command = (SqlCommand)connection.CreateCommand(); // Cast to SqlCommand for specific features
                    command.CommandText = SelectSql;
                    command.CommandTimeout = 60;

                    var priceParam = command.Parameters.Add("@MinPrice", SqlDbType.Decimal);
                    priceParam.Value = minPrice;
                    priceParam.Precision = 18;
                    priceParam.Scale = 2;

                    await command.PrepareAsync(cancellationToken);

                    var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.CloseConnection | CommandBehavior.SequentialAccess,
                        cancellationToken);

                    totalTimer.Stop();
                    _logger.LogTrace("Query execution setup finished in {TotalMs}ms. Thread ID: {ThreadId}",
                        totalTimer.Elapsed.TotalMilliseconds, Environment.CurrentManagedThreadId);

                    return (connection, reader);
                }
                catch (SqlException ex) when (ex.IsTransient())
                {
                    connection?.Dispose(); // Dispose the failed connection attempt

                    if (attempt < MaxConnectionRetries)
                    {
                        var delay = InitialRetryDelay * Math.Pow(2, attempt - 1);
                        _logger.LogWarning("Transient SQL error on attempt {Attempt}/{MaxRetries}. Retrying in {DelayMs}ms. Error: {Message}. Thread ID: {ThreadId}",
                            attempt, MaxConnectionRetries, delay.TotalMilliseconds, ex.Message, Environment.CurrentManagedThreadId);
                        await Task.Delay(delay, cancellationToken);
                    }
                    else
                    {
                        _logger.LogError(ex, "Failed to open connection after {MaxRetries} attempts. Thread ID: {ThreadId}", MaxConnectionRetries, Environment.CurrentManagedThreadId);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    connection?.Dispose();
                    _logger.LogError(ex, "Non-transient error during streaming query. Thread ID: {ThreadId}", Environment.CurrentManagedThreadId);
                    throw;
                }
            }
            throw new InvalidOperationException("Failed to establish database connection after multiple retries.");
        }
    }
}
