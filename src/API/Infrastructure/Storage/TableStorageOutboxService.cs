using Azure;
using Azure.Data.Tables;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabResultsGateway.API.Infrastructure.Storage;

/// <summary>
/// Azure Table Storage implementation of the outbox service.
/// </summary>
public class TableStorageOutboxService : IOutboxService
{
    private readonly TableClient _tableClient;
    private readonly OutboxOptions _options;
    private readonly ILogger<TableStorageOutboxService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableStorageOutboxService"/> class.
    /// </summary>
    /// <param name="tableClient">The Azure Table Storage client.</param>
    /// <param name="options">The outbox configuration options.</param>
    /// <param name="logger">The logger.</param>
    public TableStorageOutboxService(
        TableClient tableClient,
        IOptions<OutboxOptions> options,
        ILogger<TableStorageOutboxService> logger)
    {
        _tableClient = tableClient ?? throw new ArgumentNullException(nameof(tableClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task AddMessageAsync(string messageType, string payload, string correlationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(correlationId);

        var message = new OutboxMessage
        {
            MessageType = messageType,
            Payload = payload,
            CorrelationId = correlationId,
            Status = OutboxStatus.Pending,
            CreatedAt = DateTimeOffset.Now
        };

        var tableEntity = CreateTableEntity(message);

        _logger.LogInformation(
            "Adding message to outbox. MessageType: {MessageType}, CorrelationId: {CorrelationId}, Id: {Id}",
            messageType, correlationId, message.Id);

        await _tableClient.AddEntityAsync(tableEntity, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IList<OutboxMessage>> GetPendingMessagesAsync(int maxCount = 100, CancellationToken cancellationToken = default)
    {
        var filter = $"Status eq '{OutboxStatus.Pending}'";
        var query = _tableClient.QueryAsync<TableEntity>(filter, maxPerPage: maxCount, cancellationToken: cancellationToken);

        var messages = new List<OutboxMessage>();
        await foreach (var entity in query)
        {
            messages.Add(CreateOutboxMessage(entity));
            if (messages.Count >= maxCount)
                break;
        }

        _logger.LogInformation("Retrieved {Count} pending messages from outbox", messages.Count);
        return messages;
    }

    /// <inheritdoc/>
    public async Task MarkAsDispatchedAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var entity = await _tableClient.GetEntityAsync<TableEntity>(id, id, cancellationToken: cancellationToken);
        entity.Value["Status"] = OutboxStatus.Dispatched.ToString();
        entity.Value["DispatchedAt"] = DateTimeOffset.Now.ToString("O");

        await _tableClient.UpdateEntityAsync(entity.Value, ETag.All, TableUpdateMode.Replace, cancellationToken);

        _logger.LogInformation("Marked message as dispatched. Id: {Id}", id);
    }

    /// <inheritdoc/>
    public async Task MarkAsFailedAsync(string id, string errorMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var entity = await _tableClient.GetEntityAsync<TableEntity>(id, id, cancellationToken: cancellationToken);
        var message = CreateOutboxMessage(entity.Value);

        message.RetryCount++;
        message.ErrorMessage = errorMessage;

        // Calculate next retry time with exponential backoff
        var delaySeconds = _options.RetryDelaySeconds * Math.Pow(2, message.RetryCount - 1);
        message.NextRetryAt = DateTimeOffset.Now.AddSeconds(delaySeconds);

        // If max retries exceeded, mark as abandoned
        if (message.RetryCount > _options.MaxRetries)
        {
            message.Status = OutboxStatus.Abandoned;
            message.AbandonAt = DateTimeOffset.Now;
        }
        // Otherwise, keep as pending for retry
        else
        {
            message.Status = OutboxStatus.Pending;
        }

        var updatedEntity = CreateTableEntity(message);
        await _tableClient.UpdateEntityAsync(updatedEntity, ETag.All, TableUpdateMode.Replace, cancellationToken);

        _logger.LogInformation(
            "Marked message as failed. Id: {Id}, RetryCount: {RetryCount}, Status: {Status}",
            id, message.RetryCount, message.Status);
    }

    /// <inheritdoc/>
    public async Task<int> CleanupOldMessagesAsync(CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTimeOffset.Now.AddDays(-_options.CleanupRetentionDays);
        var filter = $"Status eq '{OutboxStatus.Dispatched}' and DispatchedAt lt '{cutoffDate:O}'";

        var query = _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken);
        var deletedCount = 0;

        await foreach (var entity in query)
        {
            await _tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All, cancellationToken);
            deletedCount++;
        }

        _logger.LogInformation("Cleaned up {Count} old dispatched messages", deletedCount);
        return deletedCount;
    }

    private static TableEntity CreateTableEntity(OutboxMessage message)
    {
        var entity = new TableEntity(message.Id, message.Id)
        {
            ["MessageType"] = message.MessageType,
            ["Payload"] = message.Payload,
            ["Status"] = message.Status.ToString(),
            ["CreatedAt"] = message.CreatedAt.ToString("O"),
            ["RetryCount"] = message.RetryCount,
            ["CorrelationId"] = message.CorrelationId
        };

        if (message.DispatchedAt.HasValue)
            entity["DispatchedAt"] = message.DispatchedAt.Value.ToString("O");

        if (!string.IsNullOrEmpty(message.ErrorMessage))
            entity["ErrorMessage"] = message.ErrorMessage;

        if (message.NextRetryAt.HasValue)
            entity["NextRetryAt"] = message.NextRetryAt.Value.ToString("O");

        if (message.AbandonAt.HasValue)
            entity["AbandonAt"] = message.AbandonAt.Value.ToString("O");

        return entity;
    }

    private static OutboxMessage CreateOutboxMessage(TableEntity entity)
    {
        var message = new OutboxMessage
        {
            Id = entity.PartitionKey,
            MessageType = entity.GetString("MessageType"),
            Payload = entity.GetString("Payload"),
            Status = Enum.Parse<OutboxStatus>(entity.GetString("Status")),
            CreatedAt = DateTimeOffset.Parse(entity.GetString("CreatedAt")),
            RetryCount = entity.GetInt32("RetryCount") ?? 0,
            CorrelationId = entity.GetString("CorrelationId")
        };

        if (entity.TryGetValue("DispatchedAt", out var dispatchedAt) && dispatchedAt is string dispatchedAtStr)
            message.DispatchedAt = DateTimeOffset.Parse(dispatchedAtStr);

        if (entity.TryGetValue("ErrorMessage", out var errorMessage) && errorMessage is string errorMessageStr)
            message.ErrorMessage = errorMessageStr;

        if (entity.TryGetValue("NextRetryAt", out var nextRetryAt) && nextRetryAt is string nextRetryAtStr)
            message.NextRetryAt = DateTimeOffset.Parse(nextRetryAtStr);

        if (entity.TryGetValue("AbandonAt", out var abandonAt) && abandonAt is string abandonAtStr)
            message.AbandonAt = DateTimeOffset.Parse(abandonAtStr);

        return message;
    }
}
