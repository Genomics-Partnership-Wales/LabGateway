using Azure.Storage.Queues;
using LabResultsGateway.Application.DTOs;

namespace LabResultsGateway.API.Infrastructure.HealthChecks;

public class QueueStorageHealthCheck
{
    private readonly QueueServiceClient _queueServiceClient;

    public QueueStorageHealthCheck(string connectionString)
    {
        _queueServiceClient = new QueueServiceClient(connectionString);
    }

    public async Task<ComponentHealth> CheckAsync()
    {
        var startTime = DateTimeOffset.UtcNow;
        try
        {
            var properties = await _queueServiceClient.GetPropertiesAsync();
            var responseTime = DateTimeOffset.UtcNow - startTime;
            return new ComponentHealth
            {
                Component = "QueueStorage",
                Status = "Healthy",
                ResponseTime = responseTime
            };
        }
        catch (Exception ex)
        {
            var responseTime = DateTimeOffset.UtcNow - startTime;
            return new ComponentHealth
            {
                Component = "QueueStorage",
                Status = "Unhealthy",
                Message = ex.Message,
                ResponseTime = responseTime
            };
        }
    }
}
