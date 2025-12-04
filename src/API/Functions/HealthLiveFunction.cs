using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace LabResultsGateway.API.Functions;

public class HealthLiveFunction
{
    private readonly ILogger<HealthLiveFunction> _logger;

    public HealthLiveFunction(ILogger<HealthLiveFunction> logger)
    {
        _logger = logger;
    }

    [Function("HealthLive")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/live")] HttpRequestData req)
    {
        _logger.LogInformation("Liveness health check requested");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            status = "alive",
            timestamp = DateTimeOffset.UtcNow,
            service = "LabResultsGateway.API"
        });

        return response;
    }
}
