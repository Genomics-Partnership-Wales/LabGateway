using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Infrastructure.Queue;

/// <summary>
/// Azure Queue Storage client implementation.
/// </summary>
public class AzureQueueClient : IAzureQueueClient
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<AzureQueueClient> _logger;

    /// <summary>
    /// Initializes a new instance of the AzureQueueClient class.
    /// </summary>
    /// <param name="queueClient">The Azure Queue client.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public AzureQueueClient(QueueClient queueClient, ILogger<AzureQueueClient> logger)
    {
        _queueClient = queueClient ?? throw new ArgumentNullException(nameof(queueClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ensures the queue exists, creating it if necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task EnsureQueueExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure queue exists. QueueName: {QueueName}", _queueClient.Name);
            throw new InvalidOperationException($"Failed to ensure queue exists: {_queueClient.Name}", ex);
        }
    }

    /// <summary>
    /// Receives messages from the queue.
    /// </summary>
    /// <param name="maxMessages">Maximum number of messages to receive.</param>
    /// <param name="visibilityTimeout">Visibility timeout for the messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of queue message wrappers.</returns>
    public async Task<IReadOnlyList<QueueMessageWrapper>> ReceiveMessagesAsync(
        int maxMessages = 10,
        TimeSpan? visibilityTimeout = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _queueClient.ReceiveMessagesAsync(
                maxMessages: maxMessages,
                visibilityTimeout: visibilityTimeout,
                cancellationToken: cancellationToken);

            return response.Value?
                .Select(message => new QueueMessageWrapper(
                    message.MessageId,
                    message.PopReceipt,
                    message.MessageText,
                    message.DequeueCount))
                .ToList()
                ?? new List<QueueMessageWrapper>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to receive messages from queue. QueueName: {QueueName}", _queueClient.Name);
            throw new InvalidOperationException($"Failed to receive messages from queue: {_queueClient.Name}", ex);
        }
    }

    /// <summary>
    /// Deletes a message from the queue.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="popReceipt">The pop receipt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteMessageAsync(
        string messageId,
        string popReceipt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _queueClient.DeleteMessageAsync(messageId, popReceipt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete message. QueueName: {QueueName}, MessageId: {MessageId}",
                _queueClient.Name, messageId);
            throw new InvalidOperationException($"Failed to delete message: {messageId}", ex);
        }
    }

    /// <summary>
    /// Updates a message in the queue.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="popReceipt">The pop receipt.</param>
    /// <param name="messageText">The updated message text.</param>
    /// <param name="visibilityTimeout">The new visibility timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateMessageAsync(
        string messageId,
        string popReceipt,
        string messageText,
        TimeSpan visibilityTimeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _queueClient.UpdateMessageAsync(
                messageId,
                popReceipt,
                messageText: messageText,
                visibilityTimeout: visibilityTimeout,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update message. QueueName: {QueueName}, MessageId: {MessageId}",
                _queueClient.Name, messageId);
            throw new InvalidOperationException($"Failed to update message: {messageId}", ex);
        }
    }
}
