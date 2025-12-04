using LabResultsGateway.Domain.ValueObjects;

namespace LabResultsGateway.Domain.Entities;

public class ProcessingRecord
{
    public string Id { get; set; } = string.Empty;
    public IdempotencyKey Key { get; set; } = null!;
    public ProcessingOutcome Outcome { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
