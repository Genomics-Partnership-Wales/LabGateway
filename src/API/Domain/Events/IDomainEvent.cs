using System;

namespace LabResultsGateway.API.Domain.Events;

/// <summary>
/// Marker interface for domain events.
/// Domain events represent important business occurrences that have happened within the domain.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the correlation ID for tracing the event through the system.
    /// </summary>
    string CorrelationId { get; }
}
