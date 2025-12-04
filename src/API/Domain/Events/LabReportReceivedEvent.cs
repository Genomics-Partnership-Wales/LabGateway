using System;

namespace LabResultsGateway.API.Domain.Events;

/// <summary>
/// Domain event raised when a lab report blob is received and processing begins.
/// </summary>
public class LabReportReceivedEvent : DomainEventBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LabReportReceivedEvent"/> class.
    /// </summary>
    /// <param name="blobName">The name of the blob that was received.</param>
    /// <param name="contentSize">The size of the blob content in bytes.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    public LabReportReceivedEvent(string blobName, long contentSize, string correlationId)
        : base(correlationId)
    {
        BlobName = blobName ?? throw new ArgumentNullException(nameof(blobName));
        ContentSize = contentSize;
    }

    /// <summary>
    /// Gets the name of the blob that was received.
    /// </summary>
    public string BlobName { get; }

    /// <summary>
    /// Gets the size of the blob content in bytes.
    /// </summary>
    public long ContentSize { get; }
}
