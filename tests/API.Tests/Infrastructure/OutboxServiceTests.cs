using System;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using FluentAssertions;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Enums;
using LabResultsGateway.API.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LabResultsGateway.API.Tests.Infrastructure;

public class OutboxServiceTests : IAsyncLifetime
{
    private TableClient _tableClient;
    private string _tableName;
    private OutboxOptions _options;
    private Mock<ILogger<TableStorageOutboxService>> _loggerMock;
    private TableStorageOutboxService _service;

    public async Task InitializeAsync()
    {
        // Use real Azurite for integration testing
        var tableServiceClient = new TableServiceClient("UseDevelopmentStorage=true");
        _tableName = "testoutbox" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _tableClient = tableServiceClient.GetTableClient(_tableName);
        await _tableClient.CreateIfNotExistsAsync();

        _options = new OutboxOptions
        {
            TableName = _tableName,
            MaxRetries = 3,
            RetryDelaySeconds = 10,
            CleanupRetentionDays = 30
        };

        _loggerMock = new Mock<ILogger<TableStorageOutboxService>>();
        _service = new TableStorageOutboxService(_tableClient, Options.Create(_options), _loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        // Cleanup table
        await _tableClient.DeleteAsync();
    }

    [Fact]
    public async Task AddMessageAsync_ShouldCreatePendingMessage()
    {
        // Arrange
        var messageType = "TestMessage";
        var payload = "{\"key\": \"value\"}";
        var correlationId = Guid.NewGuid().ToString();

        // Act
        await _service.AddMessageAsync(messageType, payload, correlationId);

        // Assert
        var messages = await _service.GetPendingMessagesAsync();
        messages.Should().HaveCount(1);

        var message = messages.First();
        message.MessageType.Should().Be(messageType);
        message.Payload.Should().Be(payload);
        message.CorrelationId.Should().Be(correlationId);
        message.Status.Should().Be(OutboxStatus.Pending);
        message.CreatedAt.Should().BeCloseTo(DateTimeOffset.Now, TimeSpan.FromSeconds(5));
        message.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task AddMessageAsync_ShouldThrowArgumentNullException_WhenMessageTypeIsNull()
    {
        // Act & Assert
        var action = () => _service.AddMessageAsync(null!, "payload", "correlationId");
        await action.Should().ThrowAsync<ArgumentNullException>().WithParameterName("messageType");
    }

    [Fact]
    public async Task GetPendingMessagesAsync_ShouldReturnOnlyPendingMessages()
    {
        // Arrange
        var correlationId1 = Guid.NewGuid().ToString();
        var correlationId2 = Guid.NewGuid().ToString();

        await _service.AddMessageAsync("Type1", "Payload1", correlationId1);
        await _service.AddMessageAsync("Type2", "Payload2", correlationId2);

        // Mark message with correlationId1 as dispatched
        var messages = await _service.GetPendingMessagesAsync();
        var messageToDispatch = messages.First(m => m.CorrelationId == correlationId1);
        await _service.MarkAsDispatchedAsync(messageToDispatch.Id);

        // Act
        var pendingMessages = await _service.GetPendingMessagesAsync();

        // Assert
        pendingMessages.Should().HaveCount(1);
        // Check that the remaining message has the expected correlation ID
        var remainingCorrelationIds = pendingMessages.Select(m => m.CorrelationId).ToList();
        remainingCorrelationIds.Should().Contain(correlationId2);
        remainingCorrelationIds.Should().NotContain(correlationId1);
    }

    [Fact]
    public async Task MarkAsDispatchedAsync_ShouldUpdateMessageStatus()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        await _service.AddMessageAsync("Type", "Payload", correlationId);

        var messages = await _service.GetPendingMessagesAsync();
        var messageId = messages.First().Id;

        // Act
        await _service.MarkAsDispatchedAsync(messageId);

        // Assert
        var updatedMessages = await _service.GetPendingMessagesAsync();
        updatedMessages.Should().BeEmpty();

        // Verify the message was marked as dispatched
        var allMessages = await GetAllMessagesAsync();
        var dispatchedMessage = allMessages.First(m => m.Id == messageId);
        dispatchedMessage.Status.Should().Be(OutboxStatus.Dispatched);
        dispatchedMessage.DispatchedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAsFailedAsync_ShouldIncrementRetryCountAndSetNextRetry()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        await _service.AddMessageAsync("Type", "Payload", correlationId);

        var messages = await _service.GetPendingMessagesAsync();
        var messageId = messages.First().Id;
        var errorMessage = "Connection failed";

        // Act
        await _service.MarkAsFailedAsync(messageId, errorMessage);

        // Assert
        var allMessages = await GetAllMessagesAsync();
        var failedMessage = allMessages.First(m => m.Id == messageId);
        failedMessage.Status.Should().Be(OutboxStatus.Pending); // Status remains Pending for retry
        failedMessage.RetryCount.Should().Be(1);
        failedMessage.ErrorMessage.Should().Be(errorMessage);
        failedMessage.NextRetryAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAsFailedAsync_ShouldMarkAsAbandoned_WhenMaxRetriesExceeded()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        await _service.AddMessageAsync("Type", "Payload", correlationId);

        var messages = await _service.GetPendingMessagesAsync();
        var messageId = messages.First().Id;

        // Fail the message MaxRetries + 1 times to exceed the limit
        for (int i = 0; i <= _options.MaxRetries; i++)
        {
            await _service.MarkAsFailedAsync(messageId, $"Error {i + 1}");
        }

        // Assert
        var allMessages = await GetAllMessagesAsync();
        var abandonedMessage = allMessages.First(m => m.Id == messageId);
        abandonedMessage.Status.Should().Be(OutboxStatus.Abandoned);
        abandonedMessage.RetryCount.Should().Be(_options.MaxRetries + 1); // Incremented one more time before abandoning
        abandonedMessage.AbandonAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CleanupOldMessagesAsync_ShouldRemoveOldDispatchedMessages()
    {
        // Arrange
        var correlationId1 = Guid.NewGuid().ToString();
        var correlationId2 = Guid.NewGuid().ToString();

        await _service.AddMessageAsync("Type1", "Payload1", correlationId1);
        await _service.AddMessageAsync("Type2", "Payload2", correlationId2);

        // Mark both as dispatched
        var messages = await _service.GetPendingMessagesAsync();
        foreach (var message in messages)
        {
            await _service.MarkAsDispatchedAsync(message.Id);
        }

        // Manually set one message to be old (simulate old DispatchedAt)
        var oldMessage = messages.First();
        var oldEntity = await _tableClient.GetEntityAsync<TableEntity>(oldMessage.Id, oldMessage.Id);
        oldEntity.Value["DispatchedAt"] = DateTimeOffset.Now.AddDays(-_options.CleanupRetentionDays - 1).ToString("O");
        await _tableClient.UpdateEntityAsync(oldEntity.Value, ETag.All, TableUpdateMode.Replace);

        // Act
        var deletedCount = await _service.CleanupOldMessagesAsync();

        // Assert
        deletedCount.Should().Be(1);

        var remainingMessages = await GetAllMessagesAsync();
        remainingMessages.Should().HaveCount(1);
        remainingMessages.First().Id.Should().NotBe(oldMessage.Id);
    }

    private async Task<IList<OutboxMessage>> GetAllMessagesAsync()
    {
        var query = _tableClient.QueryAsync<TableEntity>();
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
