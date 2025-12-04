using LabResultsGateway.Application.DTOs;

namespace LabResultsGateway.Application.Services;

public interface IHealthCheckService
{
    Task<HealthCheckResult> CheckHealthAsync();
}
