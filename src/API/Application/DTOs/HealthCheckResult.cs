namespace LabResultsGateway.Application.DTOs;

public class HealthCheckResult
{
    public string Status { get; set; } = "Unknown";
    public DateTimeOffset Timestamp { get; set; }
    public TimeSpan TotalResponseTime { get; set; }
    public List<ComponentHealth> Components { get; set; } = new();
}
