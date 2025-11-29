namespace LabResultsGateway.API.Application.Retry;

/// <summary>
/// Result of a retry operation.
/// </summary>
public enum RetryResult
{
    /// <summary>
    /// Operation succeeded.
    /// </summary>
    Success,

    /// <summary>
    /// Operation failed but should be retried.
    /// </summary>
    Retry,

    /// <summary>
    /// Operation failed and should be sent to dead letter queue.
    /// </summary>
    DeadLetter
}