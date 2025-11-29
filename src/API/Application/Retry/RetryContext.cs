namespace LabResultsGateway.API.Application.Retry;

/// <summary>
/// Context information for retry operations.
/// </summary>
public record RetryContext(
    string CorrelationId,
    int CurrentRetryCount,
    int MaxRetryAttempts);