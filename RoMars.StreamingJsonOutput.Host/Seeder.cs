using Microsoft.Data.SqlClient;
using RoMars.StreamingJsonOutput.Framework;
using System.Diagnostics;

namespace RoMars.StreamingJsonOutput.Host
{
    /// <summary>
    /// Handles the bulk-seeding process for the DocumentMetadata table.
    /// This class uses SqlBulkCopy combined with a custom IDocumentMetadataGenerator (IDataReader)
    /// for zero-allocation streaming of data from C# to SQL Server, achieving massive insertion performance.
    /// </summary>
    /// <remarks>
    /// This class adheres to the Single Responsibility Principle by focusing solely on
    /// seeding the database. It depends on abstractions (IDocumentMetadataGenerator, IDocumentMetadataTableManager, IDbConnectionFactory)
    /// which promotes the Dependency Inversion Principle and Open/Closed Principle.
    /// </remarks>
    public class Seeder
    {
        private readonly IDocumentMetadataTableManager _tableManager;
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<Seeder> _logger;
        private readonly int _batchSize; // Configurable batch size
        private readonly int _bulkCopyTimeoutSeconds; // Configurable bulk copy timeout

        public Seeder(ILogger<Seeder> logger,
                      IDocumentMetadataTableManager tableManager,
                      IDbConnectionFactory connectionFactory,
                      int batchSize = 50000, // Default to 50,000 for seeding
                      int bulkCopyTimeoutSeconds = 600) // Default to 600 seconds (10 minutes)
        {
            _logger = logger;
            _tableManager = tableManager;
            _connectionFactory = connectionFactory;
            _batchSize = batchSize;
            _bulkCopyTimeoutSeconds = bulkCopyTimeoutSeconds;
        }

        /// <summary>
        /// Executes the bulk copy operation to seed the DocumentMetadata table, appending records
        /// until the total target count is reached. Includes progress and estimated time logging.
        /// </summary>
        /// <param name="initialRecordCount">The number of records already existing in the table before this seeding operation.</param>
        /// <param name="totalTargetRecordCount">The total number of records desired after this seeding operation.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        public async Task SeedProductsAsync(long initialRecordCount, long totalTargetRecordCount, CancellationToken cancellationToken = default)
        {
            if (initialRecordCount >= totalTargetRecordCount)
            {
                _logger.LogInformation("Database already has {CurrentCount:N0} records (target {TargetCount:N0}). Seeding skipped.",
                    initialRecordCount, totalTargetRecordCount);
                return;
            }

            await _tableManager.EnsureTableExistsAsync();

            using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var bulkCopyTimer = Stopwatch.StartNew();
            long totalRowsCopied = 0; // Total new rows copied in this operation
            long currentRecordCount = initialRecordCount; // Tracker for total records
            long remainingRecordsToGenerate = totalTargetRecordCount - initialRecordCount;

            _logger.LogInformation("Starting database seeding, appending {RecordsToGenerate:N0} records. Initial count: {InitialCount:N0}, Target count: {TargetCount:N0}. ThreadID: {ThreadId}",
                                    remainingRecordsToGenerate, initialRecordCount, totalTargetRecordCount, Environment.CurrentManagedThreadId);

            try
            {
                using (var bulkCopy = new SqlBulkCopy((SqlConnection)connection, SqlBulkCopyOptions.Default, null))
                {
                    bulkCopy.DestinationTableName = "DocumentMetadata";
                    bulkCopy.BatchSize = _batchSize;
                    bulkCopy.NotifyAfter = _batchSize;
                    bulkCopy.BulkCopyTimeout = _bulkCopyTimeoutSeconds;

                    // Update local tracker when a batch is copied and log progress
                    bulkCopy.SqlRowsCopied += (sender, e) =>
                    {
                        totalRowsCopied += e.RowsCopied;
                        var currentTotalRecords = initialRecordCount + totalRowsCopied;
                        double percentage = (double)currentTotalRecords / totalTargetRecordCount * 100.0;

                        // Calculate rate for ERT
                        double elapsedSeconds = bulkCopyTimer.Elapsed.TotalSeconds;
                        long rowsPerSecond = elapsedSeconds > 0 ? (long)(totalRowsCopied / elapsedSeconds) : 0;

                        TimeSpan estimatedRemainingTime = TimeSpan.Zero;
                        if (rowsPerSecond > 0 && currentTotalRecords < totalTargetRecordCount)
                        {
                            long remainingRows = totalTargetRecordCount - currentTotalRecords;
                            estimatedRemainingTime = TimeSpan.FromSeconds(remainingRows / (double)rowsPerSecond);
                        }

                        _logger.LogInformation("Seeding progress: {CurrentTotalRecords:N0} / {TotalTargetRecords:N0} records ({Percentage:F2}%). Batch: {BatchSize:N0}. ERT: {ERT}",
                            currentTotalRecords, totalTargetRecordCount, percentage, _batchSize, estimatedRemainingTime.ToString(@"hh\:mm\:ss"));
                    };

                    // Loop to seed data in batches until the target is met
                    while (currentRecordCount < totalTargetRecordCount && !cancellationToken.IsCancellationRequested)
                    {
                        // Calculate how many records to generate in this batch (up to the full BatchSize)
                        long recordsToGenerateInThisBatch = Math.Min(_batchSize, totalTargetRecordCount - currentRecordCount);

                        if (recordsToGenerateInThisBatch == 0)
                        {
                            break; // Should not happen if the while condition is correct, but safe check
                        }

                        _logger.LogTrace("Generating next batch of {BatchSize:N0} records starting from index {StartIndex:N0}.",
                            recordsToGenerateInThisBatch, currentRecordCount);

                        // Instantiate DocumentMetadataDataReader for the specific batch/chunk
                        // Note: The DocumentMetadataDataReader MUST be designed to stop generating rows
                        // after 'recordsToGenerateInThisBatch' or the number of rows passed in its constructor.
                        using var dataGenerator = new DocumentMetadataDataReader(currentRecordCount, recordsToGenerateInThisBatch);

                        await bulkCopy.WriteToServerAsync(dataGenerator, cancellationToken);

                        // IMPORTANT: Update the current count after a successful WriteToServerAsync
                        currentRecordCount += recordsToGenerateInThisBatch;

                        _logger.LogTrace("SqlBulkCopy finished writing batch to server. Current count: {CurrentCount:N0}.", currentRecordCount);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Seeding canceled by token after {TotalRowsCopied:N0} rows. ThreadID: {ThreadId}",
                    totalRowsCopied, Environment.CurrentManagedThreadId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SqlBulkCopy failed during seeding after {TotalRowsCopied:N0} rows. ThreadID: {ThreadId}",
                    totalRowsCopied, Environment.CurrentManagedThreadId);
                throw;
            }
            finally
            {
                bulkCopyTimer.Stop();
                // The final count is the initial count plus the total new rows copied
                _logger.LogInformation("Database seeding complete. Total new records seeded in this operation: {TotalRowsCopied:N0}. Total final records: {FinalCount:N0}. Total time: {Elapsed}. ThreadID: {ThreadId}",
                                        totalRowsCopied, initialRecordCount + totalRowsCopied, bulkCopyTimer.Elapsed, Environment.CurrentManagedThreadId);
            }
        }
    }
}
