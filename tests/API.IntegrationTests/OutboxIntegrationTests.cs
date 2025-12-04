using System;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Enums;
using LabResultsGateway.API.Infrastructure.Messaging;
using LabResultsGateway.API.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LabResultsGateway.API.IntegrationTests;

public class OutboxIntegrationTests : IAsyncLifetime
{
    private TableClient _outboxTableClient;
    private string _outboxTableName;
    private IOutboxService _outboxService;
    private IMessageQueueService _queueService;
    private OutboxAwareQueueService _outboxAwareQueueService;

    public async Task InitializeAsync()
    {
        // Use existing Azurite instance instead of Testcontainers
        // Connection string for local Azurite
        var connectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";
        var tableServiceClient = new TableServiceClient(connectionString);

        _outboxTableName = "testoutbox" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _outboxTableClient = tableServiceClient.GetTableClient(_outboxTableName);
        await _outboxTableClient.CreateIfNotExistsAsync();

        // Setup services
        var outboxOptions = new OutboxOptions
        {
            TableName = _outboxTableName,
            MaxRetries = 3,
            RetryDelaySeconds = 1, // Fast for testing
            CleanupRetentionDays = 30
        };

        var loggerMock = new Mock<ILogger<TableStorageOutboxService>>();
        _outboxService = new TableStorageOutboxService(
            _outboxTableClient,
            Options.Create(outboxOptions),
            loggerMock.Object);

        // Mock queue service for testing
        var queueServiceMock = new Mock<IMessageQueueService>();
        queueServiceMock.Setup(x => x.SendToProcessingQueueAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        var queueLoggerMock = new Mock<ILogger<OutboxAwareQueueService>>();
        _queueService = queueServiceMock.Object;
        _outboxAwareQueueService = new OutboxAwareQueueService(
            _queueService,
            _outboxService,
            queueLoggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        // Clean up test table
        if (_outboxTableClient != null)
        {
            await _outboxTableClient.DeleteAsync();
        }
    }

    [Fact]
    public async Task OutboxAwareQueueService_ShouldStoreMessageInOutbox_WhenQueueDispatchSucceeds()
    {
        // Arrange
        var message = "test HL7 message";

        // Act
        await _outboxAwareQueueService.SendToProcessingQueueAsync(message);

        // Assert
        var pendingMessages = await _outboxService.GetPendingMessagesAsync();
        pendingMessages.Should().HaveCount(1);

        var outboxMessage = pendingMessages.First();
        outboxMessage.MessageType.Should().Be("HL7Message");
        outboxMessage.Payload.Should().Be(message);
        outboxMessage.Status.Should().Be(OutboxStatus.Pending);
    }

    [Fact]
    public async Task OutboxAwareQueueService_ShouldStoreMessageInOutbox_WhenQueueDispatchFails()
    {
        // Arrange
        var message = "test HL7 message";
        var queueServiceMock = new Mock<IMessageQueueService>();
        queueServiceMock.Setup(x => x.SendToProcessingQueueAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new Exception("Queue unavailable"));

        var queueLoggerMock = new Mock<ILogger<OutboxAwareQueueService>>();
        var failingQueueService = new OutboxAwareQueueService(
            queueServiceMock.Object,
            _outboxService,
            queueLoggerMock.Object);

        // Act
        await failingQueueService.SendToProcessingQueueAsync(message);

        // Assert - Message should still be in outbox despite queue failure
        var pendingMessages = await _outboxService.GetPendingMessagesAsync();
        pendingMessages.Should().HaveCount(1);

        var outboxMessage = pendingMessages.First();
        outboxMessage.MessageType.Should().Be("HL7Message");
        outboxMessage.Payload.Should().Be(message);
        outboxMessage.Status.Should().Be(OutboxStatus.Pending);
    }

    [Fact]
    public async Task OutboxDispatcher_ShouldProcessPendingMessages()
    {
        // Arrange - Add a message to outbox
        var message = "HL7 message for processing";
        await _outboxAwareQueueService.SendToProcessingQueueAsync(message);

        // Simulate outbox dispatcher processing
        var pendingMessages = await _outboxService.GetPendingMessagesAsync();
        pendingMessages.Should().HaveCount(1);

        var messageToProcess = pendingMessages.First();

        // Act - Mark as dispatched (simulating successful processing)
        await _outboxService.MarkAsDispatchedAsync(messageToProcess.Id);

        // Assert
        var remainingPending = await _outboxService.GetPendingMessagesAsync();
        remainingPending.Should().BeEmpty();

        // Verify message was marked as dispatched
        var allMessages = await GetAllOutboxMessagesAsync();
        var processedMessage = allMessages.First(m => m.Id == messageToProcess.Id);
        processedMessage.Status.Should().Be(OutboxStatus.Dispatched);
        processedMessage.DispatchedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task OutboxDispatcher_ShouldHandleFailedMessagesWithRetry()
    {
        // Arrange - Add a message and simulate failure
        var message = "HL7 message that will fail";
        await _outboxAwareQueueService.SendToProcessingQueueAsync(message);

        var pendingMessages = await _outboxService.GetPendingMessagesAsync();
        var messageToProcess = pendingMessages.First();

        // Act - Mark as failed
        await _outboxService.MarkAsFailedAsync(messageToProcess.Id, "Processing failed");

        // Assert
        var messagesAfterFailure = await _outboxService.GetPendingMessagesAsync();
        messagesAfterFailure.Should().HaveCount(1); // Still pending for retry

        var failedMessage = messagesAfterFailure.First();
        failedMessage.RetryCount.Should().Be(1);
        failedMessage.ErrorMessage.Should().Be("Processing failed");
        failedMessage.NextRetryAt.Should().NotBeNull();
        failedMessage.Status.Should().Be(OutboxStatus.Pending);
    }

    [Fact]
    public async Task OutboxCleanup_ShouldRemoveOldDispatchedMessages()
    {
        // Arrange - Add and dispatch a message
        var message = "Old message to be cleaned up";
        await _outboxAwareQueueService.SendToProcessingQueueAsync(message);

        var pendingMessages = await _outboxService.GetPendingMessagesAsync();
        await _outboxService.MarkAsDispatchedAsync(pendingMessages.First().Id);

        // Manually set DispatchedAt to be old
        var oldMessage = pendingMessages.First();
        var entity = await _outboxTableClient.GetEntityAsync<TableEntity>(oldMessage.Id, oldMessage.Id);
        entity.Value["DispatchedAt"] = DateTimeOffset.Now.AddDays(-31).ToString("O"); // Older than retention
        await _outboxTableClient.UpdateEntityAsync(entity.Value, ETag.All, TableUpdateMode.Replace);

        // Act
        var deletedCount = await _outboxService.CleanupOldMessagesAsync();

        // Assert
        deletedCount.Should().Be(1);

        var remainingMessages = await GetAllOutboxMessagesAsync();
        remainingMessages.Should().BeEmpty();
    }

    private async Task<IList<OutboxMessage>> GetAllOutboxMessagesAsync()
    {
        var query = _outboxTableClient.QueryAsync<TableEntity>();
        var messages = new List<OutboxMessage>();

        await foreach (var entity in query)
        {
            messages.Add(CreateOutboxMessage(entity));
        }

        return messages;
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
