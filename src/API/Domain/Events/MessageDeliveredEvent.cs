using System;

namespace LabResultsGateway.API.Domain.Events;

/// <summary>
/// Domain event raised when a message is successfully delivered to an external endpoint.
/// </summary>
public class MessageDeliveredEvent : DomainEventBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageDeliveredEvent"/> class.
    /// </summary>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    /// <param name="externalEndpoint">The external endpoint URL where the message was delivered.</param>
    public MessageDeliveredEvent(string correlationId, string externalEndpoint)
        : base(correlationId)
    {
        ExternalEndpoint = externalEndpoint ?? throw new ArgumentNullException(nameof(externalEndpoint));
    }

    /// <summary>
    /// Gets the external endpoint URL where the message was delivered.
    /// </summary>
    public string ExternalEndpoint { get; }
}
