using System;
using LabResultsGateway.API.Domain.ValueObjects;

namespace LabResultsGateway.API.Domain.Exceptions;

/// <summary>
/// Exception thrown when a lab number is invalid.
/// </summary>
public class LabNumberInvalidException : ArgumentException
{
    /// <summary>
    /// The invalid lab number value.
    /// </summary>
    public string InvalidLabNumber { get; }

    /// <summary>
    /// Initializes a new instance of the LabNumberInvalidException class.
    /// </summary>
    /// <param name="invalidLabNumber">The invalid lab number value.</param>
    /// <param name="message">The error message.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    public LabNumberInvalidException(string invalidLabNumber, string message, string paramName)
        : base(message, paramName)
    {
        InvalidLabNumber = invalidLabNumber ?? throw new ArgumentNullException(nameof(invalidLabNumber));
    }

    /// <summary>
    /// Initializes a new instance of the LabNumberInvalidException class.
    /// </summary>
    /// <param name="invalidLabNumber">The invalid lab number value.</param>
    /// <param name="message">The error message.</param>
    public LabNumberInvalidException(string invalidLabNumber, string message)
        : base(message)
    {
        InvalidLabNumber = invalidLabNumber ?? throw new ArgumentNullException(nameof(invalidLabNumber));
    }

    /// <summary>
    /// Initializes a new instance of the LabNumberInvalidException class.
    /// </summary>
    /// <param name="invalidLabNumber">The invalid lab number value.</param>
    public LabNumberInvalidException(string invalidLabNumber)
        : base($"The lab number '{invalidLabNumber}' is invalid.")
    {
        InvalidLabNumber = invalidLabNumber ?? throw new ArgumentNullException(nameof(invalidLabNumber));
    }
}
