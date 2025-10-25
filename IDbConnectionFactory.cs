using System.Data.Common; // For DbConnection

namespace RoMars.Test.StreamingDataAPI
{
    /// <summary>
    /// Defines a factory for creating database connections.
    /// This abstraction adheres to the Dependency Inversion Principle, allowing
    /// components to depend on an abstraction rather than a concrete connection
    /// type (e.g., SqlConnection), improving testability and flexibility.
    /// </summary>
    public interface IDbConnectionFactory
    {
        DbConnection CreateConnection();
    }
}
