using System;

namespace LabResultsGateway.API.Domain.Exceptions;

/// <summary>
/// Exception thrown when HL7 message generation fails.
/// Generation failures are typically due to data issues that cannot be resolved by retry.
/// </summary>
public class Hl7GenerationException : LabProcessingException
{
    /// <summary>
    /// The lab number associated with the failed HL7 generation.
    /// </summary>
    public string LabNumber { get; }

    /// <summary>
    /// HL7 generation failures are typically due to data format issues, not transient errors.
    /// </summary>
    public override bool IsRetryable => false;

    /// <summary>
    /// Initializes a new instance of the Hl7GenerationException class.
    /// </summary>
    /// <param name="labNumber">The lab number associated with the failed generation.</param>
    /// <param name="message">The error message.</param>
    /// <param name="blobName">The name of the blob being processed.</param>
    public Hl7GenerationException(string labNumber, string message, string? blobName = null)
        : base(message)
    {
        LabNumber = labNumber ?? throw new ArgumentNullException(nameof(labNumber));
        BlobName = blobName;
    }

    /// <summary>
    /// Initializes a new instance of the Hl7GenerationException class.
    /// </summary>
    /// <param name="labNumber">The lab number associated with the failed generation.</param>
    /// <param name="blobName">The name of the blob being processed.</param>
    public Hl7GenerationException(string labNumber, string? blobName = null)
        : base($"Failed to generate HL7 message for lab number '{labNumber}'.")
    {
        LabNumber = labNumber ?? throw new ArgumentNullException(nameof(labNumber));
        BlobName = blobName;
    }

    /// <summary>
    /// Initializes a new instance of the Hl7GenerationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    /// <param name="blobName">The name of the blob being processed.</param>
    public Hl7GenerationException(string message, Exception innerException, string? blobName = null)
        : base(message, innerException)
    {
        LabNumber = "Unknown";
        BlobName = blobName;
    }
}
