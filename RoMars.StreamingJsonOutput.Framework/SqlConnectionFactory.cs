using Microsoft.Data.SqlClient;
using System.Data.Common; // For DbConnection

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

        public SqlConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
