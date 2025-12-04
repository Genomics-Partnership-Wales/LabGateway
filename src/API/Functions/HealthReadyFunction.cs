using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using LabResultsGateway.Application.Services;
using System.Net;

namespace LabResultsGateway.API.Functions;

public class HealthReadyFunction
{
    private readonly ILogger<HealthReadyFunction> _logger;
    private readonly IHealthCheckService _healthCheckService;

    public HealthReadyFunction(ILogger<HealthReadyFunction> logger, IHealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
    }

    [Function("HealthReady")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/ready")] HttpRequestData req)
    {
        _logger.LogInformation("Readiness health check requested");

        var result = await _healthCheckService.CheckHealthAsync();

        // For readiness, consider Degraded as ready (can accept traffic)
        var isReady = result.Status == "Healthy" || result.Status == "Degraded";
        var response = req.CreateResponse(isReady ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        await response.WriteAsJsonAsync(result);

        return response;
    }
}
