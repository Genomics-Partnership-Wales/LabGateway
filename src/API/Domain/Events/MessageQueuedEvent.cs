using System;

namespace LabResultsGateway.API.Domain.Events;

/// <summary>
/// Domain event raised when a message is successfully queued for processing.
/// </summary>
public class MessageQueuedEvent : DomainEventBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueuedEvent"/> class.
    /// </summary>
    /// <param name="queueName">The name of the queue where the message was placed.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    public MessageQueuedEvent(string queueName, string correlationId)
        : base(correlationId)
    {
        QueueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
    }

    /// <summary>
    /// Gets the name of the queue where the message was placed.
    /// </summary>
    public string QueueName { get; }
}
