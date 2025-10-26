namespace RoMars.StreamingJsonOutput.Framework.Models
{
    public class ApiResponse<T>
    {
        public required ResponseMetadata Metadata { get; set; }
        public required T Data { get; set; }
    }

    public class ResponseMetadata
    {
        public required DateTime Timestamp { get; set; }
        public required string CorrelationId { get; set; }
        public long DurationMs { get; set; }
        public long RecordCount { get; set; }
        public required string Status { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}
