using LabResultsGateway.API.Application.DTOs;
using LabResultsGateway.API.Application.Services;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Infrastructure.Messaging;

/// <summary>
/// Decorator that wraps IMessageQueueService to implement the outbox pattern.
/// Messages are first stored in the outbox, then dispatched to the actual queue service.
/// </summary>
public class OutboxAwareQueueService : IMessageQueueService
{
    private readonly IMessageQueueService _innerQueueService;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<OutboxAwareQueueService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxAwareQueueService"/> class.
    /// </summary>
    /// <param name="innerQueueService">The inner queue service to delegate to.</param>
    /// <param name="outboxService">The outbox service for reliable message storage.</param>
    /// <param name="logger">The logger.</param>
    public OutboxAwareQueueService(
        IMessageQueueService innerQueueService,
        IOutboxService outboxService,
        ILogger<OutboxAwareQueueService> logger)
    {
        _innerQueueService = innerQueueService ?? throw new ArgumentNullException(nameof(innerQueueService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task SendToProcessingQueueAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // For outbox pattern, we need to extract correlation ID from the message
        // This is a simplified approach - in practice, you might need to deserialize
        // or extract correlation ID differently based on your message format
        var correlationId = Guid.NewGuid().ToString(); // Generate correlation ID for outbox

        // First, store the message in the outbox
        await _outboxService.AddMessageAsync("HL7Message", message, correlationId, cancellationToken);

        _logger.LogInformation(
            "Stored message in outbox. CorrelationId: {CorrelationId}",
            correlationId);

        // Then attempt to dispatch to the actual queue
        try
        {
            await _innerQueueService.SendToProcessingQueueAsync(message, cancellationToken);

            _logger.LogInformation(
                "Successfully dispatched message to processing queue. CorrelationId: {CorrelationId}",
                correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to dispatch message to processing queue, but stored in outbox for retry. CorrelationId: {CorrelationId}",
                correlationId);

            // Message remains in outbox as pending for the outbox dispatcher to handle
            // We don't throw here - the message is safely stored in the outbox
        }
    }

    /// <inheritdoc/>
    public async Task SendToPoisonQueueAsync(string message, int retryCount, CancellationToken cancellationToken = default)
    {
        await _innerQueueService.SendToPoisonQueueAsync(message, retryCount, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<QueueMessage> DeserializeMessageAsync(string message)
    {
        return await _innerQueueService.DeserializeMessageAsync(message);
    }

    /// <inheritdoc/>
    public async Task<string> SerializeMessageAsync(QueueMessage message)
    {
        return await _innerQueueService.SerializeMessageAsync(message);
    }

    /// <inheritdoc/>
    public async Task SendToDeadLetterQueueAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        await _innerQueueService.SendToDeadLetterQueueAsync(message, cancellationToken);
    }
}
