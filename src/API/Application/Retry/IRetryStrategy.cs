namespace LabResultsGateway.API.Application.Retry;

/// <summary>
/// Context information for retry operations.
/// </summary>
public record RetryContext(
    string CorrelationId,
    int CurrentRetryCount,
    int MaxRetryAttempts);

/// <summary>
/// Result of a retry operation.
/// </summary>
public enum RetryResult
{
    /// <summary>
    /// The operation succeeded.
    /// </summary>
    Success,

    /// <summary>
    /// The operation failed but should be retried.
    /// </summary>
    Retry,

    /// <summary>
    /// The operation failed and should be sent to dead letter queue.
    /// </summary>
    DeadLetter
}

/// <summary>
/// Interface for retry strategy implementations.
/// </summary>
public interface IRetryStrategy
{
    /// <summary>
    /// Determines whether a retry should be attempted based on the current context.
    /// </summary>
    /// <param name="context">The retry context.</param>
    /// <returns>True if retry should be attempted, false otherwise.</returns>
    bool ShouldRetry(RetryContext context);

    /// <summary>
    /// Calculates the next retry delay based on the current context.
    /// </summary>
    /// <param name="context">The retry context.</param>
    /// <returns>The delay before the next retry attempt.</returns>
    TimeSpan CalculateNextRetryDelay(RetryContext context);
}
