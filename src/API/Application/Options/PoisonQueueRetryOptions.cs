namespace LabResultsGateway.API.Application.Options;

/// <summary>
/// Configuration options for poison queue retry processing.
/// </summary>
public class PoisonQueueRetryOptions
{
    /// <summary>
    /// The configuration section name for poison queue retry options.
    /// </summary>
    public const string SectionName = "PoisonQueueRetry";

    /// <summary>
    /// Gets or sets the maximum number of messages to process per batch.
    /// Default value is 10.
    /// </summary>
    public int MaxMessagesPerBatch { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for a message.
    /// Default value is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base retry delay in minutes.
    /// Default value is 2 minutes.
    /// </summary>
    public double BaseRetryDelayMinutes { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the processing visibility timeout in minutes.
    /// Default value is 5 minutes.
    /// </summary>
    public double ProcessingVisibilityTimeoutMinutes { get; set; } = 5.0;

    /// <summary>
    /// Gets or sets a value indicating whether to use jitter in retry delays.
    /// Default value is true.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum jitter percentage to apply to retry delays.
    /// Default value is 0.3 (30%).
    /// </summary>
    public double MaxJitterPercentage { get; set; } = 0.3;
}
