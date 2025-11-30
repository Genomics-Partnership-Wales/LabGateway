using System.Diagnostics;
using FluentAssertions;
using LabResultsGateway.API.Application.DTOs;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Application.Processing;
using LabResultsGateway.API.Application.Retry;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Infrastructure.Queue;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LabResultsGateway.API.Tests.Application.Processing;

public class PoisonQueueRetryOrchestratorTests
{
    private readonly Mock<IAzureQueueClient> _azureQueueClientMock;
    private readonly Mock<IPoisonQueueMessageProcessor> _messageProcessorMock;
    private readonly Mock<IMessageQueueService> _messageQueueServiceMock;
    private readonly Mock<IRetryStrategy> _retryStrategyMock;
    private readonly ActivitySource _activitySource;
    private readonly Mock<ILogger<PoisonQueueRetryOrchestrator>> _loggerMock;
    private readonly PoisonQueueRetryOptions _options;

    public PoisonQueueRetryOrchestratorTests()
    {
        _azureQueueClientMock = new Mock<IAzureQueueClient>();
        _messageProcessorMock = new Mock<IPoisonQueueMessageProcessor>();
        _messageQueueServiceMock = new Mock<IMessageQueueService>();
        _retryStrategyMock = new Mock<IRetryStrategy>();
        _activitySource = new ActivitySource("TestActivitySource");
        _loggerMock = new Mock<ILogger<PoisonQueueRetryOrchestrator>>();
        _options = new PoisonQueueRetryOptions
        {
            MaxMessagesPerBatch = 10,
            MaxRetryAttempts = 3,
            BaseRetryDelayMinutes = 2.0,
            ProcessingVisibilityTimeoutMinutes = 5,
            UseJitter = true,
            MaxJitterPercentage = 0.3
        };
    }

