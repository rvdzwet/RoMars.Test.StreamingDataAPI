using System.Threading.Tasks;

namespace RoMars.Test.StreamingDataAPI
{
    /// <summary>
    /// Defines an interface for managing the product table, specifically ensuring its existence.
    /// This adheres to the Single Responsibility Principle (SRP) by isolating table management
    /// concerns and promotes the Open/Closed Principle (OCP) by allowing different table
    /// management strategies without modifying consuming code.
    /// </summary>
    public interface IProductTableManager
    {
        Task EnsureTableExistsAsync();
    }
}
