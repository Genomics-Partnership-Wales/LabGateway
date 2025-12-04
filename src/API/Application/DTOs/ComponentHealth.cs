namespace LabResultsGateway.Application.DTOs;

public class ComponentHealth
{
    public string Component { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string? Message { get; set; }
    public TimeSpan? ResponseTime { get; set; }
}
