# RoMars.Test.StreamingDataAPI

This project demonstrates a highly optimized streaming API in ASP.NET Core for serving large datasets (`10,000,000` records or more) of **mortgage document metadata** with minimal memory allocation and extreme performance. It's designed to showcase best practices for high-throughput data access and serialization in a .NET environment for a Document Management System (DMS) context.

## Key Features

*   **Zero-Allocation JSON Streaming:** Utilizes `Utf8JsonWriter` to directly write document metadata query results to the HTTP response stream, bypassing intermediate object allocations.
*   **Highly Optimized Database Access:** Employs `SqlDataReader` with `CommandBehavior.SequentialAccess`, parameterized queries, and prepared statements for efficient, unbuffered data retrieval from SQL Server, particularly for wide tables (105 columns).
*   **Efficient Bulk Data Seeding:** Leverages `SqlBulkCopy` with a custom `IDataReader` (`DocumentMetadataDataReader`) for rapid, zero-allocation database seeding, capable of inserting millions of mortgage document metadata records in seconds.
*   **Resilience:** Implements retry logic with exponential backoff for transient SQL connection failures, enhancing robustness in cloud-native (e.g., Kubernetes) environments.
*   **High Concurrency Ready:** Kestrel server configured to handle a large number of concurrent connections for a high-volume DMS.
*   **Response Compression:** Integrates Brotli and Gzip compression to reduce network payload sizes and improve client-side performance, crucial for streaming numerous columns.
*   **SOLID Principles & Clean Code:** Refactored to adhere to SOLID principles (SRP, OCP, DIP) using interfaces (`IDocumentMetadataGenerator`, `IDocumentMetadataTableManager`, `IDbConnectionFactory`) for better modularity, testability, and maintainability across the DMS architecture.
*   **Detailed Performance Logging:** Configured for `LogLevel.Trace` to provide extensive insights into connection times, query execution phases, and streaming progress, aiding in performance diagnostics when debugging issues with wide datasets.

## Architecture

The application is structured to decouple concerns:

*   **`Program.cs`**: Handles application startup, configures logging (Trace level for detailed performance monitoring), sets up response compression, configures Kestrel, and registers all services via Dependency Injection. It also orchestrates the synchronous database seeding on startup.
*   **`DocumentMetadataSchema.cs`**: A central static class defining the schema for all 105 columns of the `DocumentMetadata` table. This ensures consistency across data generation, table creation, querying, and JSON streaming.
*   **`IDocumentMetadataGenerator.cs` / `DocumentMetadataDataReader.cs`**: Defines an interface for generating synthetic mortgage document metadata and its concrete, highly efficient implementation. `DocumentMetadataDataReader` acts as a custom `IDataReader` for `SqlBulkCopy`, generating wide table data (105 columns) on-the-fly without requiring a large in-memory collection.
*   **`IDbConnectionFactory.cs` / `SqlConnectionFactory.cs`**: Provides an abstraction for creating database connections, promoting the Dependency Inversion Principle. `SqlConnectionFactory` is the concrete implementation for SQL Server.
*   **`IDocumentMetadataTableManager.cs` / `DocumentMetadataTableManager.cs`**: Encapsulates database table schema management, ensuring the `DocumentMetadata` table exists with all 105 columns and is appropriately indexed (e.g., on `MortgageAmount`).
*   **`DocumentMetadataRepository.cs`**: Responsible for data access, executing optimized SQL queries to retrieve wide document metadata records with retry logic. It abstracts interactions with the database and provides a `SqlDataReader` for streaming.
*   **`StreamingDocumentMetadataJsonResult.cs`**: A custom `IResult` implementation for ASP.NET Core Minimal APIs that directly streams 105-column JSON data from the `SqlDataReader` to the HTTP response using `Utf8JsonWriter`. This is the core of the zero-allocation streaming capability for wide tables.
*   **`SqlExceptionExtensions.cs`**: An extension method to identify transient SQL errors, used by `DocumentMetadataRepository` for resilient connection handling.

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

### Performance Optimizations Highlighted

Throughout the codebase, comments explain specific performance optimizations:
-   **`DocumentMetadataDataReader.cs`**: Explanation of `Random.Shared` for thread safety, pre-allocation of `_values` array, and direct type-specific data generation with minimized string allocations for low-overhead data generation of 105 columns.
-   **`DocumentMetadataRepository.cs`**: Detailed comments on connection pooling, parameterized queries, prepared statements, `CommandBehavior.SequentialAccess` (critical for wide tables), `CommandBehavior.CloseConnection`, and exponential backoff retry logic for resilient document metadata retrieval.
-   **`StreamingDocumentMetadataJsonResult.cs`**: Breakdown of `IResult` benefits, `Utf8JsonWriter` usage, direct `DbDataReader` streaming, *dynamic* column ordinal lookups and type-specific writing (essential for wide tables without hardcoding), `response.BodyWriter.AsStream()`, and `CancellationToken` integration.
-   **`DocumentMetadataTableManager.cs`**: Rationale for using a non-clustered index on `MortgageAmount` for efficient querying of document metadata.
-   **`Seeder.cs`**: Explanation of `SqlBulkCopy` for high-speed data insertion of wide tables and the benefits of a large `BatchSize`.
-   **`Program.cs`**: Comments on Kestrel concurrency limits and response compression (`BrotliCompressionProvider`, `GzipCompressionProvider`) for handling large, multi-column data streams.

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
