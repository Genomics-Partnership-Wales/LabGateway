using LabResultsGateway.Application.DTOs;

namespace LabResultsGateway.API.Application.Services;

public interface IHealthCheckService
{
    Task<HealthCheckResult> CheckHealthAsync();
}
