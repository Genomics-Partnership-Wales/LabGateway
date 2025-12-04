using System;

namespace LabResultsGateway.API.Domain.Events;

/// <summary>
/// Domain event raised when an HL7 message is successfully generated from lab data.
/// </summary>
public class Hl7MessageGeneratedEvent : DomainEventBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Hl7MessageGeneratedEvent"/> class.
    /// </summary>
    /// <param name="labNumber">The lab number for which the HL7 message was generated.</param>
    /// <param name="messageLength">The length of the generated HL7 message in characters.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    public Hl7MessageGeneratedEvent(string labNumber, int messageLength, string correlationId)
        : base(correlationId)
    {
        LabNumber = labNumber ?? throw new ArgumentNullException(nameof(labNumber));
        MessageLength = messageLength;
    }

    /// <summary>
    /// Gets the lab number for which the HL7 message was generated.
    /// </summary>
    public string LabNumber { get; }

    /// <summary>
    /// Gets the length of the generated HL7 message in characters.
    /// </summary>
    public int MessageLength { get; }
}
