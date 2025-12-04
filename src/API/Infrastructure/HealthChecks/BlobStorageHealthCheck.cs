using Azure.Storage.Blobs;
using LabResultsGateway.Application.DTOs;

namespace LabResultsGateway.Infrastructure.HealthChecks;

public class BlobStorageHealthCheck
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageHealthCheck(string connectionString)
    {
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<ComponentHealth> CheckAsync()
    {
        var startTime = DateTimeOffset.UtcNow;
        try
        {
            var properties = await _blobServiceClient.GetPropertiesAsync();
            var responseTime = DateTimeOffset.UtcNow - startTime;
            return new ComponentHealth
            {
                Component = "BlobStorage",
                Status = "Healthy",
                ResponseTime = responseTime
            };
        }
        catch (Exception ex)
        {
            var responseTime = DateTimeOffset.UtcNow - startTime;
            return new ComponentHealth
            {
                Component = "BlobStorage",
                Status = "Unhealthy",
                Message = ex.Message,
                ResponseTime = responseTime
            };
        }
    }
}
