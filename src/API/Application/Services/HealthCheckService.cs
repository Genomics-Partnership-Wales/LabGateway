using LabResultsGateway.Application.DTOs;
using LabResultsGateway.Application.Options;
using LabResultsGateway.API.Infrastructure.HealthChecks;
using Microsoft.Extensions.Options;

namespace LabResultsGateway.Application.Services;

public class HealthCheckService : IHealthCheckService
{
    private readonly HealthCheckOptions _options;
    private readonly BlobStorageHealthCheck _blobCheck;
    private readonly QueueStorageHealthCheck _queueCheck;
    private readonly MetadataApiHealthCheck _apiCheck;

    public HealthCheckService(
        IOptions<HealthCheckOptions> options,
        BlobStorageHealthCheck blobCheck,
        QueueStorageHealthCheck queueCheck,
        MetadataApiHealthCheck apiCheck)
    {
        _options = options.Value;
        _blobCheck = blobCheck;
        _queueCheck = queueCheck;
        _apiCheck = apiCheck;
    }

    public async Task<HealthCheckResult> CheckHealthAsync()
    {
        var startTime = DateTimeOffset.UtcNow;
        var components = new List<ComponentHealth>();

        // Run checks in parallel
        var tasks = new[]
        {
            _blobCheck.CheckAsync(),
            _queueCheck.CheckAsync(),
            _apiCheck.CheckAsync()
        };

        var results = await Task.WhenAll(tasks);
        components.AddRange(results);

        var totalResponseTime = DateTimeOffset.UtcNow - startTime;

        // Determine overall status
        var unhealthyCount = components.Count(c => c.Status == "Unhealthy");
        var status = unhealthyCount == 0 ? "Healthy" : 
                    unhealthyCount == components.Count ? "Unhealthy" : "Degraded";

        return new HealthCheckResult
        {
            Status = status,
            Timestamp = DateTimeOffset.UtcNow,
            TotalResponseTime = totalResponseTime,
            Components = components
        };
    }
}
