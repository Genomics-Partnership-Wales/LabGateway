using Azure.Storage.Queues.Models;

namespace LabResultsGateway.API.Infrastructure.Queue;

/// <summary>
/// Wrapper for Azure Queue message with additional metadata.
/// </summary>
public record QueueMessageWrapper(
    string MessageId,
    string PopReceipt,
    string MessageText,
    int DequeueCount);