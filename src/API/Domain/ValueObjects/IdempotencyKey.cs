namespace LabResultsGateway.Domain.ValueObjects;

public record IdempotencyKey(string ContentHash, string BlobName);
