using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API;

/// <summary>
/// HTTP function that returns lab metadata for testing purposes.
/// In production, this would be replaced by an actual external API.
/// </summary>
public class GetLabMetadata
{
    private readonly ILogger<GetLabMetadata> _logger;

    public GetLabMetadata(ILogger<GetLabMetadata> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns mock lab metadata for the specified lab number.
    /// </summary>
    /// <param name="req">The HTTP request.</param>
    /// <returns>Lab metadata JSON or 404 if not found.</returns>
    [Function("GetLabMetadata")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "metadata")] HttpRequest req)
    {
        var labNumber = req.Query["labNumber"].ToString();

        _logger.LogInformation("Metadata API called for LabNumber: {LabNumber}", labNumber);

        if (string.IsNullOrWhiteSpace(labNumber))
        {
            _logger.LogWarning("LabNumber query parameter is missing or empty");
            return new BadRequestObjectResult(new { error = "labNumber query parameter is required" });
        }

        // Mock test data for LAB001, LAB002, LAB003
        var testData = labNumber.ToUpperInvariant() switch
        {
            "LAB001" => new
            {
                patientId = "P001",
                firstName = "John",
                lastName = "Doe",
                dateOfBirth = "1980-01-15",
                gender = "M",
                testType = "Blood Panel",
                collectionDate = "2025-11-25T14:30:00"
            },
            "LAB002" => new
            {
                patientId = "P002",
                firstName = "Jane",
                lastName = "Smith",
                dateOfBirth = "1975-05-22",
                gender = "F",
                testType = "Urinalysis",
                collectionDate = "2025-11-25T15:45:00"
            },
            "LAB003" => new
            {
                patientId = "P003",
                firstName = "Robert",
                lastName = "Johnson",
                dateOfBirth = "1990-09-10",
                gender = "M",
                testType = "X-Ray",
                collectionDate = "2025-11-25T16:00:00"
            },
            _ => null
        };

        if (testData == null)
        {
            _logger.LogWarning("Lab metadata not found for LabNumber: {LabNumber}", labNumber);
            return new NotFoundObjectResult(new { error = $"Lab metadata not found for lab number '{labNumber}'" });
        }

        _logger.LogInformation("Returning test metadata for LabNumber: {LabNumber}, PatientId: {PatientId}",
            labNumber, testData.patientId);

        return new OkObjectResult(testData);
    }
}
