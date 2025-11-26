using System.Text;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Exceptions;
using LabResultsGateway.API.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NHapi.Base.Parser;
using NHapi.Model.V251.Message;
using NHapi.Model.V251.Segment;

namespace LabResultsGateway.API.Infrastructure.Hl7;

/// <summary>
/// Implementation of IHl7MessageBuilder that builds HL7 v2.5.1 ORU^R01 messages using NHapi.
/// </summary>
public class Hl7MessageBuilder : IHl7MessageBuilder
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<Hl7MessageBuilder> _logger;

    /// <summary>
    /// Initializes a new instance of the Hl7MessageBuilder class.
    /// </summary>
    /// <param name="configuration">Configuration containing HL7 segment values.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public Hl7MessageBuilder(IConfiguration configuration, ILogger<Hl7MessageBuilder> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Builds an HL7 v2.5.1 ORU^R01 message from the lab report data.
    /// </summary>
    /// <param name="labReport">The lab report containing data to include in the message.</param>
    /// <returns>The HL7 message as a pipe-delimited string.</returns>
    /// <exception cref="Hl7GenerationException">Thrown when HL7 message generation fails.</exception>
    public string BuildOruR01Message(LabReport labReport)
    {
        ArgumentNullException.ThrowIfNull(labReport);

        _logger.LogInformation("Building HL7 ORU^R01 message for LabNumber: {LabNumber}, CorrelationId: {CorrelationId}",
            labReport.LabNumber, labReport.CorrelationId);

        try
        {
            // Create ORU^R01 message
            var oruMessage = new ORU_R01();

            // Populate MSH segment
            PopulateMshSegment(oruMessage.MSH, labReport.CorrelationId);

            // Populate PID segment
            PopulatePidSegment(oruMessage.GetPATIENT_RESULT().PATIENT.PID, labReport.Metadata);

            // Populate OBR segment
            PopulateObrSegment(oruMessage.GetPATIENT_RESULT().GetORDER_OBSERVATION().OBR, labReport.Metadata);

            // Populate OBX segment with PDF content
            PopulateObxSegment(oruMessage.GetPATIENT_RESULT().GetORDER_OBSERVATION().GetOBSERVATION().OBX, labReport);

            // Encode message using PipeParser
            var pipeParser = new PipeParser();
            var hl7Message = pipeParser.Encode(oruMessage);

            _logger.LogInformation("Successfully built HL7 message for LabNumber: {LabNumber}, MessageLength: {Length} characters",
                labReport.LabNumber, hl7Message.Length);

            return hl7Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build HL7 message for LabNumber: {LabNumber}, CorrelationId: {CorrelationId}",
                labReport.LabNumber, labReport.CorrelationId);
            throw new Hl7GenerationException($"Failed to build HL7 message: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Populates the MSH (Message Header) segment.
    /// </summary>
    private void PopulateMshSegment(MSH msh, string correlationId)
    {
        // MSH-1: Field Separator (|)
        msh.FieldSeparator.Value = "|";

        // MSH-2: Encoding Characters (^~\&)
        msh.EncodingCharacters.Value = "^~\\&";

        // MSH-3: Sending Application (from config)
        msh.SendingApplication.NamespaceID.Value = _configuration["MSH_SendingApplication"] ?? "LABGATEWAY";

        // MSH-4: Sending Facility (from config)
        msh.SendingFacility.NamespaceID.Value = _configuration["MSH_SendingFacility"] ?? "LOCAL";

        // MSH-5: Receiving Application (from config)
        msh.ReceivingApplication.NamespaceID.Value = _configuration["MSH_ReceivingApplication"] ?? "WRRS";

        // MSH-6: Receiving Facility (from config)
        msh.ReceivingFacility.NamespaceID.Value = _configuration["MSH_ReceivingFacility"] ?? "WALESNHS";

        // MSH-7: Date/Time of Message
        msh.DateTimeOfMessage.Time.Value = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");

        // MSH-9: Message Type (ORU^R01)
        msh.MessageType.MessageCode.Value = "ORU";
        msh.MessageType.TriggerEvent.Value = "R01";

        // MSH-10: Message Control ID (use correlation ID)
        msh.MessageControlID.Value = correlationId;

        // MSH-11: Processing ID (from config, T for test/UAT)
        msh.ProcessingID.ProcessingID.Value = _configuration["MSH_ProcessingId"] ?? "T";

        // MSH-12: Version ID (2.5.1)
        msh.VersionID.VersionID.Value = "2.5.1";
    }

    /// <summary>
    /// Populates the PID (Patient Identification) segment.
    /// </summary>
    private void PopulatePidSegment(PID pid, LabMetadata metadata)
    {
        // PID-3: Patient Identifier List (Patient ID)
        pid.GetPatientIdentifierList(0).IDNumber.Value = metadata.PatientId;

        // PID-5: Patient Name (Last^First)
        pid.GetPatientName(0).FamilyName.Surname.Value = metadata.LastName;
        pid.GetPatientName(0).GivenName.Value = metadata.FirstName;

        // PID-7: Date/Time of Birth
        pid.DateTimeOfBirth.Time.Value = metadata.DateOfBirth.ToString("yyyyMMdd");

        // PID-8: Administrative Sex
        pid.AdministrativeSex.Value = metadata.Gender switch
        {
            "Male" => "M",
            "Female" => "F",
            "Other" => "O",
            "Unknown" => "U",
            _ => "U"
        };
    }

    /// <summary>
    /// Populates the OBR (Observation Request) segment.
    /// </summary>
    private void PopulateObrSegment(OBR obr, LabMetadata metadata)
    {
        // OBR-4: Universal Service Identifier (Test Type)
        obr.UniversalServiceIdentifier.Identifier.Value = metadata.TestType;

        // OBR-7: Observation Date/Time (Collection Date)
        obr.ObservationDateTime.Time.Value = metadata.CollectionDate.ToString("yyyyMMddHHmmss");
    }

    /// <summary>
    /// Populates the OBX (Observation/Result) segment with PDF content.
    /// </summary>
    private void PopulateObxSegment(OBX obx, LabReport labReport)
    {
        // OBX-1: Set ID (1)
        obx.SetIDOBX.Value = "1";

        // OBX-2: Value Type (ED for Encapsulated Data)
        obx.ValueType.Value = "ED";

        // OBX-5: Observation Value (Base64-encoded PDF as ED type)
        var base64Pdf = Convert.ToBase64String(labReport.PdfContent);
        var ed = obx.GetObservationValue(0).Data as NHapi.Model.V251.Datatype.ED;
        if (ed != null)
        {
            ed.SourceApplication.NamespaceID.Value = "LABGATEWAY";
            ed.TypeOfData.Value = "PDF";
            ed.DataSubtype.Value = "PDF";
            ed.Encoding.Value = "Base64";
            ed.Data.Value = base64Pdf;
        }

        // OBX-11: Observation Result Status (F for Final)
        obx.ObservationResultStatus.Value = "F";
    }
}
