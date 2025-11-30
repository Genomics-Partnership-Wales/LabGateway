using System;

namespace LabResultsGateway.API.Domain.Exceptions;

/// <summary>
/// Exception thrown when a lab number is invalid.
/// Invalid lab numbers indicate a data format issue that cannot be resolved by retry.
/// </summary>
public class LabNumberInvalidException : LabProcessingException
{
    /// <summary>
    /// The invalid lab number value.
    /// </summary>
    public string InvalidLabNumber { get; }

    /// <summary>
    /// Invalid lab numbers are not retryable - the data format must be corrected at the source.
    /// </summary>
    public override bool IsRetryable => false;

    /// <summary>
    /// Initializes a new instance of the LabNumberInvalidException class.
    /// </summary>
    /// <param name="invalidLabNumber">The invalid lab number value.</param>
    /// <param name="message">The error message.</param>
    /// <param name="blobName">The name of the blob being processed.</param>
    public LabNumberInvalidException(string invalidLabNumber, string message, string? blobName = null)
        : base(message)
    {
        InvalidLabNumber = invalidLabNumber ?? throw new ArgumentNullException(nameof(invalidLabNumber));
        BlobName = blobName;
    }

    /// <summary>
    /// Initializes a new instance of the LabNumberInvalidException class.
    /// </summary>
    /// <param name="invalidLabNumber">The invalid lab number value.</param>
    /// <param name="blobName">The name of the blob being processed.</param>
    public LabNumberInvalidException(string invalidLabNumber, string? blobName = null)
        : base($"The lab number '{invalidLabNumber}' is invalid.")
    {
        InvalidLabNumber = invalidLabNumber ?? throw new ArgumentNullException(nameof(invalidLabNumber));
        BlobName = blobName;
    }
}
