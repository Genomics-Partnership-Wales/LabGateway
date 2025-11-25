using System.Text.Json.Serialization;

namespace LabResultsGateway.API.Application.DTOs;

/// <summary>
/// Data Transfer Object for lab metadata retrieved from external API.
/// This DTO represents the JSON response structure from the metadata API.
/// </summary>
public record LabMetadataDto
{
    /// <summary>
    /// The unique patient identifier.
    /// </summary>
    [JsonPropertyName("patientId")]
    public required string PatientId { get; init; }

    /// <summary>
    /// The patient's first name.
    /// </summary>
    [JsonPropertyName("firstName")]
    public required string FirstName { get; init; }

    /// <summary>
    /// The patient's last name.
    /// </summary>
    [JsonPropertyName("lastName")]
    public required string LastName { get; init; }

    /// <summary>
    /// The patient's date of birth.
    /// </summary>
    [JsonPropertyName("dateOfBirth")]
    public required DateTime DateOfBirth { get; init; }

    /// <summary>
    /// The patient's gender.
    /// </summary>
    [JsonPropertyName("gender")]
    public required string Gender { get; init; }

    /// <summary>
    /// The type of test performed.
    /// </summary>
    [JsonPropertyName("testType")]
    public required string TestType { get; init; }

    /// <summary>
    /// The date and time when the sample was collected.
    /// </summary>
    [JsonPropertyName("collectionDate")]
    public required DateTime CollectionDate { get; init; }

    /// <summary>
    /// Additional metadata fields that may be present in the API response.
    /// These are optional and may vary based on the actual API schema.
    /// </summary>
    [JsonPropertyName("additionalInfo")]
    public Dictionary<string, object>? AdditionalInfo { get; init; }
}
