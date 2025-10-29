using System.Text.Json.Serialization;
using RoMars.DataStreaming.Json.Attributes;

namespace RoMars.StreamingJsonOutput.Framework.Models
{
    /// <summary>
    /// Represents the customer address information in a structured JSON format.
    /// Uses <see cref="JsonFlattenAttribute"/> to flatten these properties into a parent object
    /// during streaming if it is a property of another DTO, otherwise it will be a nested object.
    /// </summary>
    public interface ICustomerAddressDto
    {
        [JsonPropertyName("line1")]
        [DataReaderColumn("CustomerAddress_Line1")]
        string? Line1 { get; }

        [JsonPropertyName("line2")]
        [DataReaderColumn("CustomerAddress_Line2")]
        string? Line2 { get; }

        [JsonPropertyName("city")]
        [DataReaderColumn("CustomerCity")]
        string? City { get; }

        [JsonPropertyName("state")]
        [DataReaderColumn("CustomerState")]
        string? State { get; }

        [JsonPropertyName("zip")]
        [DataReaderColumn("CustomerZip")]
        string? Zip { get; }
    }
}
