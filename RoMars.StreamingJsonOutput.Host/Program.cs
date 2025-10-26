using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.SqlClient;
using RoMars.StreamingJsonOutput.Framework;
using RoMars.StreamingJsonOutput.Host;
using System.Data;
using System.Data.Common;
using IDbConnectionFactory = RoMars.StreamingJsonOutput.Framework.IDbConnectionFactory;
using SqlConnectionFactory = RoMars.StreamingJsonOutput.Framework.SqlConnectionFactory;
using SqlParameter = Microsoft.Data.SqlClient.SqlParameter;
using Microsoft.Extensions.Logging; // Added for ILogger usage
using HostLoggerExtensions = RoMars.StreamingJsonOutput.Host.Extensions.LoggerExtensions; // Alias for custom LoggerExtensions

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureLogging(builder);

        var connectionString = GetConnectionString(builder);

        ConfigureResponseCompression(builder);

        builder.Services.AddSingleton<IDbConnectionFactory>(sp => new SqlConnectionFactory(connectionString, sp.GetRequiredService<ILogger<SqlConnectionFactory>>()));
        builder.Services.AddSingleton<IStreamingQueryExecutor, DbDataReaderStreamingQueryExecutor>();

        ConfigureSeeder(builder);

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxConcurrentConnections = 5000;
        });

        var app = builder.Build();
        var mainLogger = app.Services.GetRequiredService<ILogger<Program>>(); // Get logger for Program class

        try
        {
            await EnsureDatabaseSeededAsync(app, builder.Configuration);

            app.UseResponseCompression();

            app.MapGet("/api/documents/ultimate-stream", UltimateDocumentMetadataStream)
                .WithName("UltimateDocumentMetadataStream");

            app.Run();
        }
        catch (Exception ex)
        {
            mainLogger.LogCritical(HostLoggerExtensions.LogApplicationTerminatedUnexpectedly, ex, "Application terminated unexpectedly."); // Use the EventId directly on the logger
            throw; // Re-throw to allow default host error handling
        }
    }

    private static void ConfigureLogging(WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
    }

    private static string GetConnectionString(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
        }
        return connectionString;
    }

    private static void ConfigureResponseCompression(WebApplicationBuilder builder)
    {
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json", "application/octet-stream" });
        });
    }

    private static void ConfigureSeeder(WebApplicationBuilder builder)
    {
        var batchSize = builder.Configuration.GetValue<int>("Seeding:BatchSize", 50000);
        var bulkCopyTimeoutSeconds = builder.Configuration.GetValue<int>("Seeding:BulkCopyTimeoutSeconds", 600);

        builder.Services.AddSingleton<IDocumentMetadataTableManager, DocumentMetadataTableManager>();

        builder.Services.AddSingleton<Seeder>(sp => new Seeder(
            sp.GetRequiredService<ILogger<Seeder>>(),
            sp.GetRequiredService<IDocumentMetadataTableManager>(),
            sp.GetRequiredService<IDbConnectionFactory>(),
            sp.GetRequiredService<ILogger<DocumentMetadataDataReader>>(), // Add this line
            batchSize,
            bulkCopyTimeoutSeconds));
    }

    private static async Task EnsureDatabaseSeededAsync(WebApplication app, IConfiguration configuration)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();

        var tableManager = services.GetRequiredService<IDocumentMetadataTableManager>();
        var seeder = services.GetRequiredService<Seeder>();
        var targetRecordCount = configuration.GetValue<long>("Seeding:RecordCount", 1_000_000);

        await tableManager.EnsureTableExistsAsync();

        var currentRecordCount = await tableManager.GetCurrentRecordCountAsync();

        if (currentRecordCount < targetRecordCount)
        {
            logger.LogInformation(
                "Database has {CurrentCount:N0} records. Target is {TargetCount:N0}. Starting seeding...",
                currentRecordCount, targetRecordCount);

            await seeder.SeedProductsAsync(
                currentRecordCount,
                targetRecordCount,
                app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }
        else
        {
            var message = currentRecordCount == targetRecordCount
                ? "Database already has target records ({CurrentCount:N0}). Seeding skipped."
                : "Database has more records ({CurrentCount:N0}) than the target ({TargetCount:N0}). No seeding performed.";

            logger.LogInformation(message, currentRecordCount, targetRecordCount);
        }
    }

    private static async Task<IResult> UltimateDocumentMetadataStream(
        [FromServices] IStreamingQueryExecutor executor,
        [FromServices] IDbConnectionFactory connectionFactory,
        [FromServices] ILogger<StreamingDbDataReaderJsonResult> logger,
        CancellationToken cancellationToken)
    {
        var columnNames = DocumentMetadataSchema.Columns.Select(c => c.Name);
        var selectList = string.Join(", ", columnNames);
        string query = $"SELECT TOP 1000 {selectList} FROM DocumentMetadata WHERE MortgageAmount > @MinAmount ORDER BY MortgageAmount";

        var parameters = new DbParameter[]
        {
            new SqlParameter("@MinAmount", SqlDbType.Decimal)
            {
                Value = 100000.00m,
                Precision = 18,
                Scale = 2
            }
        };

        var (connection, reader) = await executor.ExecuteStreamingQueryAsync(query, parameters, cancellationToken);

        return new StreamingDbDataReaderJsonResult(connection, reader, logger);
    }
}
