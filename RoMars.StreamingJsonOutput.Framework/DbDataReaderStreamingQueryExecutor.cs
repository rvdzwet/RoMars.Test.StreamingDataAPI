using System.Data.Common;
using System.Data;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Microsoft.Data.SqlClient; // For SqlExceptionExtensions and concrete SqlDataReader

namespace RoMars.StreamingJsonOutput.Framework
{
    /// <summary>
    /// Concrete implementation of <see cref="IStreamingQueryExecutor"/> that executes
    /// streaming database queries using a <see cref="DbConnection"/>. It incorporates
    /// connection pooling, parameterized queries, sequential access, and retry logic
    /// for transient failures, optimized for high-performance data streaming.
    /// </summary>
    public class DbDataReaderStreamingQueryExecutor : IStreamingQueryExecutor
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<DbDataReaderStreamingQueryExecutor> _logger;
        private const int MaxConnectionRetries = 3;
        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(200);

        public DbDataReaderStreamingQueryExecutor(IDbConnectionFactory connectionFactory, ILogger<DbDataReaderStreamingQueryExecutor> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes a database query designed for streaming results.
        /// </summary>
        /// <param name="query">The SQL query to execute.</param>
        /// <param name="parameters">An array of DbParameter objects for the query.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <param name="commandTimeout">The command timeout in seconds. Defaults to 60.</param>
        /// <returns>
        /// A tuple containing an opened DbConnection and an active DbDataReader.
        /// It is the caller's responsibility to dispose of both the connection and reader.
        /// </returns>
        public async Task<(DbConnection Connection, DbDataReader Reader)> ExecuteStreamingQueryAsync(
            string query,
            DbParameter[]? parameters = null,
            CancellationToken cancellationToken = default,
            int commandTimeout = 60)
        {
            var totalTimer = Stopwatch.StartNew();
            _logger.LogInformation("Streaming database query initiated by Thread ID: {ThreadId} with query: {Query}",
                Environment.CurrentManagedThreadId, query);

            DbConnection? connection = null;
            int attempt = 0;

            while (attempt < MaxConnectionRetries)
            {
                SqlException? sqlException = null; // Store specific SQL exceptions for checking IsTransient

                try
                {
                    attempt++;
                    connection = _connectionFactory.CreateConnection();
                    
                    var connectionOpenTimer = Stopwatch.StartNew();
                    await connection.OpenAsync(cancellationToken);
                    connectionOpenTimer.Stop();

                    _logger.LogTrace("Connection obtained and opened on attempt {Attempt} in {ElapsedMs}ms. Thread ID: {ThreadId}",
                        attempt, connectionOpenTimer.Elapsed.TotalMilliseconds, Environment.CurrentManagedThreadId);

                    using var command = connection.CreateCommand();
                    command.CommandText = query;
                    command.CommandTimeout = commandTimeout;

                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    // PrepareAsync is SQL Server specific optimization, but can be removed for full DbCommand compatibility if needed.
                    // For now, assume a compatible provider (like SqlClient) is used, matching the original context.
                    // If connection is actually a SqlConnection, then cast and use PrepareAsync.
                    // Otherwise, skip PrepareAsync for generic DbCommand.
                    if (command is SqlCommand sqlCommand)
                    {
                        await sqlCommand.PrepareAsync(cancellationToken);
                    }
                    else
                    {
                        // Some DbCommand implementations might not support Prepare, or it might be a no-op.
                        // Log a warning if PrepareAsync is not used due to non-SqlCommand type.
                        _logger.LogDebug("Command is not a SqlCommand; skipping PrepareAsync for generic DbCommand.");
                    }

                    var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.CloseConnection | CommandBehavior.SequentialAccess,
                        cancellationToken);

                    totalTimer.Stop();
                    _logger.LogTrace("Database query execution setup finished in {TotalMs}ms. Thread ID: {ThreadId}",
                        totalTimer.Elapsed.TotalMilliseconds, Environment.CurrentManagedThreadId);

                    return (connection, reader);
                }
                catch (DbException ex) // Catch DbException for provider-agnostic handling
                {
                    connection?.Dispose(); // Dispose the failed connection attempt
                    
                    if (ex is SqlException sqlEx)
                    {
                        sqlException = sqlEx;
                    }

                    if (sqlException?.IsTransient() == true && attempt < MaxConnectionRetries)
                    {
                        var delay = InitialRetryDelay * Math.Pow(2, attempt - 1);
                        _logger.LogWarning("Transient database error on attempt {Attempt}/{MaxRetries}. Retrying in {DelayMs}ms. Error: {Message}. Thread ID: {ThreadId}",
                            attempt, MaxConnectionRetries, delay.TotalMilliseconds, ex.Message, Environment.CurrentManagedThreadId);
                        await Task.Delay(delay, cancellationToken);
                    }
                    else
                    {
                        _logger.LogError(ex, "Non-transient or retry limit reached during streaming database query. Thread ID: {ThreadId}", Environment.CurrentManagedThreadId);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    connection?.Dispose();
                    _logger.LogError(ex, "Unexpected error during streaming database query. Thread ID: {ThreadId}", Environment.CurrentManagedThreadId);
                    throw;
                }
            }
            throw new InvalidOperationException("Failed to establish database connection after multiple retries.");
        }
    }

