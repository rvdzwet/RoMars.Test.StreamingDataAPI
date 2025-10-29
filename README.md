# RoMars.Test.StreamingDataAPI - Extreme Performance Data Streaming

This project presents a highly optimized ASP.NET Core API showcasing **extreme performance and minimal memory allocation** for streaming large datasets. Designed for demanding scenarios like Document Management Systems (DMS) handling `10,000,000` or more records of mortgage document metadata, it demonstrates cutting-edge techniques for high-throughput data access and serialization in .NET.

Developers looking to build ultra-responsive APIs for massive data loads will find this project invaluable.

## Key Features

*   **Zero-Allocation JSON Streaming with Interface-Based Mapping:** Achieves peak efficiency by directly writing JSON data from a DbDataReader to the HTTP response stream using `Utf8JsonWriter`. This new approach defines JSON output structure using C# interfaces with custom attributes (`DataReaderColumn`, `JsonFlatten`, `DataReaderArrayPattern`), eliminating intermediate object allocations per row and significantly reducing garbage collection pressure. The core streaming logic is encapsulated in a reusable library (`RoMars.DataStreaming.Json`).
*   **Highly Optimized Database Access:** Leverages `SqlDataReader` with `CommandBehavior.SequentialAccess`, parameterized queries, and prepared statements for incredibly efficient, unbuffered data retrieval from SQL Server. This is particularly critical for wide tables (105 columns) and large result sets.
*   **Robust Resilience:** Incorporates advanced retry logic with exponential backoff for transient SQL connection failures, ensuring high availability and fault tolerance in dynamic cloud environments (e.g., Kubernetes).
*   **High Concurrency Ready:** Kestrel server is finely tuned to manage a vast number of concurrent connections, vital for high-volume API services.
*   **Response Compression:** Integrates Brotli and Gzip compression at the HTTP layer, drastically reducing network payload sizes and accelerating client-side rendering, especially impactful for streaming numerous columns.
*   **SOLID Principles & Clean Code:** Architected following SOLID principles (SRP, OCP, DIP) through extensive use of interfaces (`IDocumentMetadataGenerator`, `IDocumentMetadataTableManager`, `IDbConnectionFactory`, `IStreamingQueryExecutor`, `IDocumentMetadataDto` and related DTO interfaces) for superior modularity, testability, and maintainability.
*   **Efficient Bulk Data Seeding:** Utilizes `SqlBulkCopy` with a custom `DbDataReader` (`DocumentMetadataDataReader`) for extremely rapid, zero-allocation database seeding, capable of inserting millions of wide-table records in seconds.
*   **Enterprise-Grade Logging:** Configured with structured logging (Trace level for detailed diagnostics) providing deep insights into connection times, query execution phases, streaming progress, and correlated operational events (using `OperationId`s), crucial for performance diagnostics and debugging.

## Extreme Performance Architecture: Why It's Blazing Fast

The exceptional performance of this API stems from a combination of meticulously chosen patterns and optimizations:

1.  **Direct `DbDataReader` to HTTP Response Streaming with Interface-Based Mapping (`GenericStreamingJsonResult<TInterface>`):**
    *   **Low Memory Footprint:** Instead of loading the entire dataset into memory as a collection of objects (which would be catastrophic for 10M+ records), data is read row-by-row from the `DbDataReader` and immediately written to the HTTP response stream. The mapping from flat `DbDataReader` rows to complex, hierarchical JSON is driven by C# interfaces and custom attributes, ensuring no intermediate object allocations for each row being streamed.
    *   **`Utf8JsonWriter` for Zero-Allocation JSON:** `System.Text.Json.Utf8JsonWriter` is used directly against `response.BodyWriter.AsStream()`. This avoids string allocations for property names and values, creating JSON in a highly efficient, binary-optimized manner without intermediate `string` or `JToken` objects.
    *   **`IResult` Integration:** Implementing `IResult` in Minimal APIs allows direct control over the HTTP response, enabling custom, high-performance serialization logic.

2.  **`CommandBehavior.SequentialAccess` for Database Querying:**
    *   **Unbuffered Data Retrieval:** When dealing with wide tables (e.g., 105 columns), `SequentialAccess` instructs SQL Server to send data in a forward-only stream. Columns are read sequentially, preventing the entire row from being loaded into memory upon `Read()`, thus dramatically reducing client-side memory usage and avoiding `OutOfMemoryException`s.
    *   **Optimal for `DbDataReader` Streaming:** This behavior perfectly pairs with the direct `DbDataReader` to JSON streaming approach, as data is consumed in the same sequential manner it's retrieved.

