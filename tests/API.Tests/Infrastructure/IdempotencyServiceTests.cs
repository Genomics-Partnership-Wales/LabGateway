using Xunit;
using Moq;
using LabResultsGateway.API.Domain.Interfaces;
using LabResultsGateway.API.Infrastructure.Storage;
using LabResultsGateway.API.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;

namespace LabResultsGateway.API.Tests.Infrastructure;

public class IdempotencyServiceTests
{
    [Fact]
    public async Task HasBeenProcessedAsync_ReturnsFalse_WhenNotProcessed()
    {
        // Arrange
        var mockTableClient = new Mock<TableServiceClient>();
        var options = new IdempotencyOptions { TableName = "test" };
        var mockLogger = new Mock<ILogger<TableStorageIdempotencyService>>();
        var service = new TableStorageIdempotencyService(mockTableClient.Object, Options.Create(options), mockLogger.Object);

        // Act
        var result = await service.HasBeenProcessedAsync("testBlob", new byte[] { 1, 2, 3 });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_CompletesSuccessfully()
    {
        // Arrange
        var mockTableClient = new Mock<TableServiceClient>();
        var options = new IdempotencyOptions { TableName = "test" };
        var mockLogger = new Mock<ILogger<TableStorageIdempotencyService>>();
        var service = new TableStorageIdempotencyService(mockTableClient.Object, Options.Create(options), mockLogger.Object);

        // Act & Assert - Should not throw
        await service.MarkAsProcessedAsync("testBlob", new byte[] { 1, 2, 3 }, ProcessingOutcome.Success);
    }
}
