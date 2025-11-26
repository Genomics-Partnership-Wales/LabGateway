using LabResultsGateway.API.Application.DTOs;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Exceptions;
using LabResultsGateway.API.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Application.Services;

/// <summary>
/// Orchestration service for processing complete lab report workflows.
/// Coordinates metadata retrieval, HL7 message building, and message queuing.
/// </summary>
public class LabReportProcessor : ILabReportProcessor
{
    private readonly ILabMetadataService _labMetadataService;
    private readonly IHl7MessageBuilder _hl7MessageBuilder;
    private readonly IMessageQueueService _messageQueueService;
    private readonly ILogger<LabReportProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LabReportProcessor"/> class.
    /// </summary>
    /// <param name="labMetadataService">Service for retrieving lab metadata.</param>
    /// <param name="hl7MessageBuilder">Service for building HL7 messages.</param>
    /// <param name="messageQueueService">Service for queue operations.</param>
    /// <param name="logger">Logger for structured logging.</param>
    public LabReportProcessor(
        ILabMetadataService labMetadataService,
        IHl7MessageBuilder hl7MessageBuilder,
        IMessageQueueService messageQueueService,
        ILogger<LabReportProcessor> logger)
    {
        _labMetadataService = labMetadataService ?? throw new ArgumentNullException(nameof(labMetadataService));
        _hl7MessageBuilder = hl7MessageBuilder ?? throw new ArgumentNullException(nameof(hl7MessageBuilder));
        _messageQueueService = messageQueueService ?? throw new ArgumentNullException(nameof(messageQueueService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task ProcessLabReportAsync(string blobName, byte[] pdfContent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobName);
        ArgumentNullException.ThrowIfNull(pdfContent);

        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new ArgumentException("Blob name cannot be null or whitespace.", nameof(blobName));
        }

        if (pdfContent.Length == 0)
        {
            throw new ArgumentException("PDF content cannot be empty.", nameof(pdfContent));
        }

        var correlationId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation(
                "Starting lab report processing. BlobName: {BlobName}, CorrelationId: {CorrelationId}, PdfSize: {PdfSize} bytes",
                blobName, correlationId, pdfContent.Length);

            // Step 1: Extract LabNumber from blob name
            var labNumber = ExtractLabNumberFromBlobName(blobName);
            _logger.LogInformation(
                "Extracted lab number from blob name. LabNumber: {LabNumber}, CorrelationId: {CorrelationId}",
                labNumber, correlationId);

            // Step 2: Fetch lab metadata
            _logger.LogInformation(
                "Fetching lab metadata. LabNumber: {LabNumber}, CorrelationId: {CorrelationId}",
                labNumber, correlationId);

            var metadata = await _labMetadataService.GetLabMetadataAsync(labNumber, cancellationToken);

            _logger.LogInformation(
                "Lab metadata retrieved successfully. LabNumber: {LabNumber}, PatientId: {PatientId}, CorrelationId: {CorrelationId}",
                labNumber, metadata.PatientId, correlationId);

            // Step 3: Create LabReport entity
            var labReport = new LabReport(
                labNumber: labNumber,
                pdfContent: pdfContent,
                metadata: metadata,
                correlationId: correlationId);

            _logger.LogInformation(
                "Lab report entity created. LabNumber: {LabNumber}, CorrelationId: {CorrelationId}",
                labNumber, correlationId);

            // Step 4: Build HL7 message
            _logger.LogInformation(
                "Building HL7 message. LabNumber: {LabNumber}, CorrelationId: {CorrelationId}",
                labNumber, correlationId);

            var hl7Message = _hl7MessageBuilder.BuildOruR01Message(labReport);

            _logger.LogInformation(
                "HL7 message built successfully. LabNumber: {LabNumber}, MessageLength: {MessageLength}, CorrelationId: {CorrelationId}",
                labNumber, hl7Message.Length, correlationId);

            // Step 5: Create queue message and queue for processing
            _logger.LogInformation(
                "Creating queue message for HL7 processing. LabNumber: {LabNumber}, CorrelationId: {CorrelationId}",
                labNumber, correlationId);

            var queueMessage = new QueueMessage(
                hl7Message,
                correlationId,
                0,
                DateTimeOffset.Now,
                blobName);

            var serializedMessage = await _messageQueueService.SerializeMessageAsync(queueMessage);

            _logger.LogInformation(
                "Queuing serialized message for processing. LabNumber: {LabNumber}, CorrelationId: {CorrelationId}",
                labNumber, correlationId);

            await _messageQueueService.SendToProcessingQueueAsync(serializedMessage, cancellationToken);

            _logger.LogInformation(
                "Lab report processing completed successfully. LabNumber: {LabNumber}, CorrelationId: {CorrelationId}",
                labNumber, correlationId);
        }
        catch (LabNumberInvalidException ex)
        {
            _logger.LogError(ex,
                "Lab number validation failed during processing. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
                blobName, correlationId);
            throw;
        }
        catch (MetadataNotFoundException ex)
        {
            _logger.LogError(ex,
                "Lab metadata not found during processing. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
                blobName, correlationId);
            throw;
        }
        catch (Hl7GenerationException ex)
        {
            _logger.LogError(ex,
                "HL7 message generation failed during processing. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
                blobName, correlationId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during lab report processing. BlobName: {BlobName}, CorrelationId: {CorrelationId}",
                blobName, correlationId);
            throw;
        }
    }

    /// <summary>
    /// Extracts the LabNumber from the blob name.
    /// Assumes the blob name format is "{labNumber}_{date}_{time}.{extension}" or similar pattern.
    /// </summary>
    /// <param name="blobName">The blob name to extract LabNumber from.</param>
    /// <returns>The extracted LabNumber value object.</returns>
    /// <exception cref="LabNumberInvalidException">Thrown when LabNumber cannot be extracted or is invalid.</exception>
    private static LabNumber ExtractLabNumberFromBlobName(string blobName)
    {
        // Extract lab number from filename (remove extension and path)
        var fileName = Path.GetFileNameWithoutExtension(blobName);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new LabNumberInvalidException("", "Blob name does not contain a valid filename.");
        }

        // Split by underscore and take the first part as lab number
        // Format: LAB003_20251124_163012 -> LAB003
        var parts = fileName.Split('_');
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new LabNumberInvalidException(fileName, "Blob name does not contain a valid lab number prefix.");
        }

        var labNumberString = parts[0];

        try
        {
            // Create LabNumber value object (will validate format)
            return new LabNumber(labNumberString);
        }
        catch (LabNumberInvalidException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LabNumberInvalidException(labNumberString, $"Failed to extract lab number from blob name '{blobName}': {ex.Message}");
        }
    }
}