3.  **Asynchronous I/O Everywhere:**
    *   All database operations (`OpenAsync`, `PrepareAsync`, `ExecuteReaderAsync`) and response writing (`response.BodyWriter.AsStream().WriteAsync`) are fully asynchronous.
    *   **Non-Blocking Operations:** This prevents blocking the ASP.NET Core Kestrel thread pool, allowing the server to handle a high volume of concurrent requests efficiently without resource exhaustion, leading to higher throughput.

4.  **Prepared Statements & Parameterized Queries:**
    *   **Reduced Query Parsing Overhead:** Preparing statements once (via `SqlCommand.PrepareAsync`) allows the database engine to optimize query execution plans, reusing them for subsequent calls and significantly reducing CPU cycles on the database server.
    *   **Security & Performance:** Parameterized queries prevent SQL injection and ensure that query plans can be cached effectively, further boosting performance.

5.  **Connection Pooling with Resilient Retries:**
    *   **Reduced Connection Setup Overhead:** Database connections are reused from a pool, eliminating the costly overhead of establishing new connections for every request.
    *   **Transient Fault Handling:** Built-in retry logic with exponential backoff for transient SQL errors ensures that temporary network glitches or database availability issues don't lead to failed requests, improving overall system robustness and perceived performance.

6.  **HTTP Response Compression:**
    *   **Minimized Network Bandwidth:** Using Brotli and Gzip compression significantly shrinks the size of the data transferred over the network. This translates to faster download times for clients, especially over slower connections, and reduced bandwidth costs.

By combining these techniques, the `RoMars.Test.StreamingDataAPI` achieves a level of performance that is critical for handling vast amounts of data efficiently and reliably.

## Architecture

The application is structured to decouple concerns:

*   **`RoMars.DataStreaming.Json` (NEW Project)**: A standalone, generic class library containing the core, reusable logic for zero-allocation JSON streaming from `DbDataReader` to `Utf8JsonWriter`. This includes:
    *   **Custom Attributes**: `DataReaderColumnAttribute`, `JsonFlattenAttribute`, `DataReaderArrayPatternAttribute` for declarative interface-based mapping.
    *   **`DataReaderJsonWriterStrategy<TInterface>`**: Analyzes annotated DTO interfaces to build an efficient, runtime serialization plan.
    *   **`GenericStreamingJsonResult<TInterface>`**: A custom `IResult` for ASP.NET Core Minimal APIs that executes the serialization plan to stream JSON directly to the HTTP response.
    *   **`DataStreamingJsonLoggerExtensions`**: Provides structured logging for the generic streaming components.
*   **`Program.cs`**: Handles application startup, configures logging, sets up response compression, configures Kestrel, and registers all services via Dependency Injection. It also orchestrates the synchronous database seeding on startup and integrates the new generic streaming components.
*   **`DocumentMetadataSchema.cs`**: A central static class defining the schema for all 105 columns of the `DocumentMetadata` table, ensuring consistency.
*   **`IDocumentMetadataGenerator.cs` / `DocumentMetadataDataReader.cs`**: Defines an interface for generating synthetic mortgage document metadata and its concrete, highly efficient implementation as a custom `DbDataReader` for `SqlBulkCopy` and generic streaming.
*   **`IDbConnectionFactory.cs` / `SqlConnectionFactory.cs`**: Provides an abstraction for creating database connections, promoting the Dependency Inversion Principle.
*   **`IDocumentMetadataTableManager.cs` / `DocumentMetadataTableManager.cs`**: Encapsulates database table schema management.
*   **`IStreamingQueryExecutor.cs` / `DbDataReaderStreamingQueryExecutor.cs`**: Defines and provides the concrete, highly optimized implementation for executing streaming database queries with retry logic and enhanced logging.
*   **`IDocumentMetadataDto.cs` & Nested DTO Interfaces (e.g., `ICustomerDto`, `IPropertyDto`)**: Located in `RoMars.StreamingJsonOutput.Framework/Models`, these interfaces define the application-specific structured JSON contract for document metadata, utilizing the custom attributes from `RoMars.DataStreaming.Json`.
*   **`StreamingApiResponseJsonResult.cs`**: A custom `IResult` implementation that streams JSON data from `DbDataReader` wrapped in a generic API response envelope (with metadata and data sections). Its logging is now handled by `ApiResponseLoggerExtensions`.
*   **`SqlExceptionExtensions.cs`**: An extension method to identify transient SQL errors.
*   **`FrameworkLoggerExtensions.cs`**: Defines static `EventId` constants for structured, enterprise-grade logging across the core framework, distinct from `ApiResponseLoggerExtensions`.
*   **`ApiResponseLoggerExtensions.cs`**: Provides structured logging specifically for `StreamingApiResponseJsonResult`.

### Document Metadata Table Schema (105 Columns)

The `DocumentMetadata` table contains 105 columns designed to represent various attributes of mortgage documents. This wide table setup is intended to demonstrate performance characteristics under high data volume and breadth.

