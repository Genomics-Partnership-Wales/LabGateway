using LabResultsGateway.API.Application.Retry;
using LabResultsGateway.API.Infrastructure.Queue;

namespace LabResultsGateway.API.Application.Processing;

/// <summary>
/// Result of processing a poison queue message.
/// </summary>
public record MessageProcessingResult(
    bool Success,
    RetryResult Result,
    string? ErrorMessage = null);

/// <summary>
/// Interface for processing poison queue messages.
/// </summary>
public interface IPoisonQueueMessageProcessor
{
    /// <summary>
    /// Processes a poison queue message.
    /// </summary>
    /// <param name="message">The queue message wrapper to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of processing the message.</returns>
    Task<MessageProcessingResult> ProcessMessageAsync(QueueMessageWrapper message, CancellationToken cancellationToken = default);
}