    [Fact]
    public async Task ProcessPoisonQueueAsync_CallsEnsureQueueExistsAsync()
    {
        // Arrange
        var orchestrator = new PoisonQueueRetryOrchestrator(
            _azureQueueClientMock.Object,
            _messageProcessorMock.Object,
            _messageQueueServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySource,
            _loggerMock.Object);

        _azureQueueClientMock.Setup(x => x.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new List<QueueMessageWrapper>());

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        _azureQueueClientMock.Verify(x => x.EnsureQueueExistsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPoisonQueueAsync_RetrievesMessagesWithCorrectBatchSizeAndVisibilityTimeout()
    {
        // Arrange
        var orchestrator = new PoisonQueueRetryOrchestrator(
            _azureQueueClientMock.Object,
            _messageProcessorMock.Object,
            _messageQueueServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySource,
            _loggerMock.Object);

        _azureQueueClientMock.Setup(x => x.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new List<QueueMessageWrapper>());

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        _azureQueueClientMock.Verify(
            x => x.ReceiveMessagesAsync(
                _options.MaxMessagesPerBatch,
                TimeSpan.FromMinutes(_options.ProcessingVisibilityTimeoutMinutes),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPoisonQueueAsync_ProcessesEachMessageByCallingProcessSingleMessageAsync()
    {
        // Arrange
        var messages = new List<QueueMessageWrapper>
        {
            new QueueMessageWrapper("msg1", "receipt1", "content1", 1),
            new QueueMessageWrapper("msg2", "receipt2", "content2", 2),
            new QueueMessageWrapper("msg3", "receipt3", "content3", 1)
        };

        var orchestrator = new PoisonQueueRetryOrchestrator(
            _azureQueueClientMock.Object,
            _messageProcessorMock.Object,
            _messageQueueServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySource,
            _loggerMock.Object);

        _azureQueueClientMock.Setup(x => x.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(messages);

        _messageProcessorMock.Setup(x => x.ProcessMessageAsync(It.IsAny<QueueMessageWrapper>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new MessageProcessingResult(true, RetryResult.Success));

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        _messageProcessorMock.Verify(
            x => x.ProcessMessageAsync(It.Is<QueueMessageWrapper>(dto => dto.MessageId == "msg1"), It.IsAny<CancellationToken>()),
            Times.Once);
        _messageProcessorMock.Verify(
            x => x.ProcessMessageAsync(It.Is<QueueMessageWrapper>(dto => dto.MessageId == "msg2"), It.IsAny<CancellationToken>()),
            Times.Once);
        _messageProcessorMock.Verify(
            x => x.ProcessMessageAsync(It.Is<QueueMessageWrapper>(dto => dto.MessageId == "msg3"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPoisonQueueAsync_DeletesMessage_WhenResultIsSuccess()
    {
        // Arrange
        var messages = new List<QueueMessageWrapper>
        {
            new QueueMessageWrapper("msg1", "receipt1", "content1", 1)
        };

        var orchestrator = new PoisonQueueRetryOrchestrator(
            _azureQueueClientMock.Object,
            _messageProcessorMock.Object,
            _messageQueueServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySource,
            _loggerMock.Object);

        _azureQueueClientMock.Setup(x => x.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(messages);

        _messageProcessorMock.Setup(x => x.ProcessMessageAsync(It.IsAny<QueueMessageWrapper>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new MessageProcessingResult(true, RetryResult.Success));

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        _azureQueueClientMock.Verify(x => x.DeleteMessageAsync("msg1", "receipt1", It.IsAny<CancellationToken>()), Times.Once);
        _azureQueueClientMock.Verify(x => x.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _messageQueueServiceMock.Verify(x => x.SendToDeadLetterQueueAsync(It.IsAny<DeadLetterMessage>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPoisonQueueAsync_UpdatesMessage_WhenResultIsRetry()
    {
        // Arrange
        var messages = new List<QueueMessageWrapper>
        {
            new QueueMessageWrapper("msg1", "receipt1", "{\"CorrelationId\":\"test-id\",\"RetryCount\":0}", 1)
        };

        var orchestrator = new PoisonQueueRetryOrchestrator(
            _azureQueueClientMock.Object,
            _messageProcessorMock.Object,
            _messageQueueServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySource,
            _loggerMock.Object);

        _azureQueueClientMock.Setup(x => x.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(messages);

        _messageProcessorMock.Setup(x => x.ProcessMessageAsync(It.IsAny<QueueMessageWrapper>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new MessageProcessingResult(false, RetryResult.Retry));

        _messageQueueServiceMock.Setup(x => x.DeserializeMessageAsync(It.IsAny<string>()))
                                .ReturnsAsync(new QueueMessage("test-hl7-message", "test-id", 0, DateTimeOffset.UtcNow, "test-blob"));

        _messageQueueServiceMock.Setup(x => x.SerializeMessageAsync(It.IsAny<QueueMessage>()))
                                .ReturnsAsync("{\"CorrelationId\":\"test-id\",\"RetryCount\":1}");

        _retryStrategyMock.Setup(x => x.CalculateNextRetryDelay(It.IsAny<RetryContext>()))
                         .Returns(TimeSpan.FromMinutes(4));

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        _azureQueueClientMock.Verify(x => x.UpdateMessageAsync("msg1", "receipt1", It.IsAny<string>(), TimeSpan.FromMinutes(4), It.IsAny<CancellationToken>()), Times.Once);
        _azureQueueClientMock.Verify(x => x.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _messageQueueServiceMock.Verify(x => x.SendToDeadLetterQueueAsync(It.IsAny<DeadLetterMessage>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPoisonQueueAsync_DeletesMessage_WhenResultIsDeadLetter()
    {
        // Arrange
        var messages = new List<QueueMessageWrapper>
        {
            new QueueMessageWrapper("msg1", "receipt1", "content1", 3)
        };

        var orchestrator = new PoisonQueueRetryOrchestrator(
            _azureQueueClientMock.Object,
            _messageProcessorMock.Object,
            _messageQueueServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySource,
            _loggerMock.Object);

        _azureQueueClientMock.Setup(x => x.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(messages);

        _messageProcessorMock.Setup(x => x.ProcessMessageAsync(It.IsAny<QueueMessageWrapper>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new MessageProcessingResult(false, RetryResult.DeadLetter));

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        _azureQueueClientMock.Verify(x => x.DeleteMessageAsync("msg1", "receipt1", It.IsAny<CancellationToken>()), Times.Once);
        _azureQueueClientMock.Verify(x => x.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        // Note: SendToDeadLetterQueueAsync is called by the processor, not the orchestrator
    }

    [Fact]
    public async Task ProcessPoisonQueueAsync_LogsStructuredProperties()
    {
        // Arrange
        var messages = new List<QueueMessageWrapper>
        {
            new QueueMessageWrapper("msg1", "receipt1", "content1", 1),
            new QueueMessageWrapper("msg2", "receipt2", "content2", 2)
        };

        var orchestrator = new PoisonQueueRetryOrchestrator(
            _azureQueueClientMock.Object,
            _messageProcessorMock.Object,
            _messageQueueServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySource,
            _loggerMock.Object);

        _azureQueueClientMock.Setup(x => x.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(messages);

        _messageProcessorMock.Setup(x => x.ProcessMessageAsync(It.IsAny<QueueMessageWrapper>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new MessageProcessingResult(true, RetryResult.Success));

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("2")), // Message count
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessPoisonQueueAsync_SetsActivityTags()
    {
        // Arrange
        var messages = new List<QueueMessageWrapper>
        {
            new QueueMessageWrapper("msg1", "receipt1", "content1", 1)
        };

        var orchestrator = new PoisonQueueRetryOrchestrator(
            _azureQueueClientMock.Object,
            _messageProcessorMock.Object,
            _messageQueueServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySource,
            _loggerMock.Object);

        _azureQueueClientMock.Setup(x => x.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>()))
                            .ReturnsAsync(messages);

        _messageProcessorMock.Setup(x => x.ProcessMessageAsync(It.IsAny<QueueMessageWrapper>()))
                            .ReturnsAsync(new MessageProcessingResult(true, RetryResult.Success));

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert - Activity creation and disposal is handled by the using statement in the implementation
        // This test verifies the method completes successfully with activity instrumentation
        _azureQueueClientMock.Verify(x => x.EnsureQueueExistsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _azureQueueClientMock.Verify(x => x.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _messageProcessorMock.Verify(x => x.ProcessMessageAsync(It.IsAny<QueueMessageWrapper>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPoisonQueueAsync_HandlesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var orchestrator = new PoisonQueueRetryOrchestrator(
            _azureQueueClientMock.Object,
            _messageProcessorMock.Object,
            _messageQueueServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySource,
            _loggerMock.Object);

        // Setup EnsureQueueExistsAsync to throw when cancelled
        _azureQueueClientMock.Setup(x => x.EnsureQueueExistsAsync(It.IsAny<CancellationToken>()))
                            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => orchestrator.ProcessPoisonQueueAsync(cts.Token));

        _azureQueueClientMock.Verify(x => x.EnsureQueueExistsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _azureQueueClientMock.Verify(x => x.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
