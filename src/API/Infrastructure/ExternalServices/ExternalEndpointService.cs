using LabResultsGateway.API.Application.Services;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Infrastructure.ExternalServices;

/// <summary>
/// Service for posting HL7 messages to the external NHS Wales endpoint.
/// </summary>
public class ExternalEndpointService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalEndpointService> _logger;

    /// <summary>
    /// Initializes a new instance of the ExternalEndpointService class.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public ExternalEndpointService(
        IHttpClientFactory httpClientFactory,
        ILogger<ExternalEndpointService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Posts an HL7 message to the external endpoint.
    /// </summary>
    /// <param name="hl7Message">The HL7 message to post.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the post was successful (2xx status code), false otherwise.</returns>
    public async Task<bool> PostHl7MessageAsync(string hl7Message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hl7Message);

        var httpClient = _httpClientFactory.CreateClient("ExternalEndpoint");

        _logger.LogInformation("Posting HL7 message to external endpoint. MessageLength: {Length} characters",
            hl7Message.Length);

        try
        {
            var content = new StringContent(hl7Message, System.Text.Encoding.UTF8, "text/plain");
            var response = await httpClient.PostAsync("/SubmitHL7Message", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully posted HL7 message to external endpoint. StatusCode: {StatusCode}",
                    (int)response.StatusCode);
                return true;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to post HL7 message to external endpoint. StatusCode: {StatusCode}, Response: {Response}",
                    (int)response.StatusCode, responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while posting HL7 message to external endpoint");
            return false;
        }
    }
}
