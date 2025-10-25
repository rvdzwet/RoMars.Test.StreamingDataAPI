using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Diagnostics;
using System.Data.Common; // Recommended for general DbType usage

namespace RoMars.Test.StreamingDataAPI
{
    /// <summary>
    /// Handles highly optimized, unbuffered data retrieval directly from the database.
    /// Includes retry logic for transient connection failures (K8s resilience).
    /// </summary>
    public class ProductRepository
    {
        // SQL query now uses a parameter placeholder: @MinPrice
        private const string SelectSql = "SELECT TOP 100 Id, Name, Price FROM Products WHERE Price > @MinPrice ORDER BY Price";

        private readonly string _connectionString;
        private readonly ILogger<ProductRepository> _logger;
        private const int MaxConnectionRetries = 3;
        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(200);

        public ProductRepository(string connectionString, ILogger<ProductRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// Executes the streaming query asynchronously, including resilience logic for connection attempts.
        /// </summary>
        public async Task<(SqlConnection Connection, SqlDataReader Reader)> ExecuteStreamingQueryAsync(
            decimal minPrice, // 🚨 New parameter for the WHERE clause
            CancellationToken cancellationToken = default)
        {
            var totalTimer = Stopwatch.StartNew();
            _logger.LogInformation("Streaming query operation initiated by thread ID: {ThreadId} for Price > {MinPrice}.",
                Environment.CurrentManagedThreadId, minPrice);

            SqlConnection? connection = null;
            int attempt = 0;

            while (attempt < MaxConnectionRetries)
            {
                try
                {
                    attempt++;
                    connection = new SqlConnection(_connectionString);

                    var connectionOpenTimer = Stopwatch.StartNew();
                    // 1. Connection Pooling: Attempt to get a connection from the pool.
                    await connection.OpenAsync(cancellationToken);
                    connectionOpenTimer.Stop();

                    _logger.LogTrace("Connection obtained and opened on attempt {Attempt} in {ElapsedMs}ms.",
                        attempt, connectionOpenTimer.Elapsed.TotalMilliseconds);

                    // 2. Command Setup
                    using var command = new SqlCommand(SelectSql, connection) { CommandTimeout = 60 };

                    // 🚨 PARAMETERIZATION: Add the parameter to the command
                    var priceParam = command.Parameters.Add("@MinPrice", SqlDbType.Decimal);
                    priceParam.Value = minPrice;
                    // Optional: Set precision and scale if known and required for your data type
                    priceParam.Precision = 18;
                    priceParam.Scale = 2;

                    // 🚨 PREPARED STATEMENT: Set Prepare() to enable parameterization optimization.
                    // This sends the query plan to the server once.
                    await command.PrepareAsync(cancellationToken);


                    // VITAL: CommandBehavior settings for streaming performance and resource disposal.
                    var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.CloseConnection | CommandBehavior.SequentialAccess,
                        cancellationToken);

                    totalTimer.Stop();
                    _logger.LogTrace("Query execution setup finished. Total setup time: {TotalMs}ms.",
                        totalTimer.Elapsed.TotalMilliseconds);

                    return (connection, reader);
                }
                catch (SqlException ex) when (ex.IsTransient())
                {
                    // Transient error: Log and retry (e.g., brief network interruption, dead connection in pool)
                    connection?.Dispose(); // Dispose the failed connection attempt

                    if (attempt < MaxConnectionRetries)
                    {
                        var delay = InitialRetryDelay * Math.Pow(2, attempt - 1);
                        _logger.LogWarning("Transient SQL error on attempt {Attempt}/{MaxRetries}. Retrying in {DelayMs}ms. Error: {Message}",
                            attempt, MaxConnectionRetries, delay.TotalMilliseconds, ex.Message);
                        await Task.Delay(delay, cancellationToken);
                    }
                    else
                    {
                        // If max retries reached, re-throw the permanent failure
                        _logger.LogError(ex, "Failed to open connection after {MaxRetries} attempts.", MaxConnectionRetries);
                        throw;
                    }
                }
                catch // Handle non-SQL exceptions gracefully
                {
                    connection?.Dispose();
                    throw;
                }
            }
            // Should be unreachable, but required for compiler
            throw new InvalidOperationException("Failed to establish database connection.");
        }
    }
}