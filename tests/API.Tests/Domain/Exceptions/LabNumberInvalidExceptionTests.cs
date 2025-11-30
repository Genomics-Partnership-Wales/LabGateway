using FluentAssertions;
using LabResultsGateway.API.Domain.Exceptions;
using Xunit;

namespace LabResultsGateway.API.Tests.Domain.Exceptions;

/// <summary>
/// Unit tests for <see cref="LabNumberInvalidException"/>.
/// </summary>
public class LabNumberInvalidExceptionTests
{
    [Fact]
    public void Constructor_SetsInvalidLabNumber()
    {
        // Arrange
        const string invalidLabNumber = "INVALID-123";

        // Act
        var exception = new LabNumberInvalidException(invalidLabNumber);

        // Assert
        exception.InvalidLabNumber.Should().Be(invalidLabNumber);
    }

    [Fact]
    public void Constructor_SetsDefaultMessage_WhenMessageNotProvided()
    {
        // Arrange
        const string invalidLabNumber = "INVALID-123";

        // Act
        var exception = new LabNumberInvalidException(invalidLabNumber);

        // Assert
        exception.Message.Should().Contain(invalidLabNumber);
    }

    [Fact]
    public void Constructor_SetsCustomMessage_WhenProvided()
    {
        // Arrange
        const string invalidLabNumber = "INVALID-123";
        const string customMessage = "Custom error message";

        // Act - Note: 3-param constructor takes (labNumber, message, blobName)
        var exception = new LabNumberInvalidException(invalidLabNumber, customMessage, null);

        // Assert
        exception.Message.Should().Be(customMessage);
    }

    [Fact]
    public void Constructor_SetsBlobName_WhenProvided()
    {
        // Arrange
        const string invalidLabNumber = "INVALID-123";
        const string blobName = "test-blob.json";

        // Act - 2-param constructor: (invalidLabNumber, blobName)
        var exception = new LabNumberInvalidException(invalidLabNumber, blobName);

        // Assert
        exception.BlobName.Should().Be(blobName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenInvalidLabNumberIsNull()
    {
        // Arrange
        string? nullLabNumber = null;

        // Act
        var act = () => new LabNumberInvalidException(nullLabNumber!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("invalidLabNumber");
    }

    [Fact]
    public void IsRetryable_ReturnsFalse()
    {
        // Arrange
        var exception = new LabNumberInvalidException("INVALID-123");

        // Act
        var isRetryable = exception.IsRetryable;

        // Assert
        isRetryable.Should().BeFalse("invalid lab numbers cannot be fixed by retrying");
    }

    [Fact]
    public void Exception_InheritsFromLabProcessingException()
    {
        // Arrange
        var exception = new LabNumberInvalidException("INVALID-123");

        // Assert
        exception.Should().BeAssignableTo<LabProcessingException>();
    }

    [Fact]
    public void BlobName_DefaultsToNull_WhenNotProvided()
    {
        // Arrange & Act
        var exception = new LabNumberInvalidException("INVALID-123");

        // Assert
        exception.BlobName.Should().BeNull();
    }

    [Fact]
    public void Constructor_AcceptsAllParameters()
    {
        // Arrange
        const string invalidLabNumber = "BAD-LAB";
        const string message = "Custom message";
        const string blobName = "blob.json";

        // Act
        var exception = new LabNumberInvalidException(invalidLabNumber, message, blobName);

        // Assert
        exception.InvalidLabNumber.Should().Be(invalidLabNumber);
        exception.Message.Should().Be(message);
        exception.BlobName.Should().Be(blobName);
        exception.IsRetryable.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_AcceptsEmptyOrWhitespaceLabNumber(string labNumber)
    {
        // Arrange & Act
        var exception = new LabNumberInvalidException(labNumber);

        // Assert
        exception.InvalidLabNumber.Should().Be(labNumber);
    }
}
