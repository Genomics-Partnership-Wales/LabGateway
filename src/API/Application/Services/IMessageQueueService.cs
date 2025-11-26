using LabResultsGateway.API.Application.DTOs;

namespace LabResultsGateway.API.Application.Services;

/// <summary>
/// Service interface for Azure Queue Storage operations.
/// Handles sending messages to processing and poison queues.
/// </summary>
public interface IMessageQueueService
{
    /// <summary>
    /// Sends an HL7 message to the processing queue for immediate processing.
    /// </summary>
    /// <param name="message">The HL7 message content to send.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when queue operations fail.</exception>
    Task SendToProcessingQueueAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a failed message to the poison queue with retry metadata.
    /// </summary>
    /// <param name="message">The HL7 message content that failed processing.</param>
    /// <param name="retryCount">The current retry count for this message.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when retryCount is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when queue operations fail.</exception>
    Task SendToPoisonQueueAsync(string message, int retryCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes a queue message string into a QueueMessage object.
    /// </summary>
    /// <param name="message">The serialized message string.</param>
    /// <returns>The deserialized QueueMessage.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
    /// <exception cref="JsonException">Thrown when deserialization fails.</exception>
    Task<QueueMessage> DeserializeMessageAsync(string message);

    /// <summary>
    /// Serializes a QueueMessage into a string for queue storage.
    /// </summary>
    /// <param name="message">The QueueMessage to serialize.</param>
    /// <returns>The serialized message string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
    /// <exception cref="JsonException">Thrown when serialization fails.</exception>
    Task<string> SerializeMessageAsync(QueueMessage message);

    /// <summary>
    /// Sends a message to the dead letter queue for permanent failure handling.
    /// </summary>
    /// <param name="message">The DeadLetterMessage to send.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when queue operations fail.</exception>
    Task SendToDeadLetterQueueAsync(DeadLetterMessage message, CancellationToken cancellationToken = default);
}
