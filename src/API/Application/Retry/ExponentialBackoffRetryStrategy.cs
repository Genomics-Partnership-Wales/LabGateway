using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Application.Retry;

/// <summary>
/// Implements exponential backoff with jitter retry strategy.
/// </summary>
public class ExponentialBackoffRetryStrategy : IRetryStrategy
{
    private readonly PoisonQueueRetryOptions _options;
    private readonly ILogger<ExponentialBackoffRetryStrategy> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the ExponentialBackoffRetryStrategy class.
    /// </summary>
    /// <param name="options">Retry configuration options.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public ExponentialBackoffRetryStrategy(
        PoisonQueueRetryOptions options,
        ILogger<ExponentialBackoffRetryStrategy> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Determines if a retry should be attempted.
    /// </summary>
    /// <param name="context">The retry context.</param>
    /// <returns>True if retry should be attempted, false otherwise.</returns>
    public bool ShouldRetry(RetryContext context)
    {
        return context.CurrentRetryCount < context.MaxRetryAttempts;
    }

    /// <summary>
    /// Calculates the next retry delay using exponential backoff with optional jitter.
    /// </summary>
    /// <param name="context">The retry context.</param>
    /// <returns>The delay before the next retry attempt.</returns>
    public TimeSpan CalculateNextRetryDelay(RetryContext context)
    {
        // Calculate base delay: base^(retry + 1)
        var baseDelay = Math.Pow(_options.BaseRetryDelayMinutes, context.CurrentRetryCount + 1);
        var delayMinutes = baseDelay;

        // Apply jitter if enabled
        if (_options.UseJitter)
        {
            var jitterFactor = 1 + (_random.NextDouble() * _options.MaxJitterPercentage);
            delayMinutes *= jitterFactor;
        }

        var delay = TimeSpan.FromMinutes(delayMinutes);

        _logger.LogInformation(
            "Calculated retry delay. CorrelationId: {CorrelationId}, CurrentRetry: {CurrentRetry}, MaxRetries: {MaxRetries}, Delay: {DelayMinutes:F2} minutes",
            context.CorrelationId,
            context.CurrentRetryCount,
            context.MaxRetryAttempts,
            delay.TotalMinutes);

        return delay;
    }
}