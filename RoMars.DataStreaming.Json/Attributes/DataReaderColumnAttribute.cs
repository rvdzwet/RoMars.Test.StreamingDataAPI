using System;

namespace RoMars.DataStreaming.Json.Attributes
{
    /// <summary>
    /// Specifies that an interface property should be populated from a specific column in a DbDataReader.
    /// Used by the Json streaming logic to map DbDataReader columns to JSON properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DataReaderColumnAttribute : Attribute
    {
        /// <summary>
        /// The name of the column in the DbDataReader to map to this property.
        /// </summary>
        public string? ColumnName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataReaderColumnAttribute"/> class.
        /// </summary>
        /// <param name="columnName">The name of the column in the DbDataReader.</param>
        public DataReaderColumnAttribute(string columnName)
        {
            ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataReaderColumnAttribute"/> class
        /// when the property name matches the column name.
        /// </summary>
        public DataReaderColumnAttribute() { }
    }
}
