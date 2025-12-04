namespace LabResultsGateway.API.Domain.ValueObjects;

public record IdempotencyKey(string ContentHash, string BlobName);
