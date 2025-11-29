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

public class PoisonQueueMessageProcessorTests
{
    private readonly Mock<IMessageQueueService> _messageQueueServiceMock;
    private readonly Mock<IExternalEndpointService> _externalEndpointServiceMock;
    private readonly Mock<IRetryStrategy> _retryStrategyMock;
    private readonly Mock<ActivitySource> _activitySourceMock;
    private readonly Mock<ILogger<PoisonQueueMessageProcessor>> _loggerMock;
    private readonly PoisonQueueRetryOptions _options;

    public PoisonQueueMessageProcessorTests()
    {
        _messageQueueServiceMock = new Mock<IMessageQueueService>();
        _externalEndpointServiceMock = new Mock<IExternalEndpointService>();
        _retryStrategyMock = new Mock<IRetryStrategy>();
        _activitySourceMock = new Mock<ActivitySource>();
        _loggerMock = new Mock<ILogger<PoisonQueueMessageProcessor>>();
        _options = new PoisonQueueRetryOptions
        {
            MaxRetryAttempts = 3,
            BaseRetryDelayMinutes = 2.0,
            UseJitter = true,
            MaxJitterPercentage = 0.3
        };
    }

    [Fact]
    public async Task ProcessMessageAsync_ReturnsDeadLetter_WhenShouldRetryReturnsFalse()
    {
        // Arrange
        var processor = new PoisonQueueMessageProcessor(
            _messageQueueServiceMock.Object,
            _externalEndpointServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySourceMock.Object,
            _loggerMock.Object);

        var message = new QueueMessageWrapper(
            "test-message-id",
            "test-pop-receipt",
            "{\"Hl7Message\":\"MSH|^~\\\\&|...\",\"CorrelationId\":\"test-correlation\",\"RetryCount\":3,\"Timestamp\":\"2024-01-01T00:00:00Z\",\"BlobName\":\"test-blob\"}",
            3);

        var expectedQueueMessage = new QueueMessage(
            "MSH|^~\\&|...",
            "test-correlation",
            3,
            DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            "test-blob");

        _messageQueueServiceMock.Setup(x => x.DeserializeMessageAsync(message.MessageText))
                               .ReturnsAsync(expectedQueueMessage);

        var context = new RetryContext(expectedQueueMessage.CorrelationId, expectedQueueMessage.RetryCount, _options.MaxRetryAttempts);
        _retryStrategyMock.Setup(x => x.ShouldRetry(context)).Returns(false);

        // Act
        var result = await processor.ProcessMessageAsync(message);

        // Assert
        result.Success.Should().BeFalse();
        result.Result.Should().Be(RetryResult.DeadLetter);
        result.ErrorMessage.Should().BeNull();

        _messageQueueServiceMock.Verify(
            x => x.SendToDeadLetterQueueAsync(It.Is<DeadLetterMessage>(
                dlm => dlm.CorrelationId == "test-correlation" &&
                       dlm.RetryCount == 3)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_ReturnsSuccess_WhenPostHl7MessageAsyncReturnsTrue()
    {
        // Arrange
        var processor = new PoisonQueueMessageProcessor(
            _messageQueueServiceMock.Object,
            _externalEndpointServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySourceMock.Object,
            _loggerMock.Object);

        var message = new QueueMessageWrapper(
            "test-message-id",
            "test-pop-receipt",
            "{\"Hl7Message\":\"MSH|^~\\\\&|...\",\"CorrelationId\":\"test-correlation\",\"RetryCount\":1,\"Timestamp\":\"2024-01-01T00:00:00Z\",\"BlobName\":\"test-blob\"}",
            1);

        var context = new RetryContext(message.CorrelationId, message.RetryCount, _options.MaxRetryAttempts);
        _retryStrategyMock.Setup(x => x.ShouldRetry(context)).Returns(true);
        _externalEndpointServiceMock.Setup(x => x.PostHl7MessageAsync(message.Hl7Message)).ReturnsAsync(true);

        // Act
        var result = await processor.ProcessMessageAsync(message);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Should().Be(RetryResult.Success);
        result.ErrorMessage.Should().BeNull();

        _externalEndpointServiceMock.Verify(x => x.PostHl7MessageAsync(message.Hl7Message), Times.Once);
        _messageQueueServiceMock.Verify(x => x.SendToDeadLetterQueueAsync(It.IsAny<DeadLetterMessage>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_ReturnsRetry_WhenPostHl7MessageAsyncReturnsFalse()
    {
        // Arrange
        var processor = new PoisonQueueMessageProcessor(
            _messageQueueServiceMock.Object,
            _externalEndpointServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySourceMock.Object,
            _loggerMock.Object);

        var message = new QueueMessageWrapper(
            "test-message-id",
            "test-pop-receipt",
            "{\"Hl7Message\":\"MSH|^~\\\\&|...\",\"CorrelationId\":\"test-correlation\",\"RetryCount\":1,\"Timestamp\":\"2024-01-01T00:00:00Z\",\"BlobName\":\"test-blob\"}",
            1);

        var context = new RetryContext(message.CorrelationId, message.RetryCount, _options.MaxRetryAttempts);
        _retryStrategyMock.Setup(x => x.ShouldRetry(context)).Returns(true);
        _externalEndpointServiceMock.Setup(x => x.PostHl7MessageAsync(message.Hl7Message)).ReturnsAsync(false);

        // Act
        var result = await processor.ProcessMessageAsync(message);

        // Assert
        result.Success.Should().BeFalse();
        result.Result.Should().Be(RetryResult.Retry);
        result.ErrorMessage.Should().BeNull();

        _externalEndpointServiceMock.Verify(x => x.PostHl7MessageAsync(message.Hl7Message), Times.Once);
        _messageQueueServiceMock.Verify(x => x.SendToDeadLetterQueueAsync(It.IsAny<DeadLetterMessage>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_ReturnsDeadLetter_WhenDeserializationFails()
    {
        // Arrange
        var processor = new PoisonQueueMessageProcessor(
            _messageQueueServiceMock.Object,
            _externalEndpointServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySourceMock.Object,
            _loggerMock.Object);

        var invalidMessage = new QueueMessageWrapper(
            "test-message-id",
            "test-pop-receipt",
            "INVALID_JSON",
            1);

        // Act
        var result = await processor.ProcessMessageAsync(invalidMessage);

        // Assert
        result.Success.Should().BeFalse();
        result.Result.Should().Be(RetryResult.DeadLetter);
        result.ErrorMessage.Should().NotBeNull();

        _messageQueueServiceMock.Verify(
            x => x.SendToDeadLetterQueueAsync(It.Is<DeadLetterMessage>(
                dlm => dlm.MessageId == "test-message-id")),
            Times.Once);

        _externalEndpointServiceMock.Verify(x => x.PostHl7MessageAsync(It.IsAny<string>()), Times.Never);
        _retryStrategyMock.Verify(x => x.ShouldRetry(It.IsAny<RetryContext>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_LogsStructuredProperties()
    {
        // Arrange
        var processor = new PoisonQueueMessageProcessor(
            _messageQueueServiceMock.Object,
            _externalEndpointServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySourceMock.Object,
            _loggerMock.Object);

        var message = new QueueMessageWrapper(
            "test-message-456",
            "test-pop-receipt",
            "{\"Hl7Message\":\"MSH|^~\\\\&|...\",\"CorrelationId\":\"test-correlation-123\",\"RetryCount\":2,\"Timestamp\":\"2024-01-01T00:00:00Z\",\"BlobName\":\"test-blob\"}",
            2);

        var context = new RetryContext(message.CorrelationId, message.RetryCount, _options.MaxRetryAttempts);
        _retryStrategyMock.Setup(x => x.ShouldRetry(context)).Returns(true);
        _externalEndpointServiceMock.Setup(x => x.PostHl7MessageAsync(message.Hl7Message)).ReturnsAsync(true);

        // Act
        await processor.ProcessMessageAsync(message);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("test-correlation-123") &&
                                               o.ToString()!.Contains("2") &&
                                               o.ToString()!.Contains("test-message-456")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_SetsActivityTags()
    {
        // Arrange
        var activity = new Mock<Activity>();
        _activitySourceMock.Setup(x => x.StartActivity("ProcessPoisonQueueMessage", ActivityKind.Internal))
                          .Returns(activity.Object);

        var processor = new PoisonQueueMessageProcessor(
            _messageQueueServiceMock.Object,
            _externalEndpointServiceMock.Object,
            _retryStrategyMock.Object,
            _options,
            _activitySourceMock.Object,
            _loggerMock.Object);

        var message = new QueueMessageWrapper(
            "test-message-id",
            "test-pop-receipt",
            "{\"Hl7Message\":\"MSH|^~\\\\&|...\",\"CorrelationId\":\"test-correlation\",\"RetryCount\":1,\"Timestamp\":\"2024-01-01T00:00:00Z\",\"BlobName\":\"test-blob\"}",
            1);

        var context = new RetryContext(message.CorrelationId, message.RetryCount, _options.MaxRetryAttempts);
        _retryStrategyMock.Setup(x => x.ShouldRetry(context)).Returns(true);
        _externalEndpointServiceMock.Setup(x => x.PostHl7MessageAsync(message.Hl7Message)).ReturnsAsync(true);

        // Act
        await processor.ProcessMessageAsync(message);

        // Assert
        activity.VerifySet(x => x.SetTag("correlation.id", "test-correlation"));
        activity.VerifySet(x => x.SetTag("retry.count", 1));
        activity.VerifySet(x => x.SetTag("message.id", "test-message-id"));
        activity.Verify(x => x.SetStatus(ActivityStatusCode.Ok), Times.Once);
        activity.Verify(x => x.Dispose(), Times.Once);
    }
}
