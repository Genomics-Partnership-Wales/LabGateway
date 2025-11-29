using LabResultsGateway.API.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace LabResultsGateway.API.Infrastructure.ExternalServices;

/// <summary>
/// Service for posting HL7 messages to the external NHS Wales endpoint.
/// </summary>
public class ExternalEndpointService : IExternalEndpointService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalEndpointService> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the ExternalEndpointService class.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public ExternalEndpointService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ExternalEndpointService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

        // Respect a development-only fallback to a local mock endpoint when enabled
        var useLocalFallback = _configuration.GetValue<bool>("UseLocalHl7Fallback", false);

        _logger.LogInformation("Posting HL7 message to external endpoint. MessageLength: {Length}, UseLocalHl7Fallback: {UseLocal}",
            hl7Message.Length, useLocalFallback);

        try
        {
            var content = new StringContent(hl7Message, System.Text.Encoding.UTF8, "text/plain");
            HttpResponseMessage response;

            if (useLocalFallback)
            {
                // Use the local functions host (MetadataApi client) as a mock receiver for HL7
                var localClient = _httpClientFactory.CreateClient("MetadataApi");

                // Functions HTTP routes are prefixed with 'api' by default. Post to 'api/SubmitHL7Message'.
                const string localPath = "api/SubmitHL7Message";
                _logger.LogInformation("UseLocalHl7Fallback enabled - posting to local mock at {Base}{Path}", localClient.BaseAddress, localPath);
                response = await localClient.PostAsync(localPath, content, cancellationToken);
            }
            else
            {
                var httpClient = _httpClientFactory.CreateClient("ExternalEndpoint");

                // If ExternalEndpointUrl contains the full absolute URL (including path), prefer it
                var configured = _configuration.GetValue<string>("ExternalEndpointUrl");
                Uri target;

                if (!string.IsNullOrWhiteSpace(configured) && Uri.TryCreate(configured, UriKind.Absolute, out var cfgUri))
                {
                    // If configured URL already includes path, use it. Otherwise combine with client base.
                    if (!string.IsNullOrWhiteSpace(cfgUri.PathAndQuery) && cfgUri.PathAndQuery != "/")
                    {
                        target = cfgUri;
                    }
                    else
                    {
                        target = new Uri(httpClient.BaseAddress!, "SubmitHL7Message");
                    }
                }
                else
                {
                    target = new Uri(httpClient.BaseAddress!, "SubmitHL7Message");
                }

                _logger.LogInformation("Posting to external HL7 target {Target}", target);
                response = await httpClient.PostAsync(target, content, cancellationToken);
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully posted HL7 message. StatusCode: {StatusCode}", (int)response.StatusCode);
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to post HL7 message. StatusCode: {StatusCode}, Response: {Response}", (int)response.StatusCode, body);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request exception while posting HL7 message");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected exception while posting HL7 message");
            return false;
        }
    }
}
