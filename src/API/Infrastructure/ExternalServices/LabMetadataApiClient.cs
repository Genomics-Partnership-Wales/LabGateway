using System.Net.Http.Json;
using System.Text.Json;
using LabResultsGateway.API.Application.DTOs;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Exceptions;
using LabResultsGateway.API.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Infrastructure.ExternalServices;

/// <summary>
/// Implementation of ILabMetadataService that retrieves lab metadata from an external API.
/// </summary>
public class LabMetadataApiClient : ILabMetadataService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LabMetadataApiClient> _logger;

    /// <summary>
    /// Initializes a new instance of the LabMetadataApiClient class.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="configuration">Configuration containing API settings.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public LabMetadataApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LabMetadataApiClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves lab metadata for the specified lab number from the external API.
    /// </summary>
    /// <param name="labNumber">The lab number to retrieve metadata for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The lab metadata value object.</returns>
    /// <exception cref="MetadataNotFoundException">Thrown when the lab number is not found (404).</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    public async Task<LabMetadata> GetLabMetadataAsync(LabNumber labNumber, CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient("MetadataApi");
        var requestUri = $"/metadata?labNumber={labNumber}";

        _logger.LogInformation("Retrieving lab metadata for LabNumber: {LabNumber}", labNumber);

        try
        {
            var response = await httpClient.GetAsync(requestUri, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Lab metadata not found for LabNumber: {LabNumber}", labNumber);
                throw new MetadataNotFoundException(labNumber, $"Lab metadata not found for lab number '{labNumber}'.");
            }

            response.EnsureSuccessStatusCode();

            var labMetadataDto = await response.Content.ReadFromJsonAsync<LabMetadataDto>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (labMetadataDto == null)
            {
                _logger.LogError("Received null or invalid response from metadata API for LabNumber: {LabNumber}", labNumber);
                throw new HttpRequestException("Received null or invalid response from metadata API.");
            }

            // Map DTO to domain value object
            var labMetadata = new LabMetadata
            {
                PatientId = labMetadataDto.PatientId,
                FirstName = labMetadataDto.FirstName,
                LastName = labMetadataDto.LastName,
                DateOfBirth = labMetadataDto.DateOfBirth,
                Gender = labMetadataDto.Gender,
                TestType = labMetadataDto.TestType,
                CollectionDate = new DateTimeOffset(labMetadataDto.CollectionDate)
            };

            _logger.LogInformation("Successfully retrieved lab metadata for LabNumber: {LabNumber}, PatientId: {PatientId}",
                labNumber, labMetadata.PatientId);

            return labMetadata;
        }
        catch (MetadataNotFoundException)
        {
            // Re-throw domain exception without wrapping
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for LabNumber: {LabNumber}", labNumber);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve lab metadata for LabNumber: {LabNumber}", labNumber);
            throw new HttpRequestException($"Failed to retrieve lab metadata: {ex.Message}", ex);
        }
    }
}
