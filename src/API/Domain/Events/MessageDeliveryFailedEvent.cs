using System;

namespace LabResultsGateway.API.Domain.Events;

/// <summary>
/// Domain event raised when message delivery to an external endpoint fails.
/// </summary>
public class MessageDeliveryFailedEvent : DomainEventBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageDeliveryFailedEvent"/> class.
    /// </summary>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="retryCount">The number of retry attempts made.</param>
    public MessageDeliveryFailedEvent(string correlationId, string errorMessage, int retryCount)
        : base(correlationId)
    {
        ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        RetryCount = retryCount;
    }

    /// <summary>
    /// Gets the error message describing the failure.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Gets the number of retry attempts made.
    /// </summary>
    public int RetryCount { get; }
}
