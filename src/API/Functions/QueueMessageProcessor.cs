using System.Diagnostics;
using LabResultsGateway.API.Application.DTOs;
using LabResultsGateway.API.Application.Events;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Events;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Functions;

/// <summary>
/// Azure Function that processes HL7 messages from the processing queue.
/// Handles message validation, external endpoint posting, and error routing.
/// </summary>
public class QueueMessageProcessor
{
    private readonly IMessageQueueService _messageQueueService;
    private readonly IExternalEndpointService _externalEndpointService;
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly ILogger<QueueMessageProcessor> _logger;
    private readonly ActivitySource _activitySource;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueMessageProcessor"/> class.
    /// </summary>
    /// <param name="messageQueueService">Service for queue operations.</param>
    /// <param name="externalEndpointService">Service for posting HL7 messages to external endpoints.</param>
    /// <param name="domainEventDispatcher">Dispatcher for domain events.</param>
    /// <param name="logger">Logger for structured logging.</param>
    /// <param name="activitySource">Activity source for distributed tracing.</param>
    public QueueMessageProcessor(
        IMessageQueueService messageQueueService,
        IExternalEndpointService externalEndpointService,
        IDomainEventDispatcher domainEventDispatcher,
        ILogger<QueueMessageProcessor> logger,
        ActivitySource activitySource)
    {
        _messageQueueService = messageQueueService ?? throw new ArgumentNullException(nameof(messageQueueService));
        _externalEndpointService = externalEndpointService ?? throw new ArgumentNullException(nameof(externalEndpointService));
        _domainEventDispatcher = domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
    }

    /// <summary>
    /// Processes HL7 messages from the processing queue.
    /// </summary>
    /// <param name="message">The serialized queue message containing HL7 data.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    [Function("QueueMessageProcessor")]
    public async Task Run(
        [QueueTrigger("%ProcessingQueueName%", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        QueueMessage queueMessage;

        try
        {
            // Deserialize the queue message
            queueMessage = await _messageQueueService.DeserializeMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize queue message. Message: {Message}", message);
            throw; // Let it go to poison queue
        }

        using var activity = _activitySource.StartActivity("ProcessQueueMessage", ActivityKind.Consumer);
        activity?.SetTag("correlationId", queueMessage.CorrelationId);
        activity?.SetTag("blobName", queueMessage.BlobName);
        activity?.SetTag("retryCount", queueMessage.RetryCount);

        try
        {
            _logger.LogInformation(
                "Processing HL7 message from queue. CorrelationId: {CorrelationId}, BlobName: {BlobName}, RetryCount: {RetryCount}",
                queueMessage.CorrelationId, queueMessage.BlobName, queueMessage.RetryCount);

            // Validate message content
            if (string.IsNullOrWhiteSpace(queueMessage.Hl7Message))
            {
                throw new InvalidOperationException("HL7 message content is empty or null.");
            }

            // Post HL7 message to external endpoint
            _logger.LogInformation(
                "Posting HL7 message to external endpoint. CorrelationId: {CorrelationId}",
                queueMessage.CorrelationId);

            var success = await _externalEndpointService.PostHl7MessageAsync(queueMessage.Hl7Message, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException("Failed to post HL7 message to external endpoint.");
            }

            _logger.LogInformation(
                "HL7 message processed successfully. CorrelationId: {CorrelationId}",
                queueMessage.CorrelationId);

            // Raise MessageDeliveredEvent on successful external POST
            var messageDeliveredEvent = new MessageDeliveredEvent(queueMessage.CorrelationId, "external-endpoint");
            await _domainEventDispatcher.DispatchAsync(messageDeliveredEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process HL7 message. CorrelationId: {CorrelationId}, BlobName: {BlobName}",
                queueMessage.CorrelationId, queueMessage.BlobName);

            // Raise MessageDeliveryFailedEvent on failed external POST
            var messageDeliveryFailedEvent = new MessageDeliveryFailedEvent(
                queueMessage.CorrelationId, ex.Message, queueMessage.RetryCount);
            await _domainEventDispatcher.DispatchAsync(messageDeliveryFailedEvent);

            // Exception will cause message to go to poison queue for retry processing
            throw;
        }
    }
}
