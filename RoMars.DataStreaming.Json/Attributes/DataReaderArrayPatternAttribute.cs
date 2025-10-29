using System;

namespace RoMars.DataStreaming.Json.Attributes
{
    /// <summary>
    /// Specifies that multiple DbDataReader columns matching a pattern should be collected
    /// into a single JSON array property. This is useful for handling "bag of properties"
    /// scenarios where column names follow a consistent prefix and numbering (e.g., Tag_01, Tag_02).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DataReaderArrayPatternAttribute : Attribute
    {
        /// <summary>
        /// The prefix pattern for DbDataReader column names to be included in this array.
        /// E.g., "Tag_" would match "Tag_01", "Tag_02", etc.
        /// </summary>
        public string ColumnPrefix { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataReaderArrayPatternAttribute"/> class.
        /// </summary>
        /// <param name="columnPrefix">The prefix of the DbDataReader columns to collect into an array.</param>
        public DataReaderArrayPatternAttribute(string columnPrefix)
        {
            ColumnPrefix = columnPrefix ?? throw new ArgumentNullException(nameof(columnPrefix));
        }
    }
}
