using System.Threading.Tasks;
using LabResultsGateway.API.Domain.Events;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Application.Events.Handlers;

/// <summary>
/// Event handler that logs all domain events with structured data for audit purposes.
/// </summary>
public class AuditLoggingEventHandler<TEvent> : IDomainEventHandler<TEvent>
    where TEvent : IDomainEvent
{
    private readonly ILogger<AuditLoggingEventHandler<TEvent>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLoggingEventHandler{TEvent}"/> class.
    /// </summary>
    /// <param name="logger">The logger for recording audit events.</param>
    public AuditLoggingEventHandler(ILogger<AuditLoggingEventHandler<TEvent>> logger)
    {
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task HandleAsync(TEvent @event)
    {
        _logger.LogInformation(
            "Domain Event: {EventType} | CorrelationId: {CorrelationId} | Timestamp: {Timestamp:O} | Details: {@EventDetails}",
            @event.GetType().Name,
            @event.CorrelationId,
            @event.Timestamp,
            @event);

        return Task.CompletedTask;
    }
}
