using System;

namespace LabResultsGateway.API.Domain.Exceptions;

/// <summary>
/// Exception thrown when HL7 message generation fails.
/// </summary>
public class Hl7GenerationException : InvalidOperationException
{
    /// <summary>
    /// The lab number associated with the failed HL7 generation.
    /// </summary>
    public string LabNumber { get; }

    /// <summary>
    /// Initializes a new instance of the Hl7GenerationException class.
    /// </summary>
    /// <param name="labNumber">The lab number associated with the failed generation.</param>
    /// <param name="message">The error message.</param>
    public Hl7GenerationException(string labNumber, string message)
        : base(message)
    {
        LabNumber = labNumber ?? throw new ArgumentNullException(nameof(labNumber));
    }

    /// <summary>
    /// Initializes a new instance of the Hl7GenerationException class.
    /// </summary>
    /// <param name="labNumber">The lab number associated with the failed generation.</param>
    public Hl7GenerationException(string labNumber)
        : base($"Failed to generate HL7 message for lab number '{labNumber}'.")
    {
        LabNumber = labNumber ?? throw new ArgumentNullException(nameof(labNumber));
    }

    /// <summary>
    /// Initializes a new instance of the Hl7GenerationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public Hl7GenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
        LabNumber = "Unknown"; // Lab number may not be available in some error scenarios
    }
}
