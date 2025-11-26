namespace LabResultsGateway.API.Application.Services;

/// <summary>
/// Interface for posting HL7 messages to external endpoints.
/// </summary>
public interface IExternalEndpointService
{
    /// <summary>
    /// Posts an HL7 message to the configured external endpoint.
    /// </summary>
    /// <param name="hl7Message">The HL7 message to post.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the post was successful, false otherwise.</returns>
    Task<bool> PostHl7MessageAsync(string hl7Message, CancellationToken cancellationToken = default);
}
