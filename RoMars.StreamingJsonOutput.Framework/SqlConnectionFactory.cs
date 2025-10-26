using Microsoft.Data.SqlClient;
using System.Data.Common; // For DbConnection
using Microsoft.Extensions.Logging;
using static RoMars.StreamingJsonOutput.Framework.FrameworkLoggerExtensions;

namespace RoMars.StreamingJsonOutput.Framework
{
    /// <summary>
    /// A concrete implementation of IDbConnectionFactory for creating SqlConnection instances.
    /// This class isolates the dependency on Microsoft.Data.SqlClient and its connection string,
    /// adhering to the Dependency Inversion Principle.
    /// </summary>
    public class SqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlConnectionFactory> _logger;

        public SqlConnectionFactory(string connectionString, ILogger<SqlConnectionFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError(LogEmptyConnectionString, "Connection string for SqlConnectionFactory cannot be null or empty.");
                throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
            }

            _connectionString = connectionString;
            _logger.LogInformation(LogConnectionFactoryInitialized, "SqlConnectionFactory initialized.");
        }

        public DbConnection CreateConnection()
        {
            _logger.LogTrace(LogConnectionCreatedSuccessfully, "New SqlConnection created.");
            return new SqlConnection(_connectionString);
        }
    }
}
