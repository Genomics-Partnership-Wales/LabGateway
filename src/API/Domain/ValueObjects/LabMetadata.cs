using System;

namespace LabResultsGateway.API.Domain.ValueObjects;

/// <summary>
/// Represents lab metadata retrieved from the external API.
/// This is an immutable record containing patient and test information.
/// Properties are TBD based on actual metadata API JSON schema.
/// </summary>
public record LabMetadata
{
    /// <summary>
    /// The patient's unique identifier.
    /// </summary>
    public required string PatientId { get; init; }

    /// <summary>
    /// The patient's first name.
    /// </summary>
    public required string FirstName { get; init; }

    /// <summary>
    /// The patient's last name.
    /// </summary>
    public required string LastName { get; init; }

    /// <summary>
    /// The patient's date of birth.
    /// </summary>
    public required DateTime DateOfBirth { get; init; }

    /// <summary>
    /// The patient's gender.
    /// </summary>
    public required string Gender { get; init; }

    /// <summary>
    /// The type of lab test performed.
    /// </summary>
    public required string TestType { get; init; }

    /// <summary>
    /// The date and time when the sample was collected.
    /// </summary>
    public required DateTimeOffset CollectionDate { get; init; }

    /// <summary>
    /// Returns the patient's full name by combining first and last names.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";
}
