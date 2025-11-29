namespace LabResultsGateway.API.Application.Processing;

/// <summary>
/// Interface for orchestrating poison queue retry operations.
/// </summary>
public interface IPoisonQueueRetryOrchestrator
{
    /// <summary>
    /// Processes messages from the poison queue.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessPoisonQueueAsync(CancellationToken cancellationToken);
}
