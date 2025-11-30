using FluentAssertions;
using LabResultsGateway.API.Domain.Constants;
using Xunit;

namespace LabResultsGateway.API.Tests.Domain.Constants;

/// <summary>
/// Unit tests for <see cref="BlobConstants"/>.
/// </summary>
public class BlobConstantsTests
{
    [Fact]
    public void FailedFolderPrefix_HasCorrectValue()
    {
        // Assert
        BlobConstants.FailedFolderPrefix.Should().Be("Failed/");
    }

    [Fact]
    public void FailedFolderPrefix_EndsWithSlash()
    {
        // Assert
        BlobConstants.FailedFolderPrefix.Should().EndWith("/",
            "folder prefix should end with slash for proper path concatenation");
    }

    [Fact]
    public void LabResultsContainer_HasCorrectValue()
    {
        // Assert
        BlobConstants.LabResultsContainer.Should().Be("lab-results-gateway");
    }

    [Fact]
    public void LabResultsContainer_IsLowerCase()
    {
        // Assert
        BlobConstants.LabResultsContainer.Should().Be(BlobConstants.LabResultsContainer.ToLowerInvariant(),
            "Azure blob container names must be lowercase");
    }
}