*   `DocumentId` (BIGINT, Primary Key)
*   `DocumentTitle` (NVARCHAR(255))
*   `MortgageAmount` (DECIMAL(18,2))
*   `DocumentType` (NVARCHAR(100))
*   `FileType` (NVARCHAR(50))
*   `CustomerName` (NVARCHAR(255))
*   `CustomerAddress_Line1` (NVARCHAR(255))
*   `CustomerAddress_Line2` (NVARCHAR(255))
*   `CustomerCity` (NVARCHAR(100))
*   `CustomerState` (NVARCHAR(50))
*   `CustomerZip` (NVARCHAR(20))
*   `LoanNumber` (NVARCHAR(50))
*   `PropertyAddress_Street` (NVARCHAR(255))
*   `PropertyAddress_City` (NVARCHAR(100))
*   `PropertyAddress_State` (NVARCHAR(50))
*   `PropertyAddress_Zip` (NVARCHAR(20))
*   `OriginalFilename` (NVARCHAR(255))
*   `SourceSystem` (NVARCHAR(100))
*   `WorkflowStatus` (NVARCHAR(50))
*   `ReviewerName` (NVARCHAR(100))
*   `Tag_01` to `Tag_20` (NVARCHAR(100) each)
*   `Comment_01` to `Comment_30` (NVARCHAR(4000) each)
*   `PageCount` (INT)
*   `DocumentSizeKB` (INT)
*   `VersionNumber` (INT)
*   `RetentionYears` (INT)
*   `CreditScore` (INT)
*   `LoanTermMonths` (INT)
*   `InterestRate` (DECIMAL(5,3))
*   `LTVRatio` (DECIMAL(5,2))
*   `PropertyAppraisalValue` (DECIMAL(18,2))
*   `InsurancePremium` (DECIMAL(10,2))
*   `ProcessingTimeMinutes` (INT)
*   `ComplianceScore` (DECIMAL(5,2))
*   `RiskRating` (INT)
*   `AuditCount` (INT)
*   `AssociatedFees` (DECIMAL(18,2))
*   `EscrowBalance` (DECIMAL(18,2))
*   `PropertyTaxAmount` (DECIMAL(18,2))
*   `DocumentHash_CRC32` (BIGINT)
*   `DocumentHash_MD5` (NVARCHAR(32))
*   `DocumentScore` (INT)
*   `CreationDate` (DATETIME2)
*   `LastModifiedDate` (DATETIME2)
*   `ReviewDate` (DATETIME2)
*   `ApprovalDate` (DATETIME2)
*   `ExpirationDate` (DATETIME2)
*   `OriginalUploadDate` (DATETIME2)
*   `LastAccessedDate` (DATETIME2)
*   `RetentionEndDate` (DATETIME2)
*   `NextReviewDate` (DATETIME2)
*   `FundingDate` (DATETIME2)
*   `DisbursementDate` (DATETIME2)
*   `ClosingDate` (DATETIME2)
*   `EffectiveDate_01` to `EffectiveDate_03` (DATETIME2 each)

## Getting Started

### Prerequisites

*   .NET 8 SDK
*   SQL Server (LocalDB, SQL Express, or a full SQL Server instance)

### Setup

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/yourusername/RoMars.Test.StreamingDataAPI.git
    cd RoMars.Test.StreamingDataAPI
    ```

2.  **Configure Connection String:**
    Update `appsettings.json` or `appsettings.Development.json` with your SQL Server connection string.
    Example `appsettings.Development.json`:
    ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Trace", // Extremely verbose logging for diagnostics
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "ConnectionStrings": {
        "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=StreamingDataDMSDb;Trusted_Connection=True;MultipleActiveResultSets=true"
      },
      "Seeding": {
        "RecordCount": 10000000 // Number of document metadata records to seed (e.g., 10 million)
      }
    }
    ```
    Ensure the `DefaultConnection` points to your SQL Server instance and a database name (e.g., `StreamingDataDMSDb`). The database will be created/recreated on startup if it doesn't exist.

3.  **Run the application:**
    ```bash
    dotnet run
    ```
    The application will automatically seed the database with 10 million document metadata records (configurable in `appsettings.json`) on first run. This might take a few seconds or minutes depending on your system's performance.

4.  **Access the API:**
    Once running, navigate to:
    ```
    https://localhost:7041/api/documents/ultimate-stream
    ```
    (Port might vary, check console output when running via `dotnet run`).
    This endpoint will stream a JSON array of 100 document metadata records with `MortgageAmount` greater than `100,000.00`.

## Contributing

We welcome contributions! Please see our `CONTRIBUTING.md` for guidelines.

## License

This project is licensed under the terms of the MIT License. See `LICENSE.txt` for details.
