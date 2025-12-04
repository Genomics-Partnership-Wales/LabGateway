namespace LabResultsGateway.API.Domain.Enums;

/// <summary>
/// Represents the status of an outbox message.
/// </summary>
public enum OutboxStatus
{
    /// <summary>
    /// Message is pending dispatch.
    /// </summary>
    Pending,

    /// <summary>
    /// Message is currently being dispatched.
    /// </summary>
    Dispatching,

    /// <summary>
    /// Message has been successfully dispatched.
    /// </summary>
    Dispatched,

    /// <summary>
    /// Message dispatch failed and will be retried.
    /// </summary>
    Failed,

    /// <summary>
    /// Message has been abandoned after maximum retry attempts.
    /// </summary>
    Abandoned
}
