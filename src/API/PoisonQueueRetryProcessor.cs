using System.Diagnostics;
using Azure.Storage.Queues;
using LabResultsGateway.API.Application.DTOs;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Infrastructure.ExternalServices;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API;

/// <summary>
/// Azure Function that retries failed HL7 messages from the poison queue.
/// Renamed from TimeTriggeredProcessor to PoisonQueueRetryProcessor for clarity.
/// </summary>
public class PoisonQueueRetryProcessor
{
    private readonly IMessageQueueService _messageQueueService;
    private readonly IExternalEndpointService _externalEndpointService;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<PoisonQueueRetryProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the PoisonQueueRetryProcessor class.
    /// </summary>
    /// <param name="messageQueueService">Service for queue operations.</param>
    /// <param name="externalEndpointService">Service for posting to external endpoint.</param>
    /// <param name="configuration">Configuration for queue names and settings.</param>
    /// <param name="activitySource">Activity source for OpenTelemetry tracing.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public PoisonQueueRetryProcessor(
        IMessageQueueService messageQueueService,
        IExternalEndpointService externalEndpointService,
        IConfiguration configuration,
        ActivitySource activitySource,
        ILogger<PoisonQueueRetryProcessor> logger)
    {
        _messageQueueService = messageQueueService ?? throw new ArgumentNullException(nameof(messageQueueService));
        _externalEndpointService = externalEndpointService ?? throw new ArgumentNullException(nameof(externalEndpointService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes messages from the poison queue and retries failed deliveries.
    /// Runs every 5 minutes.
    /// </summary>
    /// <param name="myTimer">Timer trigger information.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    [Function("PoisonQueueRetryProcessor")]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("RetryPoisonQueue", ActivityKind.Consumer);

        _logger.LogInformation("Poison queue retry processor starting at: {ExecutionTime}", DateTime.UtcNow);

        var queueServiceClient = new QueueServiceClient(_configuration["StorageConnection"]!);
        var queueClient = queueServiceClient.GetQueueClient(_configuration["PoisonQueueName"]!);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var messages = await queueClient.ReceiveMessagesAsync(maxMessages: 10, visibilityTimeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);

        _logger.LogInformation("Retrieved {MessageCount} messages from poison queue", messages.Value?.Length ?? 0);
        activity?.SetTag("message.count", messages.Value?.Length ?? 0);

        if (messages.Value is not null)
        {
            foreach (var message in messages.Value)
            {
                using var retryActivity = _activitySource.StartActivity("RetryMessage", ActivityKind.Consumer, activity.Context);

                string correlationId = string.Empty;

                try
                {
                    // Deserialize the queue message
#pragma warning disable CS8602 // Dereference of a possibly null reference - suppressed as we check for null below
                    var queueMessage = await _messageQueueService.DeserializeMessageAsync(message.MessageText);
#pragma warning restore CS8602

                    if (queueMessage is null)
                    {
                        _logger.LogError("Failed to deserialize queue message. MessageId: {MessageId}", message.MessageId);
                        retryActivity?.SetStatus(ActivityStatusCode.Error, "Deserialization failed");
                        continue;
                    }

                    // At this point, queueMessage is guaranteed to be not null
#pragma warning disable CS8602 // Dereference of a possibly null reference - suppressed as we check for null above
                    correlationId = queueMessage.CorrelationId;
                    var hl7Message = queueMessage.Hl7Message;
                    var retryCount = queueMessage.RetryCount;
#pragma warning restore CS8602

                    retryActivity?.SetTag("correlation.id", correlationId);
                    retryActivity?.SetTag("retry.count", retryCount);

                    if (retryCount >= 3)
                    {
                        _logger.LogWarning("Message exceeded max retries. CorrelationId: {CorrelationId}, RetryCount: {RetryCount}",
                            correlationId, retryCount);

                        // Send to dead letter queue
                        var deadLetterMessage = new DeadLetterMessage(
                            hl7Message,
                            correlationId,
                            retryCount,
                            queueMessage.Timestamp,
                            queueMessage.BlobName,
                            "Exceeded maximum retry attempts",
                            DateTimeOffset.UtcNow);

                        await _messageQueueService.SendToDeadLetterQueueAsync(deadLetterMessage, cancellationToken);
                        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                        continue;
                    }

                    var success = await _externalEndpointService.PostHl7MessageAsync(hl7Message, cancellationToken);

                    if (success)
                    {
                        _logger.LogInformation("Message retry successful. CorrelationId: {CorrelationId}, RetryCount: {RetryCount}",
                            correlationId, retryCount);
                        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                        retryActivity?.SetStatus(ActivityStatusCode.Ok);
                    }
                    else
                    {
                        retryCount++;
                        _logger.LogWarning("Message retry failed. CorrelationId: {CorrelationId}, RetryCount: {RetryCount}, NextRetryIn: {NextRetry} minutes",
                            correlationId, retryCount, Math.Pow(2, retryCount));

                        // Update message with incremented retry count
                        var updatedMessage = queueMessage with { RetryCount = retryCount };
                        var serializedMessage = await _messageQueueService.SerializeMessageAsync(updatedMessage);

                        var visibilityTimeout = TimeSpan.FromMinutes(Math.Pow(2, retryCount));
                        await queueClient.UpdateMessageAsync(message.MessageId, message.PopReceipt, messageText: serializedMessage, visibilityTimeout: visibilityTimeout, cancellationToken: cancellationToken);
                        retryActivity?.SetStatus(ActivityStatusCode.Error, "Retry failed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing poison queue message. CorrelationId: {CorrelationId}", correlationId);
                    retryActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                }
            }
        }

        _logger.LogInformation("Poison queue retry processor completed at: {ExecutionTime}", DateTime.UtcNow);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
}
