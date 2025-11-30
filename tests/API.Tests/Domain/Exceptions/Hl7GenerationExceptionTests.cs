using FluentAssertions;
using LabResultsGateway.API.Domain.Exceptions;
using Xunit;

namespace LabResultsGateway.API.Tests.Domain.Exceptions;

/// <summary>
/// Unit tests for <see cref="Hl7GenerationException"/>.
/// </summary>
public class Hl7GenerationExceptionTests
{
    [Fact]
    public void Constructor_WithLabNumber_SetsLabNumber()
    {
        // Arrange
        const string labNumber = "LAB123";

        // Act
        var exception = new Hl7GenerationException(labNumber);

        // Assert
        exception.LabNumber.Should().Be(labNumber);
    }

    [Fact]
    public void Constructor_WithLabNumber_SetsDefaultMessage_WhenMessageNotProvided()
    {
        // Arrange
        const string labNumber = "LAB123";

        // Act
        var exception = new Hl7GenerationException(labNumber);

        // Assert
        exception.Message.Should().Contain(labNumber);
    }

    [Fact]
    public void Constructor_WithLabNumber_SetsCustomMessage_WhenProvided()
    {
        // Arrange
        const string labNumber = "LAB123";
        const string customMessage = "Custom error message";

        // Act - Note: 3-param constructor takes (labNumber, message, blobName)
        var exception = new Hl7GenerationException(labNumber, customMessage, null);

        // Assert
        exception.Message.Should().Be(customMessage);
    }

    [Fact]
    public void Constructor_WithLabNumber_SetsBlobName_WhenProvided()
    {
        // Arrange
        const string labNumber = "LAB123";
        const string blobName = "test-blob.json";

        // Act - 2-param constructor: (labNumber, blobName)
        var exception = new Hl7GenerationException(labNumber, blobName);

        // Assert
        exception.BlobName.Should().Be(blobName);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsLabNumberToUnknown()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        const string message = "HL7 generation failed";

        // Act
        var exception = new Hl7GenerationException(message, innerException);

        // Assert
        exception.LabNumber.Should().Be("Unknown");
    }

    [Fact]
    public void Constructor_WithInnerException_SetsInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        const string message = "HL7 generation failed";

        // Act
        var exception = new Hl7GenerationException(message, innerException);

        // Assert
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsBlobName_WhenProvided()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        const string message = "HL7 generation failed";
        const string blobName = "test-blob.json";

        // Act
        var exception = new Hl7GenerationException(message, innerException, blobName);

        // Assert
        exception.BlobName.Should().Be(blobName);
    }

    [Fact]
    public void IsRetryable_ReturnsFalse()
    {
        // Arrange
        var exception = new Hl7GenerationException("LAB123");

        // Act
        var isRetryable = exception.IsRetryable;

        // Assert
        isRetryable.Should().BeFalse("HL7 generation errors are typically data-related and not fixable by retrying");
    }

    [Fact]
    public void Exception_InheritsFromLabProcessingException()
    {
        // Arrange
        var exception = new Hl7GenerationException("LAB123");

        // Assert
        exception.Should().BeAssignableTo<LabProcessingException>();
    }

    [Fact]
    public void BlobName_DefaultsToNull_WhenNotProvided()
    {
        // Arrange & Act
        var exception = new Hl7GenerationException("LAB123");

        // Assert
        exception.BlobName.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithLabNumber_AcceptsAllParameters()
    {
        // Arrange
        const string labNumber = "LAB456";
        const string message = "Custom message";
        const string blobName = "blob.json";

        // Act
        var exception = new Hl7GenerationException(labNumber, message, blobName);

        // Assert
        exception.LabNumber.Should().Be(labNumber);
        exception.Message.Should().Be(message);
        exception.BlobName.Should().Be(blobName);
        exception.IsRetryable.Should().BeFalse();
    }
}
