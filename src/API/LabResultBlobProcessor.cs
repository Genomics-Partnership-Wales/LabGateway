using System.Diagnostics;
using System.Security.Cryptography;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Constants;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Exceptions;
using LabResultsGateway.API.Domain.Interfaces;
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
    private readonly IIdempotencyService _idempotencyService;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<LabResultBlobProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the LabResultBlobProcessor class.
    /// </summary>
    /// <param name="labReportProcessor">Service for processing lab reports.</param>
    /// <param name="blobStorageService">Service for blob storage operations.</param>
    /// <param name="idempotencyService">Service for checking idempotency of blob processing.</param>
    /// <param name="activitySource">Activity source for OpenTelemetry tracing.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public LabResultBlobProcessor(
        ILabReportProcessor labReportProcessor,
        IBlobStorageService blobStorageService,
        IIdempotencyService idempotencyService,
        ActivitySource activitySource,
        ILogger<LabResultBlobProcessor> logger)
    {
        _labReportProcessor = labReportProcessor ?? throw new ArgumentNullException(nameof(labReportProcessor));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
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

        if (ShouldSkipProcessing(name))
        {
            _logger.LogInformation("Skipping processing for file in Failed folder: {BlobName}", name);
            return;
        }

        var pdfBytes = await ReadStreamContentAsync(stream, cancellationToken);
        var contentHash = ComputeContentHash(pdfBytes);

        try
        {
            // Check for idempotency to avoid reprocessing duplicate blobs
            if (await _idempotencyService.HasBeenProcessedAsync(name, contentHash))
            {
                _logger.LogInformation("Blob {BlobName} has already been processed (idempotency check). CorrelationId: {CorrelationId}",
                    name, correlationId);
                activity?.SetStatus(ActivityStatusCode.Ok, "Already processed");
                return;
            }

            _logger.LogInformation("Starting lab report processing. BlobName: {BlobName}, CorrelationId: {CorrelationId}, Size: {Size} bytes",
                name, correlationId, pdfBytes.Length);

            await _labReportProcessor.ProcessLabReportAsync(name, pdfBytes, cancellationToken);

            // Mark as processed successfully
            await _idempotencyService.MarkAsProcessedAsync(name, contentHash, ProcessingOutcome.Success);

            _logger.LogInformation("Lab report processed successfully. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
                name, correlationId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (LabProcessingException ex)
        {
            await HandleLabProcessingExceptionAsync(ex, name, contentHash, correlationId, activity, cancellationToken);
        }
        catch (Exception ex)
        {
            await HandleUnexpectedExceptionAsync(ex, name, contentHash, correlationId, activity, cancellationToken);
        }
    }

    /// <summary>
    /// Determines if the blob should be skipped from processing.
    /// </summary>
    /// <param name="blobName">The name of the blob.</param>
    /// <returns>True if processing should be skipped; otherwise, false.</returns>
    private static bool ShouldSkipProcessing(string blobName) =>
        blobName.StartsWith(BlobConstants.FailedFolderPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the stream content into a byte array.
    /// </summary>
    /// <param name="stream">The stream to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stream content as a byte array.</returns>
    private static async Task<byte[]> ReadStreamContentAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Computes a SHA-256 hash of the content for idempotency checking.
    /// </summary>
    /// <param name="content">The content to hash.</param>
    /// <returns>The computed hash as a byte array.</returns>
    private static byte[] ComputeContentHash(byte[] content)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(content);
    }

    /// <summary>
    /// Handles domain-specific lab processing exceptions.
    /// </summary>
    /// <param name="ex">The lab processing exception.</param>
    /// <param name="blobName">The name of the blob.</param>
    /// <param name="contentHash">The hash of the blob content.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    /// <param name="activity">The current activity for OpenTelemetry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task HandleLabProcessingExceptionAsync(
        LabProcessingException ex,
        string blobName,
        byte[] contentHash,
        string correlationId,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        _logger.LogError(ex, "Lab processing error ({ExceptionType}). BlobName: {BlobName}, CorrelationId: {CorrelationId}, IsRetryable: {IsRetryable}",
            ex.GetType().Name, blobName, correlationId, ex.IsRetryable);

        await _blobStorageService.MoveToFailedFolderAsync(blobName, cancellationToken);
        await _idempotencyService.MarkAsProcessedAsync(blobName, contentHash, ProcessingOutcome.Failed);
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    }

    /// <summary>
    /// Handles unexpected exceptions during lab report processing.
    /// </summary>
    /// <param name="ex">The unexpected exception.</param>
    /// <param name="blobName">The name of the blob.</param>
    /// <param name="contentHash">The hash of the blob content.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    /// <param name="activity">The current activity for OpenTelemetry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task HandleUnexpectedExceptionAsync(
        Exception ex,
        string blobName,
        byte[] contentHash,
        string correlationId,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        _logger.LogError(ex, "Unexpected error processing lab report. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
            blobName, correlationId);

        await _blobStorageService.MoveToFailedFolderAsync(blobName, cancellationToken);
        await _idempotencyService.MarkAsProcessedAsync(blobName, contentHash, ProcessingOutcome.Failed);
        // TODO: Send to poison queue with retry count 0 based on queue message format decision
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    }
}
