using System;
using LabResultsGateway.API.Domain.ValueObjects;

namespace LabResultsGateway.API.Domain.Exceptions;

/// <summary>
/// Exception thrown when lab metadata cannot be found for a given lab number.
/// </summary>
public class MetadataNotFoundException : InvalidOperationException
{
    /// <summary>
    /// The lab number for which metadata was not found.
    /// </summary>
    public LabNumber LabNumber { get; }

    /// <summary>
    /// Initializes a new instance of the MetadataNotFoundException class.
    /// </summary>
    /// <param name="labNumber">The lab number for which metadata was not found.</param>
    /// <param name="message">The error message.</param>
    public MetadataNotFoundException(LabNumber labNumber, string message)
        : base(message)
    {
        LabNumber = labNumber;
    }

    /// <summary>
    /// Initializes a new instance of the MetadataNotFoundException class.
    /// </summary>
    /// <param name="labNumber">The lab number for which metadata was not found.</param>
    public MetadataNotFoundException(LabNumber labNumber)
        : base($"Metadata not found for lab number '{labNumber}'.")
    {
        LabNumber = labNumber;
    }
}
