using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Enums;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Functions;

/// <summary>
/// Timer-triggered function that processes pending outbox messages.
/// Runs every 30 seconds to dispatch messages that were stored in the outbox.
/// </summary>
public class OutboxDispatcherFunction
{
    private readonly IOutboxService _outboxService;
    private readonly IMessageQueueService _messageQueueService;
    private readonly ILogger<OutboxDispatcherFunction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxDispatcherFunction"/> class.
    /// </summary>
    /// <param name="outboxService">The outbox service.</param>
    /// <param name="messageQueueService">The message queue service.</param>
    /// <param name="logger">The logger.</param>
    public OutboxDispatcherFunction(
        IOutboxService outboxService,
        IMessageQueueService messageQueueService,
        ILogger<OutboxDispatcherFunction> logger)
    {
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _messageQueueService = messageQueueService ?? throw new ArgumentNullException(nameof(messageQueueService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes pending outbox messages every 30 seconds.
    /// </summary>
    /// <param name="timerInfo">Timer information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Function("OutboxDispatcher")]
    public async Task Run(
        [TimerTrigger("0/30 * * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Outbox dispatcher started at: {Time}", DateTimeOffset.Now);

        try
        {
            // Get pending messages ready for dispatch
            var pendingMessages = await _outboxService.GetPendingMessagesAsync(cancellationToken: cancellationToken);

            if (!pendingMessages.Any())
            {
                _logger.LogInformation("No pending messages to dispatch");
                return;
            }

            _logger.LogInformation("Processing {Count} pending messages", pendingMessages.Count);

            var processedCount = 0;
            var failedCount = 0;

            foreach (var message in pendingMessages)
            {
                try
                {
                    await ProcessMessageAsync(message, cancellationToken);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process outbox message. Id: {Id}, CorrelationId: {CorrelationId}",
                        message.Id, message.CorrelationId);

                    await _outboxService.MarkAsFailedAsync(message.Id, ex.Message, cancellationToken);
                    failedCount++;
                }
            }

            _logger.LogInformation(
                "Outbox dispatcher completed. Processed: {Processed}, Failed: {Failed}",
                processedCount, failedCount);

            // Cleanup old dispatched messages
            var cleanedCount = await _outboxService.CleanupOldMessagesAsync(cancellationToken);
            if (cleanedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old dispatched messages", cleanedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox dispatcher encountered an error");
            throw;
        }
    }

    private async Task ProcessMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing outbox message. Id: {Id}, Type: {Type}, CorrelationId: {CorrelationId}",
            message.Id, message.MessageType, message.CorrelationId);

        // Check if message is ready for retry (if it was previously failed)
        if (message.Status == OutboxStatus.Failed && message.NextRetryAt.HasValue)
        {
            if (DateTimeOffset.Now < message.NextRetryAt.Value)
            {
                _logger.LogInformation(
                    "Message not ready for retry yet. Id: {Id}, NextRetryAt: {NextRetryAt}",
                    message.Id, message.NextRetryAt.Value);
                return;
            }
        }

        // Attempt to dispatch to the processing queue
        await _messageQueueService.SendToProcessingQueueAsync(message.Payload, cancellationToken);

        // Mark as dispatched
        await _outboxService.MarkAsDispatchedAsync(message.Id, cancellationToken);

        _logger.LogInformation(
            "Successfully dispatched outbox message. Id: {Id}, CorrelationId: {CorrelationId}",
            message.Id, message.CorrelationId);
    }
}
