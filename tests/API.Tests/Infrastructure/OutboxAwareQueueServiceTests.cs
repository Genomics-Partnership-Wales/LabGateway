using System;
using System.Threading.Tasks;
using FluentAssertions;
using LabResultsGateway.API.Application.DTOs;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LabResultsGateway.API.Tests.Infrastructure;

public class OutboxAwareQueueServiceTests
{
    private readonly Mock<IMessageQueueService> _innerQueueServiceMock;
    private readonly Mock<IOutboxService> _outboxServiceMock;
    private readonly Mock<ILogger<OutboxAwareQueueService>> _loggerMock;
    private readonly OutboxAwareQueueService _service;

    public OutboxAwareQueueServiceTests()
    {
        _innerQueueServiceMock = new Mock<IMessageQueueService>();
        _outboxServiceMock = new Mock<IOutboxService>();
        _loggerMock = new Mock<ILogger<OutboxAwareQueueService>>();
        _service = new OutboxAwareQueueService(
            _innerQueueServiceMock.Object,
            _outboxServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SendToProcessingQueueAsync_ShouldStoreMessageInOutboxFirst()
    {
        // Arrange
        var message = "test message";
        _innerQueueServiceMock.Setup(x => x.SendToProcessingQueueAsync(message, default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendToProcessingQueueAsync(message);

        // Assert
        _outboxServiceMock.Verify(
            x => x.AddMessageAsync("HL7Message", message, It.IsAny<string>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendToProcessingQueueAsync_ShouldDispatchToInnerService_WhenSuccessful()
    {
        // Arrange
        var message = "test message";
        _innerQueueServiceMock.Setup(x => x.SendToProcessingQueueAsync(message, default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendToProcessingQueueAsync(message);

        // Assert
        _innerQueueServiceMock.Verify(
            x => x.SendToProcessingQueueAsync(message, default),
            Times.Once);
    }

    [Fact]
    public async Task SendToProcessingQueueAsync_ShouldNotThrow_WhenInnerServiceFails()
    {
        // Arrange
        var message = "test message";
        _innerQueueServiceMock.Setup(x => x.SendToProcessingQueueAsync(message, default))
            .ThrowsAsync(new Exception("Queue service failed"));

        // Act & Assert
        var action = () => _service.SendToProcessingQueueAsync(message);
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendToProcessingQueueAsync_ShouldLogSuccess_WhenDispatchSucceeds()
    {
        // Arrange
        var message = "test message";
        _innerQueueServiceMock.Setup(x => x.SendToProcessingQueueAsync(message, default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendToProcessingQueueAsync(message);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully dispatched")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendToProcessingQueueAsync_ShouldLogWarning_WhenDispatchFails()
    {
        // Arrange
        var message = "test message";
        var exception = new Exception("Queue service failed");
        _innerQueueServiceMock.Setup(x => x.SendToProcessingQueueAsync(message, default))
            .ThrowsAsync(exception);

        // Act
        await _service.SendToProcessingQueueAsync(message);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to dispatch")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendToProcessingQueueAsync_ShouldThrowArgumentNullException_WhenMessageIsNull()
    {
        // Act & Assert
        var action = () => _service.SendToProcessingQueueAsync(null!);
        await action.Should().ThrowAsync<ArgumentNullException>().WithParameterName("message");
    }

    [Fact]
    public async Task SendToPoisonQueueAsync_ShouldDelegateToInnerService()
    {
        // Arrange
        var message = "poison message";
        var retryCount = 3;
        _innerQueueServiceMock.Setup(x => x.SendToPoisonQueueAsync(message, retryCount, default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendToPoisonQueueAsync(message, retryCount);

        // Assert
        _innerQueueServiceMock.Verify(
            x => x.SendToPoisonQueueAsync(message, retryCount, default),
            Times.Once);
    }

    [Fact]
    public async Task DeserializeMessageAsync_ShouldDelegateToInnerService()
    {
        // Arrange
        var message = "serialized message";
        var expectedQueueMessage = new QueueMessage(
            "HL7 message content",
            "correlation-123",
            0,
            DateTimeOffset.Now,
            "test-blob.pdf");
        _innerQueueServiceMock.Setup(x => x.DeserializeMessageAsync(message))
            .ReturnsAsync(expectedQueueMessage);

        // Act
        var result = await _service.DeserializeMessageAsync(message);

        // Assert
        result.Should().Be(expectedQueueMessage);
        _innerQueueServiceMock.Verify(x => x.DeserializeMessageAsync(message), Times.Once);
    }

    [Fact]
    public async Task SerializeMessageAsync_ShouldDelegateToInnerService()
    {
        // Arrange
        var queueMessage = new QueueMessage(
            "HL7 message content",
            "correlation-456",
            1,
            DateTimeOffset.Now,
            "test-blob2.pdf");
        var expectedSerializedMessage = "serialized";
        _innerQueueServiceMock.Setup(x => x.SerializeMessageAsync(queueMessage))
            .ReturnsAsync(expectedSerializedMessage);

        // Act
        var result = await _service.SerializeMessageAsync(queueMessage);

        // Assert
        result.Should().Be(expectedSerializedMessage);
        _innerQueueServiceMock.Verify(x => x.SerializeMessageAsync(queueMessage), Times.Once);
    }

    [Fact]
    public async Task SendToDeadLetterQueueAsync_ShouldDelegateToInnerService()
    {
        // Arrange
        var deadLetterMessage = new DeadLetterMessage(
            "HL7 message content",
            "correlation-789",
            3,
            DateTimeOffset.Now,
            "test-blob3.pdf",
            "Max retries exceeded",
            DateTimeOffset.Now);
        _innerQueueServiceMock.Setup(x => x.SendToDeadLetterQueueAsync(deadLetterMessage, default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendToDeadLetterQueueAsync(deadLetterMessage);

        // Assert
        _innerQueueServiceMock.Verify(
            x => x.SendToDeadLetterQueueAsync(deadLetterMessage, default),
            Times.Once);
    }
}
