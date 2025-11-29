using System.Diagnostics;
using LabResultsGateway.API.Application.DTOs;
using LabResultsGateway.API.Application.Retry;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Infrastructure.ExternalServices;
using LabResultsGateway.API.Infrastructure.Queue;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Application.Processing;

/// <summary>
/// Processes individual poison queue messages.
/// </summary>
public class PoisonQueueMessageProcessor : IPoisonQueueMessageProcessor
{
    private readonly IMessageQueueService _messageQueueService;
    private readonly IExternalEndpointService _externalEndpointService;
    private readonly IRetryStrategy _retryStrategy;
    private readonly PoisonQueueRetryOptions _options;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<PoisonQueueMessageProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the PoisonQueueMessageProcessor class.
    /// </summary>
    /// <param name="messageQueueService">Service for queue operations.</param>
    /// <param name="externalEndpointService">Service for posting to external endpoint.</param>
    /// <param name="retryStrategy">Strategy for determining retry behavior.</param>
    /// <param name="options">Retry configuration options.</param>
    /// <param name="activitySource">Activity source for OpenTelemetry tracing.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public PoisonQueueMessageProcessor(
        IMessageQueueService messageQueueService,
        IExternalEndpointService externalEndpointService,
        IRetryStrategy retryStrategy,
        PoisonQueueRetryOptions options,
        ActivitySource activitySource,
        ILogger<PoisonQueueMessageProcessor> logger)
    {
        _messageQueueService = messageQueueService ?? throw new ArgumentNullException(nameof(messageQueueService));
        _externalEndpointService = externalEndpointService ?? throw new ArgumentNullException(nameof(externalEndpointService));
        _retryStrategy = retryStrategy ?? throw new ArgumentNullException(nameof(retryStrategy));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a single poison queue message.
    /// </summary>
    /// <param name="message">The queue message wrapper.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The processing result.</returns>
    public async Task<MessageProcessingResult> ProcessMessageAsync(
        QueueMessageWrapper message,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ProcessPoisonQueueMessage", ActivityKind.Consumer);

        try
        {
            var queueMessage = await DeserializeMessageAsync(message.MessageText, cancellationToken);
            if (queueMessage is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Deserialization failed");
                return new MessageProcessingResult(false, RetryResult.DeadLetter, "Failed to deserialize message");
            }

            activity?.SetTag("correlation.id", queueMessage.CorrelationId);
            activity?.SetTag("retry.count", queueMessage.RetryCount);

            var retryContext = new RetryContext(
                queueMessage.CorrelationId,
                queueMessage.RetryCount,
                _options.MaxRetryAttempts);

            if (!_retryStrategy.ShouldRetry(retryContext))
            {
                await SendToDeadLetterAsync(queueMessage, "Exceeded maximum retry attempts", cancellationToken);
                activity?.SetStatus(ActivityStatusCode.Error, "Max retries exceeded");
                return new MessageProcessingResult(false, RetryResult.DeadLetter, "Max retries exceeded");
            }

            var success = await TryPostToExternalEndpointAsync(queueMessage, cancellationToken);

            if (success)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                return new MessageProcessingResult(true, RetryResult.Success);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error, "External endpoint call failed");
                return new MessageProcessingResult(false, RetryResult.Retry, "External endpoint call failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message. MessageId: {MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return new MessageProcessingResult(false, RetryResult.DeadLetter, ex.Message);
        }
    }

    private async Task<QueueMessage?> DeserializeMessageAsync(string messageText, CancellationToken cancellationToken)
    {
        try
        {
            return await _messageQueueService.DeserializeMessageAsync(messageText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize message");
            return null;
        }
    }

    private async Task<bool> TryPostToExternalEndpointAsync(QueueMessage queueMessage, CancellationToken cancellationToken)
    {
        try
        {
            return await _externalEndpointService.PostHl7MessageAsync(queueMessage.Hl7Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to external endpoint. CorrelationId: {CorrelationId}",
                queueMessage.CorrelationId);
            return false;
        }
    }

    private async Task SendToDeadLetterAsync(QueueMessage queueMessage, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var deadLetterMessage = new DeadLetterMessage(
                queueMessage.Hl7Message,
                queueMessage.CorrelationId,
                queueMessage.RetryCount,
                queueMessage.Timestamp,
                queueMessage.BlobName,
                reason,
                DateTimeOffset.UtcNow);

            await _messageQueueService.SendToDeadLetterQueueAsync(deadLetterMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to dead letter queue. CorrelationId: {CorrelationId}",
                queueMessage.CorrelationId);
            throw;
        }
    }
}