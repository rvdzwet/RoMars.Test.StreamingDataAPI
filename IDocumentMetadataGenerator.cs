namespace RoMars.Test.StreamingDataAPI
{
    /// <summary>
    /// Defines an interface for generating product data in a streaming fashion.
    /// This adheres to the Single Responsibility Principle for data generation
    /// and the Open/Closed Principle, allowing new data generation logic
    /// without modifying existing consumers.
    /// </summary>
    public interface IProductDataGenerator
    {
        long TotalRecords { get; }
        bool Read();
        object GetValue(int i);
        int FieldCount { get; }
        string GetName(int i);
        Type GetFieldType(int i);
        bool IsDBNull(int i);
        int GetValues(object[] values);
        int GetOrdinal(string name);
    }
}
