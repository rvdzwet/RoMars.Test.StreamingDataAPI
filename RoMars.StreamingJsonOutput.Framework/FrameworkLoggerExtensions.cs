using Microsoft.Extensions.Logging;
using System; // For Guid

namespace RoMars.StreamingJsonOutput.Framework
{
    public static partial class FrameworkLoggerExtensions
    {
        // Category for Database Query Execution (EventId 1000-1099)
        private const int QueryExecutorEventIdBase = 1000;

        public static readonly EventId QueryInitiated = new(
            id: QueryExecutorEventIdBase + 1,
            name: "QueryInitiated");
        public static readonly EventId ConnectionCreated = new(
            id: QueryExecutorEventIdBase + 2,
            name: "ConnectionCreated");
        public static readonly EventId ConnectionOpened = new(
            id: QueryExecutorEventIdBase + 3,
            name: "ConnectionOpened");
        public static readonly EventId CommandNotSqlCommand = new(
            id: QueryExecutorEventIdBase + 4,
            name: "CommandNotSqlCommand");
        public static readonly EventId QuerySetupComplete = new(
            id: QueryExecutorEventIdBase + 5,
            name: "QuerySetupComplete");
        public static readonly EventId TransientDatabaseError = new(
            id: QueryExecutorEventIdBase + 6,
            name: "TransientDatabaseError");
        public static readonly EventId NonTransientDatabaseError = new(
            id: QueryExecutorEventIdBase + 7,
            name: "NonTransientDatabaseError");
        public static readonly EventId UnexpectedError = new(
            id: QueryExecutorEventIdBase + 8,
            name: "UnexpectedError");
        public static readonly EventId FailedToEstablishConnection = new(
            id: QueryExecutorEventIdBase + 9,
            name: "FailedToEstablishConnection");

        // Category for SqlConnectionFactory (EventId 1200-1299)
        public static readonly EventId LogConnectionFactoryInitialized = new(1201, "ConnectionFactoryInitialized");
        public static readonly EventId LogConnectionCreatedSuccessfully = new(1202, "ConnectionCreatedSuccessfully");
        public static readonly EventId LogEmptyConnectionString = new(1203, "EmptyConnectionString");

    }
}
