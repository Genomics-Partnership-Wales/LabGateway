using LabResultsGateway.API.Domain.Entities;

namespace LabResultsGateway.API.Application.Services;

/// <summary>
/// Service interface for managing outbox messages.
/// </summary>
public interface IOutboxService
{
    /// <summary>
    /// Adds a new message to the outbox.
    /// </summary>
    /// <param name="messageType">The type of message (e.g., "HL7Message").</param>
    /// <param name="payload">The JSON payload of the message.</param>
    /// <param name="correlationId">The correlation ID for tracking.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddMessageAsync(string messageType, string payload, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending messages that are ready for dispatch.
    /// </summary>
    /// <param name="maxCount">Maximum number of messages to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of pending outbox messages.</returns>
    Task<IList<OutboxMessage>> GetPendingMessagesAsync(int maxCount = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as dispatched.
    /// </summary>
    /// <param name="id">The message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkAsDispatchedAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as failed and schedules retry.
    /// </summary>
    /// <param name="id">The message ID.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkAsFailedAsync(string id, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old dispatched messages based on retention policy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of messages cleaned up.</returns>
    Task<int> CleanupOldMessagesAsync(CancellationToken cancellationToken = default);
}
