using System.Collections.Generic;
using System.Threading.Tasks;

namespace LabResultsGateway.API.Infrastructure.Queue;

/// <summary>
/// Wrapper for Azure Queue message data.
/// </summary>
public record QueueMessageWrapper(
    string MessageId,
    string PopReceipt,
    string MessageText,
    int DequeueCount);

/// <summary>
/// Interface for Azure Queue operations.
/// </summary>
public interface IAzureQueueClient
{
    /// <summary>
    /// Ensures that the queue exists, creating it if necessary.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnsureQueueExistsAsync();

    /// <summary>
    /// Receives messages from the queue.
    /// </summary>
    /// <param name="maxMessages">The maximum number of messages to receive.</param>
    /// <param name="visibilityTimeout">The visibility timeout for the messages.</param>
    /// <returns>A read-only list of queue message wrappers.</returns>
    Task<IReadOnlyList<QueueMessageWrapper>> ReceiveMessagesAsync(int maxMessages, TimeSpan visibilityTimeout);

    /// <summary>
    /// Deletes a message from the queue.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="popReceipt">The pop receipt.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteMessageAsync(string messageId, string popReceipt);

    /// <summary>
    /// Updates a message in the queue.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="popReceipt">The pop receipt.</param>
    /// <param name="messageText">The updated message text.</param>
    /// <param name="visibilityTimeout">The new visibility timeout.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateMessageAsync(string messageId, string popReceipt, string messageText, TimeSpan visibilityTimeout);
}
