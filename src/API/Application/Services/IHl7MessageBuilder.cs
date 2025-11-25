using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Exceptions;

namespace LabResultsGateway.API.Application.Services;

/// <summary>
/// Service interface for building HL7 v2.5.1 ORU^R01 messages from lab report data.
/// </summary>
public interface IHl7MessageBuilder
{
    /// <summary>
    /// Builds an HL7 v2.5.1 ORU^R01 message from the provided lab report data.
    /// The message includes patient information, test details, and the PDF content as Base64-encoded data in OBX-5.
    /// </summary>
    /// <param name="labReport">The lab report entity containing all necessary data for HL7 message construction.</param>
    /// <returns>The complete HL7 v2.5.1 ORU^R01 message as a pipe-delimited string.</returns>
    /// <exception cref="Hl7GenerationException">Thrown when HL7 message construction fails due to invalid data or configuration issues.</exception>
    /// <exception cref="ArgumentNullException">Thrown when labReport is null.</exception>
    string BuildOruR01Message(LabReport labReport);
}
