namespace LabResultsGateway.Application.Options;

public class HealthCheckOptions
{
    public int TimeoutSeconds { get; set; } = 30;
    public string BlobStorageConnection { get; set; } = string.Empty;
    public string QueueStorageConnection { get; set; } = string.Empty;
    public string MetadataApiUrl { get; set; } = string.Empty;
}
