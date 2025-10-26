using System.Data.Common; // For DbConnection, DbDataReader, DbParameter

namespace RoMars.StreamingJsonOutput.Framework
{
    /// <summary>
    /// Defines an interface for executing streaming database queries asynchronously.
    /// This abstraction allows microservices to retrieve DbDataReaders for streaming
    /// without knowing the concrete database implementation details, adhering to the
    /// Dependency Inversion Principle.
    /// </summary>
    public interface IStreamingQueryExecutor
    {
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
        Task<(DbConnection Connection, DbDataReader Reader)> ExecuteStreamingQueryAsync(
            string query,
            DbParameter[]? parameters = null,
            CancellationToken cancellationToken = default,
            int commandTimeout = 60);
    }
}
