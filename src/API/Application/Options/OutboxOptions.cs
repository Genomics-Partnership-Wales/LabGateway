namespace LabResultsGateway.API.Application.Options;

/// <summary>
/// Configuration options for the outbox pattern implementation.
/// </summary>
public class OutboxOptions
{
    /// <summary>
    /// Gets or sets the name of the Azure Table Storage table used for the outbox.
    /// </summary>
    public string TableName { get; set; } = "OutboxMessages";

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed dispatches.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay in seconds between retry attempts (used for exponential backoff).
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the retention period in days for dispatched messages before cleanup.
    /// </summary>
    public int CleanupRetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of messages to process in a single batch.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the timeout in seconds for individual dispatch operations.
    /// </summary>
    public int DispatchTimeoutSeconds { get; set; } = 30;
}
