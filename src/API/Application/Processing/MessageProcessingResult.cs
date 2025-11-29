using LabResultsGateway.API.Application.Retry;

namespace LabResultsGateway.API.Application.Processing;

/// <summary>
/// Result of processing a poison queue message.
/// </summary>
public record MessageProcessingResult(
    bool Success,
    RetryResult Result,
    string? ErrorMessage = null);