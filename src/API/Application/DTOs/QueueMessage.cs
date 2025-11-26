namespace LabResultsGateway.API.Application.DTOs;

/// <summary>
/// Represents a message in the processing queue containing HL7 content and metadata.
/// </summary>
public record QueueMessage(
    string Hl7Message,
    string CorrelationId,
    int RetryCount,
    DateTimeOffset Timestamp,
    string BlobName);