    /// <summary>
    /// Provides extension methods for <see cref="SqlException"/>.
    /// This should ideally be placed in a common utility project or within the framework,
    /// but is kept here for current context.
    /// </summary>
    internal static class SqlExceptionExtensions
    {
        // A more comprehensive list of transient SQL error codes
        private static readonly HashSet<int> TransientErrorCodes = new HashSet<int>
        {
            4060, // Cannot open database requested by the login.
            40197, // The service encountered an error processing your request.
            40501, // The service is currently busy. Retry after a few seconds.
            40540, // The service has encountered too many requests. Retry after a few seconds.
            40613, // Database xx on server xx is not currently available. Please retry the connection later.
            49918, // Cannot process request. Too many operations in progress for subscription "%.*ls" on server "%.*ls".
            49919, // Cannot process request. Too many operations in progress for subscription "%.*ls" on server "%.*ls".
            49920, // Cannot process request. Too many operations in progress for subscription "%.*ls" on server "%.*ls".
            10928, // A severe error occurred on the current command. The results, if any, should be discarded.
            10929, // A severe error occurred on the current command. The results, if any, should be discarded.
            10053, // A transport-level error has occurred when receiving results from the server.
            10054, // A transport-level error has occurred when sending the request to the server.
            10060, // A network-related or instance-specific error occurred while establishing a connection to SQL Server.
            20,    // The instance of SQL Server you attempted to connect to does not support encryption.
            233,   // The server did not respond or the connection was forcibly closed.
            921,   // Database '%.*ls' cannot be opened. It is in the middle of a restore.
            1205,  // Deadlock victim
            1973,  // Error during login, timeout.
            1974,  // Login failed for user. (though this is not always transient, sometimes it's good to retry if network is flaky)
            // Azure SQL specific transient errors
            4221,  // Login timeout.
            40854, // Internal transient error.
            40858, // Internal transient error.
            10060, // Network error
            10054, // Network error
            11001, // Host not found (DNS issues, transient)
            40143, // The database '%.*ls' has reached its size quota.
            40145, // The database encountered a connection error.
            40158  // An internal error occurred while processing the request. Please retry later.
        };

        public static bool IsTransient(this SqlException ex)
        {
            // Check for specific error codes
            if (TransientErrorCodes.Contains(ex.Number))
            {
                return true;
            }

            // Check if connection is broken or during login
            foreach (SqlError error in ex.Errors)
            {
                // Severity 11-16 (user error, not transient often)
                // Severity 17-20 (system error, often transient)
                if (error.Class >= 17) // Check for severe errors
                {
                    return true;
                }
            }

            // General network-related errors which often classify as transient
            // These might not have specific numbers but manifest as certain InnerExceptions
            if (ex.InnerException is System.Net.Sockets.SocketException socketEx)
            {
                switch (socketEx.SocketErrorCode)
                {
                    case System.Net.Sockets.SocketError.ConnectionAborted:
                    case System.Net.Sockets.SocketError.ConnectionReset:
                    case System.Net.Sockets.SocketError.TimedOut:
                    case System.Net.Sockets.SocketError.HostNotFound: // DNS resolution issues
                    case System.Net.Sockets.SocketError.TryAgain:     // Temporary lookup failure
                        return true;
                }
            }

            return false;
        }
    }
}
