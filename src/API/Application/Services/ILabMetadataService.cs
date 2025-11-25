using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Exceptions;
using LabResultsGateway.API.Domain.ValueObjects;

namespace LabResultsGateway.API.Application.Services;

/// <summary>
/// Service interface for retrieving lab metadata from external APIs.
/// </summary>
public interface ILabMetadataService
{
    /// <summary>
    /// Retrieves lab metadata for the specified lab number from the external metadata API.
    /// </summary>
    /// <param name="labNumber">The lab number value object identifying the lab report.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>The lab metadata value object containing patient and test information.</returns>
    /// <exception cref="MetadataNotFoundException">Thrown when metadata is not found for the specified lab number (404 response).</exception>
    /// <exception cref="HttpRequestException">Thrown when there are network or HTTP-related errors.</exception>
    /// <exception cref="JsonException">Thrown when the API response cannot be deserialized.</exception>
    Task<LabMetadata> GetLabMetadataAsync(LabNumber labNumber, CancellationToken cancellationToken = default);
}
