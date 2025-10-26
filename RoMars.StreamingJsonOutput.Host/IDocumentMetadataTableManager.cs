namespace RoMars.StreamingJsonOutput.Host
{
    /// <summary>
    /// Defines an interface for managing the document metadata table, specifically ensuring its existence and providing record counts.
    /// This adheres to the Single Responsibility Principle (SRP) by isolating table management
    /// concerns and promotes the Open/Closed Principle (OCP) by allowing different table
    /// management strategies without modifying consuming code.
    /// </summary>
    public interface IDocumentMetadataTableManager
    {
        Task EnsureTableExistsAsync();
        Task<long> GetCurrentRecordCountAsync();
    }
}
