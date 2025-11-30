using System;
using LabResultsGateway.API.Domain.ValueObjects;

namespace LabResultsGateway.API.Domain.Exceptions;

/// <summary>
/// Exception thrown when lab metadata cannot be found for a given lab number.
/// Metadata lookup failures may be transient (service unavailable) so retry is allowed.
/// </summary>
public class MetadataNotFoundException : LabProcessingException
{
    /// <summary>
    /// The lab number for which metadata was not found.
    /// </summary>
    public LabNumber LabNumber { get; }

    /// <summary>
    /// Metadata lookup failures may be transient (e.g., external service temporarily unavailable).
    /// </summary>
    public override bool IsRetryable => true;

    /// <summary>
    /// Initializes a new instance of the MetadataNotFoundException class.
    /// </summary>
    /// <param name="labNumber">The lab number for which metadata was not found.</param>
    /// <param name="message">The error message.</param>
    /// <param name="blobName">The name of the blob being processed.</param>
    public MetadataNotFoundException(LabNumber labNumber, string message, string? blobName = null)
        : base(message)
    {
        LabNumber = labNumber;
        BlobName = blobName;
    }

    /// <summary>
    /// Initializes a new instance of the MetadataNotFoundException class.
    /// </summary>
    /// <param name="labNumber">The lab number for which metadata was not found.</param>
    /// <param name="blobName">The name of the blob being processed.</param>
    public MetadataNotFoundException(LabNumber labNumber, string? blobName = null)
        : base($"Metadata not found for lab number '{labNumber}'.")
    {
        LabNumber = labNumber;
        BlobName = blobName;
    }
}
