using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using LabResultsGateway.API.Application.Services;
using System.Net;

namespace LabResultsGateway.API.Functions;

public class HealthCheckFunction
{
    private readonly ILogger<HealthCheckFunction> _logger;
    private readonly IHealthCheckService _healthCheckService;

    public HealthCheckFunction(ILogger<HealthCheckFunction> logger, IHealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
    }

    [Function("HealthCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/check")] HttpRequestData req)
    {
        _logger.LogInformation("Comprehensive health check requested");

        var result = await _healthCheckService.CheckHealthAsync();

        var response = req.CreateResponse(result.Status == "Healthy" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        await response.WriteAsJsonAsync(result);

        return response;
    }
}
