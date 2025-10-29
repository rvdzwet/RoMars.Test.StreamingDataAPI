using System.Text.Json.Serialization;
using RoMars.DataStreaming.Json.Attributes;

namespace RoMars.StreamingJsonOutput.Framework.Models
{
    /// <summary>
    /// Represents the property address information in a structured JSON format.
    /// Uses <see cref="JsonFlattenAttribute"/> to flatten these properties into a parent object
    /// during streaming if it is a property of another DTO, otherwise it will be a nested object.
    /// </summary>
    public interface IPropertyAddressDto
    {
        [JsonPropertyName("street")]
        [DataReaderColumn("PropertyAddress_Street")]
        string? Street { get; }

        [JsonPropertyName("city")]
        [DataReaderColumn("PropertyAddress_City")]
        string? City { get; }

        [JsonPropertyName("state")]
        [DataReaderColumn("PropertyAddress_State")]
        string? State { get; }

        [JsonPropertyName("zip")]
        [DataReaderColumn("PropertyAddress_Zip")]
        string? Zip { get; }
    }
}
