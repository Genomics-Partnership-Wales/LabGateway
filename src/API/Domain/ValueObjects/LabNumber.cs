using System;
using System.Text.RegularExpressions;

namespace LabResultsGateway.API.Domain.ValueObjects;

/// <summary>
/// Represents a lab number value object with validation and immutability.
/// Lab numbers must be non-empty and contain only alphanumeric characters.
/// </summary>
public readonly struct LabNumber : IEquatable<LabNumber>
{
    private static readonly Regex LabNumberPattern = new Regex("^[a-zA-Z0-9]+$", RegexOptions.Compiled);

    /// <summary>
    /// The lab number value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the LabNumber value object.
    /// </summary>
    /// <param name="value">The lab number string value.</param>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty, or contains invalid characters.</exception>
    public LabNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Lab number cannot be null or empty.", nameof(value));
        }

        if (!LabNumberPattern.IsMatch(value))
        {
            throw new ArgumentException("Lab number must contain only alphanumeric characters.", nameof(value));
        }

        Value = value.Trim();
    }

    /// <summary>
    /// Returns a string representation of the lab number.
    /// </summary>
    /// <returns>The lab number value.</returns>
    public override string ToString() => Value;

    /// <summary>
    /// Determines whether the specified object is equal to the current LabNumber.
    /// </summary>
    /// <param name="obj">The object to compare with the current LabNumber.</param>
    /// <returns>true if the specified object is equal to the current LabNumber; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is LabNumber other && Equals(other);

    /// <summary>
    /// Determines whether the specified LabNumber is equal to the current LabNumber.
    /// </summary>
    /// <param name="other">The LabNumber to compare with the current LabNumber.</param>
    /// <returns>true if the specified LabNumber is equal to the current LabNumber; otherwise, false.</returns>
    public bool Equals(LabNumber other) => Value == other.Value;

    /// <summary>
    /// Returns the hash code for this LabNumber.
    /// </summary>
    /// <returns>A hash code for the current LabNumber.</returns>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Determines whether two specified LabNumber instances are equal.
    /// </summary>
    /// <param name="left">The first LabNumber to compare.</param>
    /// <param name="right">The second LabNumber to compare.</param>
    /// <returns>true if the two LabNumber instances are equal; otherwise, false.</returns>
    public static bool operator ==(LabNumber left, LabNumber right) => left.Equals(right);

    /// <summary>
    /// Determines whether two specified LabNumber instances are not equal.
    /// </summary>
    /// <param name="left">The first LabNumber to compare.</param>
    /// <param name="right">The second LabNumber to compare.</param>
    /// <returns>true if the two LabNumber instances are not equal; otherwise, false.</returns>
    public static bool operator !=(LabNumber left, LabNumber right) => !left.Equals(right);

    /// <summary>
    /// Defines an implicit conversion from LabNumber to string.
    /// </summary>
    /// <param name="labNumber">The LabNumber to convert.</param>
    /// <returns>The string representation of the LabNumber.</returns>
    public static implicit operator string(LabNumber labNumber) => labNumber.Value;

    /// <summary>
    /// Defines an explicit conversion from string to LabNumber.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>A new LabNumber instance.</returns>
    public static explicit operator LabNumber(string value) => new LabNumber(value);
}
