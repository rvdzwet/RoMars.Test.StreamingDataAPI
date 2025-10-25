using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using RoMars.Test.StreamingDataAPI;

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

        // Service Registration
        builder.Services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<ProductRepository>();
            return new ProductRepository(connectionString, logger);
        });

        // Configure high concurrency limits on the web server
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxConcurrentConnections = 5000;
        });

        // --- 2. Minimal API Definition and Execution ---

        var app = builder.Build();

        // Resolve the necessary services for the Seeder
        var seederLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

        // CRITICAL: Execute Seeding synchronously on startup. This MUST complete before the server starts.
        // For production, you would wrap this in a check (e.g., if env is development or if db is empty).
        var seedingTask = Seeder.SeedProductsAsync(connectionString, seederLogger);
        seedingTask.GetAwaiter().GetResult(); // Block the startup until seeding is done

        app.UseResponseCompression();

        app.MapGet("/api/products/ultimate-stream",
            ([FromServices] ProductRepository repo, [FromServices] ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("StreamingJsonResultExecution");
                return new StreamingJsonResult(repo, logger);
            })
        .WithName("UltimateStream");

        app.Run();
    }
}
