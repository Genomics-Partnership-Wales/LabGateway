using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LabResultsGateway.API.Domain.Events;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Application.Events.Handlers;

/// <summary>
/// Event handler that records domain events as Application Insights custom events.
/// </summary>
public class TelemetryEventHandler<TEvent> : IDomainEventHandler<TEvent>
    where TEvent : IDomainEvent
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<TelemetryEventHandler<TEvent>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetryEventHandler{TEvent}"/> class.
    /// </summary>
    /// <param name="telemetryClient">The Application Insights telemetry client.</param>
    /// <param name="logger">The logger for recording telemetry operations.</param>
    public TelemetryEventHandler(TelemetryClient telemetryClient, ILogger<TelemetryEventHandler<TEvent>> logger)
    {
        _telemetryClient = telemetryClient ?? throw new System.ArgumentNullException(nameof(telemetryClient));
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task HandleAsync(TEvent @event)
    {
        try
        {
            var properties = new Dictionary<string, string>
            {
                ["EventType"] = @event.GetType().Name,
                ["CorrelationId"] = @event.CorrelationId,
                ["Timestamp"] = @event.Timestamp.ToString("O")
            };

            // Add event-specific properties
            switch (@event)
            {
                case LabReportReceivedEvent labEvent:
                    properties["BlobName"] = labEvent.BlobName;
                    properties["ContentSize"] = labEvent.ContentSize.ToString();
                    break;

                case LabMetadataRetrievedEvent metaEvent:
                    properties["LabNumber"] = metaEvent.LabNumber;
                    properties["PatientId"] = metaEvent.PatientId;
                    break;

                case Hl7MessageGeneratedEvent hl7Event:
                    properties["LabNumber"] = hl7Event.LabNumber;
                    properties["MessageLength"] = hl7Event.MessageLength.ToString();
                    break;

                case MessageQueuedEvent queueEvent:
                    properties["QueueName"] = queueEvent.QueueName;
                    break;

                case MessageDeliveryFailedEvent failedEvent:
                    properties["ErrorMessage"] = failedEvent.ErrorMessage;
                    properties["RetryCount"] = failedEvent.RetryCount.ToString();
                    break;

                case MessageDeliveredEvent deliveredEvent:
                    properties["ExternalEndpoint"] = deliveredEvent.ExternalEndpoint;
                    break;
            }

            _telemetryClient.TrackEvent(@event.GetType().Name, properties);

            _logger.LogInformation("Recorded telemetry event: {EventType} for correlation ID: {CorrelationId}",
                @event.GetType().Name, @event.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record telemetry for event {EventType}",
                @event.GetType().Name);
        }

        return Task.CompletedTask;
    }
}
