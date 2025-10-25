# RoMars.Test.StreamingDataAPI

This project demonstrates a highly optimized streaming API in ASP.NET Core for serving large datasets (`10,000,000` records or more) with minimal memory allocation and extreme performance. It's designed to showcase best practices for high-throughput data access and serialization in a .NET environment.

## Key Features

*   **Zero-Allocation JSON Streaming:** Utilizes `Utf8JsonWriter` to directly write database query results to the HTTP response stream, bypassing intermediate object allocations.
*   **Highly Optimized Database Access:** Employs `SqlDataReader` with `CommandBehavior.SequentialAccess`, parameterized queries, and prepared statements for efficient, unbuffered data retrieval from SQL Server.
*   **Efficient Bulk Data Seeding:** Leverages `SqlBulkCopy` with a custom `IDataReader` (`ProductDataReader`) for rapid, zero-allocation database seeding, capable of inserting millions of records in seconds.
*   **Resilience:** Implements retry logic with exponential backoff for transient SQL connection failures, enhancing robustness in cloud-native (e.g., Kubernetes) environments.
*   **High Concurrency Ready:** Kestrel server configured to handle a large number of concurrent connections.
*   **Response Compression:** Integrates Brotli and Gzip compression to reduce network payload sizes and improve client-side performance.
*   **SOLID Principles & Clean Code:** Refactored to adhere to SOLID principles (SRP, OCP, DIP) using interfaces (`IProductDataGenerator`, `IProductTableManager`, `IDbConnectionFactory`) for better modularity, testability, and maintainability.
*   **Detailed Performance Logging:** Configured for `LogLevel.Trace` to provide extensive insights into connection times, query execution phases, and streaming progress, aiding in performance diagnostics.

## Architecture

The application is structured to decouple concerns:

*   **`Program.cs`**: Handles application startup, configures logging (Trace level for detailed performance monitoring), sets up response compression, configures Kestrel, and registers all services via Dependency Injection. It also orchestrates the synchronous database seeding on startup.
*   **`IProductDataGenerator.cs` / `ProductDataReader.cs`**: Defines an interface for generating product data and its concrete, highly efficient implementation. `ProductDataReader` acts as a custom `IDataReader` for `SqlBulkCopy`, generating synthetic data on-the-fly without requiring a large in-memory collection.
*   **`IDbConnectionFactory.cs` / `SqlConnectionFactory.cs`**: Provides an abstraction for creating database connections, promoting the Dependency Inversion Principle. `SqlConnectionFactory` is the concrete implementation for SQL Server.
*   **`IProductTableManager.cs` / `ProductTableManager.cs`**: Encapsulates database table schema management, ensuring the `Products` table exists and is indexed.
*   **`ProductRepository.cs`**: Responsible for data access, executing optimized SQL queries with retry logic. It abstracts interactions with the database and provides a `SqlDataReader` for streaming.
*   **`StreamingJsonResult.cs`**: A custom `IResult` implementation for ASP.NET Core Minimal APIs that directly streams JSON data from the `SqlDataReader` to the HTTP response using `Utf8JsonWriter`. This is the core of the zero-allocation streaming capability.
*   **`SqlExceptionExtensions.cs`**: An extension method to identify transient SQL errors, used by `ProductRepository` for resilient connection handling.

### Performance Optimizations Highlighted

Throughout the codebase, comments explain specific performance optimizations:
-   **`ProductDataReader.cs`**: Explanation of `Random.Shared` for thread safety, pre-allocation of `_values` array, and direct switch-case for `GetValue` for low-overhead data generation.
-   **`ProductRepository.cs`**: Detailed comments on connection pooling, parameterized queries, prepared statements, `CommandBehavior.SequentialAccess`, `CommandBehavior.CloseConnection`, and exponential backoff retry logic.
-   **`StreamingJsonResult.cs`**: Breakdown of `IResult` benefits, `Utf8JsonWriter` usage, direct `DbDataReader` streaming, column ordinal lookups, `response.BodyWriter.AsStream()`, and `CancellationToken` integration.
-   **`ProductTableManager.cs`**: Rationale for using a non-clustered index on `Price` for efficient querying.
-   **`Seeder.cs`**: Explanation of `SqlBulkCopy` for high-speed data insertion and the benefits of a large `BatchSize`.
-   **`Program.cs`**: Comments on Kestrel concurrency limits and response compression (`BrotliCompressionProvider`, `GzipCompressionProvider`).

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
        "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=StreamingDataDb;Trusted_Connection=True;MultipleActiveResultSets=true"
      },
      "Seeding": {
        "RecordCount": 10000000 // Number of records to seed (e.g., 10 million)
      }
    }
    ```
    Ensure the `DefaultConnection` points to your SQL Server instance and a database name (e.g., `StreamingDataDb`). The database will be created/recreated on startup if it doesn't exist.

3.  **Run the application:**
    ```bash
    dotnet run
    ```
    The application will automatically seed the database with 10 million products (configurable in `appsettings.json`) on first run. This might take a few seconds or minutes depending on your system's performance.

4.  **Access the API:**
    Once running, navigate to:
    ```
    https://localhost:7041/api/products/ultimate-stream
    ```
    (Port might vary, check console output when running via `dotnet run`).
    This endpoint will stream a JSON array of 100 products with prices greater than `50` (hardcoded for demonstration in `StreamingJsonResult`).

## Contributing

We welcome contributions! Please see our `CONTRIBUTING.md` for guidelines.

## License

This project is licensed under the terms of the MIT License. See `LICENSE.txt` for details.
