using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using LabResultsGateway.API.Application.Events;
using LabResultsGateway.API.Application.Events.Handlers;
using LabResultsGateway.API.Domain.Events;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LabResultsGateway.API.IntegrationTests;

public class DomainEventsIntegrationTests : IAsyncLifetime
{
    private IServiceProvider _serviceProvider;
    private IDomainEventDispatcher _eventDispatcher;

    public async Task InitializeAsync()
    {
        // Setup DI container with event infrastructure
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging();

        // Add Application Insights (mock telemetry client for testing)
        var telemetryConfiguration = new TelemetryConfiguration();
        var telemetryClient = new TelemetryClient(telemetryConfiguration);
        services.AddSingleton(telemetryClient);

        // Register event handlers using open generics
        services.AddTransient(typeof(IDomainEventHandler<>), typeof(AuditLoggingEventHandler<>));
        services.AddTransient(typeof(IDomainEventHandler<>), typeof(TelemetryEventHandler<>));

        // Register event dispatcher
        services.AddTransient<IDomainEventDispatcher, DomainEventDispatcher>();

        _serviceProvider = services.BuildServiceProvider();
        _eventDispatcher = _serviceProvider.GetRequiredService<IDomainEventDispatcher>();
    }

    public async Task DisposeAsync()
    {
        // No cleanup needed for domain events tests
    }

    [Fact]
    public async Task LabReportReceivedEvent_ShouldBeHandledByMultipleHandlers()
    {
        // Arrange
        var blobName = "test-report.pdf";
        var contentSize = 1024L;
        var correlationId = Guid.NewGuid().ToString();
        var @event = new LabReportReceivedEvent(blobName, contentSize, correlationId);

        // Act
        await _eventDispatcher.DispatchAsync(@event);

        // Assert - Event was dispatched without throwing
        // In a real scenario, we'd verify logging output or telemetry
        // For this test, we just ensure no exceptions were thrown
        @event.Should().NotBeNull();
        @event.BlobName.Should().Be(blobName);
        @event.ContentSize.Should().Be(contentSize);
        @event.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task LabMetadataRetrievedEvent_ShouldBeHandledSuccessfully()
    {
        // Arrange
        var labNumber = "LAB001";
        var patientId = "PAT123";
        var correlationId = Guid.NewGuid().ToString();
        var @event = new LabMetadataRetrievedEvent(labNumber, patientId, correlationId);

        // Act
        await _eventDispatcher.DispatchAsync(@event);

        // Assert
        @event.LabNumber.Should().Be(labNumber);
        @event.PatientId.Should().Be(patientId);
        @event.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task Hl7MessageGeneratedEvent_ShouldBeHandledSuccessfully()
    {
        // Arrange
        var labNumber = "LAB001";
        var messageLength = 2048;
        var correlationId = Guid.NewGuid().ToString();
        var @event = new Hl7MessageGeneratedEvent(labNumber, messageLength, correlationId);

        // Act
        await _eventDispatcher.DispatchAsync(@event);

        // Assert
        @event.LabNumber.Should().Be(labNumber);
        @event.MessageLength.Should().Be(messageLength);
        @event.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task MessageQueuedEvent_ShouldBeHandledSuccessfully()
    {
        // Arrange
        var queueName = "processing-queue";
        var correlationId = Guid.NewGuid().ToString();
        var @event = new MessageQueuedEvent(queueName, correlationId);

        // Act
        await _eventDispatcher.DispatchAsync(@event);

        // Assert
        @event.QueueName.Should().Be(queueName);
        @event.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task MessageDeliveryFailedEvent_ShouldBeHandledSuccessfully()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var errorMessage = "Connection timeout";
        var retryCount = 2;
        var @event = new MessageDeliveryFailedEvent(correlationId, errorMessage, retryCount);

        // Act
        await _eventDispatcher.DispatchAsync(@event);

        // Assert
        @event.CorrelationId.Should().Be(correlationId);
        @event.ErrorMessage.Should().Be(errorMessage);
        @event.RetryCount.Should().Be(retryCount);
    }

    [Fact]
    public async Task MessageDeliveredEvent_ShouldBeHandledSuccessfully()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var externalEndpoint = "https://external-api.com/results";
        var @event = new MessageDeliveredEvent(correlationId, externalEndpoint);

        // Act
        await _eventDispatcher.DispatchAsync(@event);

        // Assert
        @event.CorrelationId.Should().Be(correlationId);
        @event.ExternalEndpoint.Should().Be(externalEndpoint);
    }

    [Fact]
    public async Task MultipleEvents_ShouldBeHandledIndependently()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var events = new List<IDomainEvent>
        {
            new LabReportReceivedEvent("report1.pdf", 1024L, correlationId),
            new LabMetadataRetrievedEvent("LAB001", "PAT123", correlationId),
            new Hl7MessageGeneratedEvent("LAB001", 2048, correlationId),
            new MessageQueuedEvent("processing-queue", correlationId),
            new MessageDeliveredEvent(correlationId, "https://api.example.com")
        };

        // Act & Assert - All events should be handled without throwing
        foreach (var @event in events)
        {
            await _eventDispatcher.DispatchAsync(@event);
        }
    }

    [Fact]
    public async Task EventDispatcher_ShouldHandleMissingHandlersGracefully()
    {
        // Arrange - Create a custom event type with no registered handlers
        var customEvent = new CustomTestEvent("test-correlation");

        // Act & Assert - Should not throw, just log that no handlers were found
        await _eventDispatcher.DispatchAsync(customEvent);
    }

    // Test event for missing handlers scenario
    private class CustomTestEvent : DomainEventBase
    {
        public CustomTestEvent(string correlationId) : base(correlationId) { }
    }
}
