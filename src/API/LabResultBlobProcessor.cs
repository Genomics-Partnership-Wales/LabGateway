using System.Diagnostics;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Exceptions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API;

/// <summary>
/// Azure Function that processes lab result PDF files uploaded to blob storage.
/// Renamed from FileProcessor to LabResultBlobProcessor for clarity.
/// </summary>
public class LabResultBlobProcessor
{
    private readonly ILabReportProcessor _labReportProcessor;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<LabResultBlobProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the LabResultBlobProcessor class.
    /// </summary>
    /// <param name="labReportProcessor">Service for processing lab reports.</param>
    /// <param name="blobStorageService">Service for blob storage operations.</param>
    /// <param name="activitySource">Activity source for OpenTelemetry tracing.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public LabResultBlobProcessor(
        ILabReportProcessor labReportProcessor,
        IBlobStorageService blobStorageService,
        ActivitySource activitySource,
        ILogger<LabResultBlobProcessor> logger)
    {
        _labReportProcessor = labReportProcessor ?? throw new ArgumentNullException(nameof(labReportProcessor));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a lab result PDF file uploaded to blob storage.
    /// </summary>
    /// <param name="stream">The blob stream containing the PDF file.</param>
    /// <param name="name">The name of the blob (includes filename).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    [Function("LabResultBlobProcessor")]
    public async Task Run(
        [BlobTrigger("lab-results-gateway/{name}", Connection = "StorageConnection")] Stream stream,
        string name,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ProcessLabReport", ActivityKind.Consumer);

        var correlationId = activity?.Id ?? Guid.NewGuid().ToString();
        activity?.SetTag("correlation.id", correlationId);
        activity?.SetTag("blob.name", name);

        if (name.StartsWith("Failed/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipping processing for file in Failed folder: {BlobName}", name);
            return;
        }

        try
        {
            // Read PDF content from stream
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            var pdfBytes = memoryStream.ToArray();

            _logger.LogInformation("Starting lab report processing. BlobName: {BlobName}, CorrelationId: {CorrelationId}, Size: {Size} bytes",
                name, correlationId, pdfBytes.Length);

            // Process the lab report
            await _labReportProcessor.ProcessLabReportAsync(name, pdfBytes, cancellationToken);

            _logger.LogInformation("Lab report processed successfully. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
                name, correlationId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (LabNumberInvalidException ex)
        {
            _logger.LogError(ex, "Invalid lab number in blob name. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
                name, correlationId);
            await _blobStorageService.MoveToFailedFolderAsync(name, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
        catch (MetadataNotFoundException ex)
        {
            _logger.LogError(ex, "Metadata not found for lab number. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
                name, correlationId);
            await _blobStorageService.MoveToFailedFolderAsync(name, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
        catch (Hl7GenerationException ex)
        {
            _logger.LogError(ex, "HL7 message generation failed. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
                name, correlationId);
            await _blobStorageService.MoveToFailedFolderAsync(name, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing lab report. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
                name, correlationId);
            await _blobStorageService.MoveToFailedFolderAsync(name, cancellationToken);
            // TODO: Send to poison queue with retry count 0 based on queue message format decision
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
    }
}
