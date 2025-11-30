using FluentAssertions;
using LabResultsGateway.API.Domain.Exceptions;
using Xunit;

namespace LabResultsGateway.API.Tests.Domain.Exceptions;

/// <summary>
/// Unit tests for <see cref="LabProcessingException"/> base class.
/// </summary>
public class LabProcessingExceptionTests
{
    /// <summary>
    /// Concrete implementation of LabProcessingException for testing purposes.
    /// </summary>
    private class TestLabProcessingException : LabProcessingException
    {
        public TestLabProcessingException() : base() { }
        public TestLabProcessingException(string message) : base(message) { }
        public TestLabProcessingException(string message, Exception innerException) : base(message, innerException) { }
    }

    [Fact]
    public void IsRetryable_ReturnsDefaultFalse()
    {
        // Arrange
        var exception = new TestLabProcessingException("Test error");

        // Act
        var isRetryable = exception.IsRetryable;

        // Assert
        isRetryable.Should().BeFalse("default IsRetryable should be false for safety");
    }

    [Fact]
    public void BlobName_CanBeSetViaInitOnlyProperty()
    {
        // Arrange & Act
        var exception = new TestLabProcessingException("Test error")
        {
            BlobName = "test-blob.json"
        };

        // Assert
        exception.BlobName.Should().Be("test-blob.json");
    }

    [Fact]
    public void BlobName_DefaultsToNull()
    {
        // Arrange & Act
        var exception = new TestLabProcessingException("Test error");

        // Assert
        exception.BlobName.Should().BeNull();
    }

    [Fact]
    public void Message_IsSetCorrectly()
    {
        // Arrange
        const string expectedMessage = "Test error message";

        // Act
        var exception = new TestLabProcessingException(expectedMessage);

        // Assert
        exception.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void InnerException_IsSetCorrectly()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        const string message = "Outer error";

        // Act
        var exception = new TestLabProcessingException(message, innerException);

        // Assert
        exception.InnerException.Should().BeSameAs(innerException);
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void LabProcessingException_InheritsFromException()
    {
        // Arrange
        var exception = new TestLabProcessingException("Test error");

        // Assert
        exception.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void DerivedExceptions_CanBeCaughtAsLabProcessingException()
    {
        // Arrange
        LabProcessingException? caughtException = null;

        // Act
        try
        {
            throw new TestLabProcessingException("Test error") { BlobName = "test.json" };
        }
        catch (LabProcessingException ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.Should().NotBeNull();
        caughtException!.BlobName.Should().Be("test.json");
    }
}
