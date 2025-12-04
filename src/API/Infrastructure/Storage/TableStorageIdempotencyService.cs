using Azure;
using Azure.Data.Tables;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LabResultsGateway.API.Infrastructure.Storage;

public class TableStorageIdempotencyService : IIdempotencyService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly IdempotencyOptions _options;
    private readonly ILogger<TableStorageIdempotencyService> _logger;
    private readonly Meter _meter;
    private readonly Counter<long> _idempotencyHits;
    private readonly Counter<long> _idempotencyMisses;
    private readonly ActivitySource _activitySource;

    public TableStorageIdempotencyService(
        TableServiceClient tableServiceClient,
        IOptions<IdempotencyOptions> options,
        ILogger<TableStorageIdempotencyService> logger)
    {
        _tableServiceClient = tableServiceClient;
        _options = options.Value;
        _logger = logger;
        _meter = new Meter("LabResultsGateway.Idempotency");
        _idempotencyHits = _meter.CreateCounter<long>("idempotency_hits", description: "Number of idempotency hits");
        _idempotencyMisses = _meter.CreateCounter<long>("idempotency_misses", description: "Number of idempotency misses");
        _activitySource = new ActivitySource("LabResultsGateway.Idempotency");
    }

    public async Task<bool> HasBeenProcessedAsync(string blobName, byte[] contentHash)
    {
        using var activity = _activitySource.StartActivity("CheckIdempotency");
        activity?.SetTag("blob.name", blobName);

        var tableClient = _tableServiceClient.GetTableClient(_options.TableName);
        await tableClient.CreateIfNotExistsAsync();

        var hashString = Convert.ToBase64String(contentHash);
        var entity = await tableClient.GetEntityIfExistsAsync<TableEntity>(blobName, hashString);

        if (entity.HasValue)
        {
            var processedAtString = entity.Value!.GetString("ProcessedAt");
            if (processedAtString != null)
            {
                var processedAt = DateTimeOffset.Parse(processedAtString);
                if (DateTimeOffset.UtcNow - processedAt < TimeSpan.FromHours(_options.TTLHours))
                {
                    _logger.LogInformation("Blob {BlobName} has already been processed", blobName);
                    _idempotencyHits.Add(1);
                    activity?.SetTag("idempotency.result", "hit");
                    return true;
                }
            }
        }

        _idempotencyMisses.Add(1);
        activity?.SetTag("idempotency.result", "miss");
        return false;
    }

    public async Task MarkAsProcessedAsync(string blobName, byte[] contentHash, ProcessingOutcome outcome)
    {
        using var activity = _activitySource.StartActivity("MarkAsProcessed");
        activity?.SetTag("blob.name", blobName);
        activity?.SetTag("processing.outcome", outcome.ToString());

        var tableClient = _tableServiceClient.GetTableClient(_options.TableName);
        await tableClient.CreateIfNotExistsAsync();

        var hashString = Convert.ToBase64String(contentHash);
        var entity = new TableEntity(blobName, hashString)
        {
            { "ProcessedAt", DateTimeOffset.UtcNow.ToString("O") },
            { "Outcome", outcome.ToString() }
        };

        await tableClient.UpsertEntityAsync(entity);
        _logger.LogInformation("Marked blob {BlobName} as processed with outcome {Outcome}", blobName, outcome);
    }
}
