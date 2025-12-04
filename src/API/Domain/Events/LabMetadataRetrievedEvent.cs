using System;

namespace LabResultsGateway.API.Domain.Events;

/// <summary>
/// Domain event raised when lab metadata is successfully retrieved from the external API.
/// </summary>
public class LabMetadataRetrievedEvent : DomainEventBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LabMetadataRetrievedEvent"/> class.
    /// </summary>
    /// <param name="labNumber">The lab number that was processed.</param>
    /// <param name="patientId">The patient ID associated with the lab report.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    public LabMetadataRetrievedEvent(string labNumber, string patientId, string correlationId)
        : base(correlationId)
    {
        LabNumber = labNumber ?? throw new ArgumentNullException(nameof(labNumber));
        PatientId = patientId ?? throw new ArgumentNullException(nameof(patientId));
    }

    /// <summary>
    /// Gets the lab number that was processed.
    /// </summary>
    public string LabNumber { get; }

    /// <summary>
    /// Gets the patient ID associated with the lab report.
    /// </summary>
    public string PatientId { get; }
}
