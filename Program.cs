using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using RoMars.Test.StreamingDataAPI;
using System.Data.Common; // For DbConnection

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // --- 0. Infrastructure Setup and Configuration ---

        // Configure extremely verbose logging for diagnostics (set minimum level to Trace)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
        }

        // Configure Response Compression
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = new[] { "application/json" };
        });

        // --- 1. Service Registration and Dependency Injection ---

        // Register IDbConnectionFactory as a singleton
        builder.Services.AddSingleton<IDbConnectionFactory>(sp => new SqlConnectionFactory(connectionString));

        // Register IProductDataGenerator as a singleton
        // Performance Comment: ProductDataReader is a zero-allocation generator that creates
        // synthetic data on-the-fly, avoiding large memory footprints. Registering it
        // as a singleton (if its state management is thread-safe and stateless or idempotent)
        // ensures that creation overhead is incurred only once.
        builder.Services.AddSingleton<IProductDataGenerator>(sp => new ProductDataReader(
            builder.Configuration.GetValue<long>("Seeding:RecordCount", 10_000_000))); // Default to 10M records

        // Register IProductTableManager as a singleton
        builder.Services.AddSingleton<IProductTableManager, ProductTableManager>();

        // Register ProductRepository as a singleton. It depends on IDbConnectionFactory and ILogger.
        builder.Services.AddSingleton<ProductRepository>();

        // Register Seeder as a singleton. It orchestrates table management and data generation.
        builder.Services.AddSingleton<Seeder>();

        // Configure high concurrency limits on the web server
        // Performance Comment: Kestrel is configured with 5000 MaxConcurrentConnections.
        // This is a server-level optimization that allows Kestrel to handle a large
        // number of concurrent client connections, crucial for high-throughput
        // streaming APIs.
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxConcurrentConnections = 5000;
        });

        // Application Build
        var app = builder.Build();

        // --- 2. Database Seeding on Startup ---
        // CRITICAL: Execute Seeding synchronously on startup (only once). This MUST complete before the server starts.
        // For production, this should typically be guarded by an environment check or a health check that ensures
        // the database is ready and seeded, perhaps as part of a migration system, not on every app startup.
        // Performance Comment: Seeding is a one-time startup cost. Although it's synchronous here,
        // the seeding process itself (using SqlBulkCopy and IDataReader) is highly optimized
        // for performance.
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var seeder = services.GetRequiredService<Seeder>();
            seeder.SeedProductsAsync().GetAwaiter().GetResult(); // Block startup until seeding is done
        }

        // --- 3. Minimal API Definition and Execution ---

        app.UseResponseCompression(); // Performance Comment: Enables Brotli/Gzip compression for
                                      // JSON responses, significantly reducing bandwidth usage and
                                      // improving client-side load times, especially for large data streams.

        app.UseResponseCompression();

        app.MapGet("/api/products/ultimate-stream",
            ([FromServices] ProductRepository repo, [FromServices] ILogger<StreamingJsonResult> logger) =>
            {
                return new StreamingJsonResult(repo, logger);
            })
        .WithName("UltimateStream");

        app.Run();
    }
}
