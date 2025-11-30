using FluentAssertions;
using LabResultsGateway.API.Domain.Exceptions;
using LabResultsGateway.API.Domain.ValueObjects;
using Xunit;

namespace LabResultsGateway.API.Tests.Domain.Exceptions;

/// <summary>
/// Unit tests for <see cref="MetadataNotFoundException"/>.
/// </summary>
public class MetadataNotFoundExceptionTests
{
    private static LabNumber CreateValidLabNumber() => new LabNumber("LAB123");

    [Fact]
    public void Constructor_SetsLabNumber()
    {
        // Arrange
        var labNumber = CreateValidLabNumber();

        // Act
        var exception = new MetadataNotFoundException(labNumber);

        // Assert
        exception.LabNumber.Should().Be(labNumber);
    }

    [Fact]
    public void Constructor_SetsDefaultMessage_WhenMessageNotProvided()
    {
        // Arrange
        var labNumber = CreateValidLabNumber();

        // Act
        var exception = new MetadataNotFoundException(labNumber);

        // Assert
        exception.Message.Should().Contain(labNumber.Value);
    }

    [Fact]
    public void Constructor_SetsCustomMessage_WhenProvided()
    {
        // Arrange
        var labNumber = CreateValidLabNumber();
        const string customMessage = "Custom error message";

        // Act - Note: 3-param constructor takes (labNumber, message, blobName)
        var exception = new MetadataNotFoundException(labNumber, customMessage, null);

        // Assert
        exception.Message.Should().Be(customMessage);
    }

    [Fact]
    public void Constructor_SetsBlobName_WhenProvided()
    {
        // Arrange
        var labNumber = CreateValidLabNumber();
        const string blobName = "test-blob.json";

        // Act - 2-param constructor: (labNumber, blobName)
        var exception = new MetadataNotFoundException(labNumber, blobName);

        // Assert
        exception.BlobName.Should().Be(blobName);
    }

    [Fact]
    public void IsRetryable_ReturnsTrue()
    {
        // Arrange
        var labNumber = CreateValidLabNumber();
        var exception = new MetadataNotFoundException(labNumber);

        // Act
        var isRetryable = exception.IsRetryable;

        // Assert
        isRetryable.Should().BeTrue("metadata might be available on retry");
    }

    [Fact]
    public void Exception_InheritsFromLabProcessingException()
    {
        // Arrange
        var labNumber = CreateValidLabNumber();
        var exception = new MetadataNotFoundException(labNumber);

        // Assert
        exception.Should().BeAssignableTo<LabProcessingException>();
    }

    [Fact]
    public void BlobName_DefaultsToNull_WhenNotProvided()
    {
        // Arrange
        var labNumber = CreateValidLabNumber();

        // Act
        var exception = new MetadataNotFoundException(labNumber);

        // Assert
        exception.BlobName.Should().BeNull();
    }

    [Fact]
    public void Constructor_AcceptsAllParameters()
    {
        // Arrange
        var labNumber = CreateValidLabNumber();
        const string message = "Custom message";
        const string blobName = "blob.json";

        // Act
        var exception = new MetadataNotFoundException(labNumber, message, blobName);

        // Assert
        exception.LabNumber.Should().Be(labNumber);
        exception.Message.Should().Be(message);
        exception.BlobName.Should().Be(blobName);
        exception.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public void IsRetryable_OverridesBaseClassDefault()
    {
        // Arrange
        var labNumber = CreateValidLabNumber();
        LabProcessingException exception = new MetadataNotFoundException(labNumber);

        // Act & Assert
        exception.IsRetryable.Should().BeTrue("MetadataNotFoundException overrides base class IsRetryable to true");
    }
}
