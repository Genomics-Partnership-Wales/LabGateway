using LabResultsGateway.API.Domain.Enums;

namespace LabResultsGateway.API.Domain.Entities;

/// <summary>
/// Represents a message stored in the outbox for reliable delivery.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Gets or sets the unique identifier for the outbox message.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the type of message (e.g., "HL7Message", "Notification").
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON payload of the message.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of the outbox message.
    /// </summary>
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// Gets or sets the timestamp when the message was dispatched (nullable).
    /// </summary>
    public DateTimeOffset? DispatchedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts made.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the correlation ID for tracking the message across the system.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message if the dispatch failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the next retry attempt.
    /// </summary>
    public DateTimeOffset? NextRetryAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message will be abandoned if retries fail.
    /// </summary>
    public DateTimeOffset? AbandonAt { get; set; }
}
