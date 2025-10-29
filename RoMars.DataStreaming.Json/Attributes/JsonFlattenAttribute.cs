using System;

namespace RoMars.DataStreaming.Json.Attributes
{
    /// <summary>
    /// Specifies that properties of a nested interface should be flattened into the parent JSON object.
    /// This allows for creating a hierarchical C# interface structure while maintaining a flat or
    /// partially-flat JSON output, avoiding unnecessary nesting in the final JSON.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class JsonFlattenAttribute : Attribute
    {
        public JsonFlattenAttribute() { }
    }
}
