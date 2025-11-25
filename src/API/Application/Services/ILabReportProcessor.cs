using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Exceptions;
using LabResultsGateway.API.Domain.ValueObjects;

namespace LabResultsGateway.API.Application.Services;

/// <summary>
/// Orchestration service interface for processing complete lab report workflows.
/// Coordinates the end-to-end processing from PDF upload to HL7 message queuing.
/// </summary>
public interface ILabReportProcessor
{
    /// <summary>
    /// Processes a complete lab report workflow from PDF content.
    /// Orchestrates the following steps:
    /// 1. Extract LabNumber from blob name
    /// 2. Fetch lab metadata from external API
    /// 3. Build HL7 v2.5.1 ORU^R01 message
    /// 4. Queue message for processing
    /// </summary>
    /// <param name="blobName">The blob name containing the PDF file (used to extract LabNumber).</param>
    /// <param name="pdfContent">The PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when blobName or pdfContent is null.</exception>
    /// <exception cref="ArgumentException">Thrown when blobName is empty or pdfContent is empty.</exception>
    /// <exception cref="LabNumberInvalidException">Thrown when LabNumber cannot be extracted from blobName.</exception>
    /// <exception cref="MetadataNotFoundException">Thrown when metadata is not found for the extracted LabNumber.</exception>
    /// <exception cref="Hl7GenerationException">Thrown when HL7 message generation fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when queue operations fail.</exception>
    Task ProcessLabReportAsync(string blobName, byte[] pdfContent, CancellationToken cancellationToken = default);
}
