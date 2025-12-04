using System;
using FluentAssertions;
using LabResultsGateway.API.Domain.Events;
using Xunit;

namespace LabResultsGateway.API.Tests.Domain.Events;

public class DomainEventTests
{
    [Fact]
    public void LabReportReceivedEvent_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var blobName = "test-blob.pdf";
        var contentSize = 1024L;
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var @event = new LabReportReceivedEvent(blobName, contentSize, correlationId);

        // Assert
        @event.BlobName.Should().Be(blobName);
        @event.ContentSize.Should().Be(contentSize);
        @event.CorrelationId.Should().Be(correlationId);
        @event.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LabReportReceivedEvent_ShouldThrowArgumentNullException_WhenBlobNameIsNull()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert
        var action = () => new LabReportReceivedEvent(null!, 1024L, correlationId);
        action.Should().Throw<ArgumentNullException>().WithParameterName("blobName");
    }

    [Fact]
    public void LabMetadataRetrievedEvent_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var labNumber = "LAB001";
        var patientId = "PAT123";
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var @event = new LabMetadataRetrievedEvent(labNumber, patientId, correlationId);

        // Assert
        @event.LabNumber.Should().Be(labNumber);
        @event.PatientId.Should().Be(patientId);
        @event.CorrelationId.Should().Be(correlationId);
        @event.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Hl7MessageGeneratedEvent_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var labNumber = "LAB001";
        var messageLength = 2048;
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var @event = new Hl7MessageGeneratedEvent(labNumber, messageLength, correlationId);

        // Assert
        @event.LabNumber.Should().Be(labNumber);
        @event.MessageLength.Should().Be(messageLength);
        @event.CorrelationId.Should().Be(correlationId);
        @event.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MessageQueuedEvent_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var queueName = "processing-queue";
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var @event = new MessageQueuedEvent(queueName, correlationId);

        // Assert
        @event.QueueName.Should().Be(queueName);
        @event.CorrelationId.Should().Be(correlationId);
        @event.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MessageDeliveryFailedEvent_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var errorMessage = "Connection timeout";
        var retryCount = 2;

        // Act
        var @event = new MessageDeliveryFailedEvent(correlationId, errorMessage, retryCount);

        // Assert
        @event.CorrelationId.Should().Be(correlationId);
        @event.ErrorMessage.Should().Be(errorMessage);
        @event.RetryCount.Should().Be(retryCount);
        @event.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MessageDeliveredEvent_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var externalEndpoint = "https://external-api.com/results";

        // Act
        var @event = new MessageDeliveredEvent(correlationId, externalEndpoint);

        // Assert
        @event.CorrelationId.Should().Be(correlationId);
        @event.ExternalEndpoint.Should().Be(externalEndpoint);
        @event.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DomainEventBase_ShouldThrowArgumentNullException_WhenCorrelationIdIsNull()
    {
        // This test verifies the base class validation
        // We'll test through a concrete implementation
        var action = () => new LabReportReceivedEvent("test.pdf", 1024L, null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("correlationId");
    }
}
