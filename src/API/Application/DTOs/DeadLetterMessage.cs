namespace LabResultsGateway.API.Application.DTOs;

/// <summary>
/// Represents a message that has been moved to the dead letter queue after exceeding max retry attempts.
/// </summary>
public record DeadLetterMessage(
    string Hl7Message,
    string CorrelationId,
    int RetryCount,
    DateTimeOffset Timestamp,
    string BlobName,
    string FailureReason,
    DateTimeOffset LastAttempt) : QueueMessage(Hl7Message, CorrelationId, RetryCount, Timestamp, BlobName);
