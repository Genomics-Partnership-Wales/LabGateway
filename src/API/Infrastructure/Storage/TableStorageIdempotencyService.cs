using Azure;
using Azure.Data.Tables;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabResultsGateway.API.Infrastructure.Storage;

public class TableStorageIdempotencyService : IIdempotencyService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly IdempotencyOptions _options;
    private readonly ILogger<TableStorageIdempotencyService> _logger;

    public TableStorageIdempotencyService(
        TableServiceClient tableServiceClient,
        IOptions<IdempotencyOptions> options,
        ILogger<TableStorageIdempotencyService> logger)
    {
        _tableServiceClient = tableServiceClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> HasBeenProcessedAsync(string blobName, byte[] contentHash)
    {
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
                    return true;
                }
            }
        }

        return false;
    }

    public async Task MarkAsProcessedAsync(string blobName, byte[] contentHash, ProcessingOutcome outcome)
    {
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
