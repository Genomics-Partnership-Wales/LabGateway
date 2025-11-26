using System.Diagnostics;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Infrastructure.ExternalServices;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API;

/// <summary>
/// Azure Function that processes HL7 messages from the processing queue.
/// Triggers on messages in the lab-reports-queue and posts them to the external NHS Wales endpoint.
/// </summary>
public class QueueMessageProcessor
{
    private readonly IMessageQueueService _messageQueueService;
    private readonly ExternalEndpointService _externalEndpointService;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<QueueMessageProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the QueueMessageProcessor class.
    /// </summary>
    /// <param name="messageQueueService">Service for queue operations.</param>
    /// <param name="externalEndpointService">Service for posting to external endpoint.</param>
    /// <param name="activitySource">Activity source for OpenTelemetry tracing.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public QueueMessageProcessor(
        IMessageQueueService messageQueueService,
        ExternalEndpointService externalEndpointService,
        ActivitySource activitySource,
        ILogger<QueueMessageProcessor> logger)
    {
        _messageQueueService = messageQueueService ?? throw new ArgumentNullException(nameof(messageQueueService));
        _externalEndpointService = externalEndpointService ?? throw new ArgumentNullException(nameof(externalEndpointService));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes an HL7 message from the queue and posts it to the external endpoint.
    /// </summary>
    /// <param name="message">The queue message containing HL7 data.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    [Function("QueueMessageProcessor")]
    public async Task Run(
        [QueueTrigger("lab-reports-queue")] string message,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ProcessQueueMessage", ActivityKind.Consumer);

        try
        {
            // Deserialize the queue message
            var queueMessage = await _messageQueueService.DeserializeMessageAsync(message);

            var correlationId = queueMessage.CorrelationId;
            var hl7Message = queueMessage.Hl7Message;

            activity?.SetTag("correlation.id", correlationId);
            activity?.SetTag("blob.name", queueMessage.BlobName);

            _logger.LogInformation("Processing HL7 message from queue. CorrelationId: {CorrelationId}, BlobName: {BlobName}",
                correlationId, queueMessage.BlobName);

            // Validate HL7 message
            if (string.IsNullOrWhiteSpace(hl7Message))
            {
                throw new ArgumentException("HL7 message is empty or null", nameof(hl7Message));
            }

            // Post to external endpoint
            var success = await _externalEndpointService.PostHl7MessageAsync(hl7Message, cancellationToken);

            if (success)
            {
                _logger.LogInformation("HL7 message posted successfully. CorrelationId: {CorrelationId}", correlationId);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                _logger.LogWarning("Failed to post HL7 message. CorrelationId: {CorrelationId}", correlationId);
                // Send to poison queue for retry
                await _messageQueueService.SendToPoisonQueueAsync(hl7Message, 0, cancellationToken);
                activity?.SetStatus(ActivityStatusCode.Error, "External endpoint returned failure");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing queue message");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw; // Let Azure Functions handle the poison queue routing
        }
    }
}
