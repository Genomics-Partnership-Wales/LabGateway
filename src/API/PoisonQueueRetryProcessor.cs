using System.Diagnostics;
using LabResultsGateway.API.Application.Processing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API;

/// <summary>
/// Azure Function that retries failed HL7 messages from the poison queue.
/// Renamed from TimeTriggeredProcessor to PoisonQueueRetryProcessor for clarity.
/// </summary>
public class PoisonQueueRetryProcessor
{
    private readonly IPoisonQueueRetryOrchestrator _orchestrator;
    private readonly ILogger<PoisonQueueRetryProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the PoisonQueueRetryProcessor class.
    /// </summary>
    /// <param name="orchestrator">The poison queue retry orchestrator.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public PoisonQueueRetryProcessor(
        IPoisonQueueRetryOrchestrator orchestrator,
        ILogger<PoisonQueueRetryProcessor> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
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
        try
        {
            await _orchestrator.ProcessPoisonQueueAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in poison queue retry processor");
            throw;
        }
    }
}
