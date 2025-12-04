using System.Net.Http;
using LabResultsGateway.Application.DTOs;

namespace LabResultsGateway.API.Infrastructure.HealthChecks;

public class MetadataApiHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;

    public MetadataApiHealthCheck(string apiUrl)
    {
        _httpClient = new HttpClient();
        _apiUrl = apiUrl;
    }

    public async Task<ComponentHealth> CheckAsync()
    {
        var startTime = DateTimeOffset.UtcNow;
        try
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}/health");
            var responseTime = DateTimeOffset.UtcNow - startTime;
            if (response.IsSuccessStatusCode)
            {
                return new ComponentHealth
                {
                    Component = "MetadataApi",
                    Status = "Healthy",
                    ResponseTime = responseTime
                };
            }
            else
            {
                return new ComponentHealth
                {
                    Component = "MetadataApi",
                    Status = "Unhealthy",
                    Message = $"HTTP {response.StatusCode}",
                    ResponseTime = responseTime
                };
            }
        }
        catch (Exception ex)
        {
            var responseTime = DateTimeOffset.UtcNow - startTime;
            return new ComponentHealth
            {
                Component = "MetadataApi",
                Status = "Unhealthy",
                Message = ex.Message,
                ResponseTime = responseTime
            };
        }
    }
}
