using System;

namespace LabResultsGateway.API.Domain.Events;

/// <summary>
/// Abstract base class for domain events that provides common properties.
/// </summary>
public abstract class DomainEventBase : IDomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEventBase"/> class.
    /// </summary>
    /// <param name="correlationId">The correlation ID for tracing the event.</param>
    protected DomainEventBase(string correlationId)
    {
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc/>
    public string CorrelationId { get; }
}
