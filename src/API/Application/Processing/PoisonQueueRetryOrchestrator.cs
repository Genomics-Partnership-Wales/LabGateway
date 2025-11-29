using System.Diagnostics;
using LabResultsGateway.API.Application.DTOs;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Application.Retry;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Infrastructure.Queue;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Application.Processing;

/// <summary>
/// Orchestrates the poison queue retry process.
/// </summary>
public class PoisonQueueRetryOrchestrator : IPoisonQueueRetryOrchestrator
{
    private readonly IAzureQueueClient _queueClient;
    private readonly IPoisonQueueMessageProcessor _messageProcessor;
    private readonly IMessageQueueService _messageQueueService;
    private readonly IRetryStrategy _retryStrategy;
    private readonly PoisonQueueRetryOptions _options;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<PoisonQueueRetryOrchestrator> _logger;

    /// <summary>
    /// Initializes a new instance of the PoisonQueueRetryOrchestrator class.
    /// </summary>
    /// <param name="queueClient">The Azure queue client.</param>
    /// <param name="messageProcessor">The message processor.</param>
    /// <param name="messageQueueService">The message queue service.</param>
    /// <param name="retryStrategy">The retry strategy.</param>
    /// <param name="options">Retry configuration options.</param>
    /// <param name="activitySource">Activity source for OpenTelemetry tracing.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public PoisonQueueRetryOrchestrator(
        IAzureQueueClient queueClient,
        IPoisonQueueMessageProcessor messageProcessor,
        IMessageQueueService messageQueueService,
        IRetryStrategy retryStrategy,
        PoisonQueueRetryOptions options,
        ActivitySource activitySource,
        ILogger<PoisonQueueRetryOrchestrator> logger)
    {
        _queueClient = queueClient ?? throw new ArgumentNullException(nameof(queueClient));
        _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
        _messageQueueService = messageQueueService ?? throw new ArgumentNullException(nameof(messageQueueService));
        _retryStrategy = retryStrategy ?? throw new ArgumentNullException(nameof(retryStrategy));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes messages from the poison queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ProcessPoisonQueueAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ProcessPoisonQueue", ActivityKind.Consumer);

        await _queueClient.EnsureQueueExistsAsync(cancellationToken);

        var messages = await _queueClient.ReceiveMessagesAsync(
            maxMessages: _options.MaxMessagesPerBatch,
            visibilityTimeout: TimeSpan.FromMinutes(_options.ProcessingVisibilityTimeoutMinutes),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Retrieved {MessageCount} messages from poison queue", messages.Count);
        activity?.SetTag("message.count", messages.Count);

        if (messages.Count == 0)
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
            return;
        }

        var processingTasks = messages
            .Select(message => ProcessSingleMessageAsync(message, cancellationToken))
            .ToList();

        await Task.WhenAll(processingTasks);

        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private async Task ProcessSingleMessageAsync(QueueMessageWrapper message, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _messageProcessor.ProcessMessageAsync(message, cancellationToken);

            await HandleProcessingResultAsync(message, result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message. MessageId: {MessageId}", message.MessageId);
            // In case of unexpected errors, send to dead letter queue
            await HandleProcessingResultAsync(
                message,
                new MessageProcessingResult(false, RetryResult.DeadLetter, ex.Message),
                cancellationToken);
        }
    }

    private async Task HandleProcessingResultAsync(
        QueueMessageWrapper message,
        MessageProcessingResult result,
        CancellationToken cancellationToken)
    {
        switch (result.Result)
        {
            case RetryResult.Success:
                await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                _logger.LogInformation("Message processed successfully. MessageId: {MessageId}", message.MessageId);
                break;

            case RetryResult.Retry:
                await UpdateMessageForRetryAsync(message, cancellationToken);
                _logger.LogWarning("Message scheduled for retry. MessageId: {MessageId}, Error: {Error}",
                    message.MessageId, result.ErrorMessage);
                break;

            case RetryResult.DeadLetter:
                await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                _logger.LogError("Message sent to dead letter queue. MessageId: {MessageId}, Error: {Error}",
                    message.MessageId, result.ErrorMessage);
                break;
        }
    }

    private async Task UpdateMessageForRetryAsync(QueueMessageWrapper message, CancellationToken cancellationToken)
    {
        try
        {
            var queueMessage = await _messageQueueService.DeserializeMessageAsync(message.MessageText);
            if (queueMessage is null)
            {
                _logger.LogError("Failed to deserialize message for retry update. MessageId: {MessageId}", message.MessageId);
                return;
            }

            var updatedMessage = queueMessage with { RetryCount = queueMessage.RetryCount + 1 };
            var serializedMessage = await _messageQueueService.SerializeMessageAsync(updatedMessage);

            var retryContext = new RetryContext(
                updatedMessage.CorrelationId,
                updatedMessage.RetryCount,
                _options.MaxRetryAttempts);

            var delay = _retryStrategy.CalculateNextRetryDelay(retryContext);

            await _queueClient.UpdateMessageAsync(
                message.MessageId,
                message.PopReceipt,
                serializedMessage,
                delay,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update message for retry. MessageId: {MessageId}", message.MessageId);
            throw;
        }
    }
}
