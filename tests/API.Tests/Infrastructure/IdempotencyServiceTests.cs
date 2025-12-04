using Xunit;
using Moq;
using LabResultsGateway.API.Domain.Interfaces;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Infrastructure.Storage;
using LabResultsGateway.API.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Azure;

namespace LabResultsGateway.API.Tests.Infrastructure;

public class IdempotencyServiceTests
{
    [Fact]
    public async Task HasBeenProcessedAsync_ReturnsFalse_WhenNotProcessed()
    {
        // Arrange - Use real Azurite for integration testing
        var mockTableServiceClient = new TableServiceClient("UseDevelopmentStorage=true");
        var options = new IdempotencyOptions { TableName = "testidempotency" + Guid.NewGuid().ToString("N").Substring(0, 8) };
        var mockLogger = new Mock<ILogger<TableStorageIdempotencyService>>();
        var service = new TableStorageIdempotencyService(mockTableServiceClient, Options.Create(options), mockLogger.Object);

        // Act
        var result = await service.HasBeenProcessedAsync("testBlob", new byte[] { 1, 2, 3 });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_CompletesSuccessfully()
    {
        // Arrange
        var mockTableClient = new Mock<TableClient>();
        var mockTableServiceClient = new Mock<TableServiceClient>();
        mockTableServiceClient.Setup(x => x.GetTableClient(It.IsAny<string>())).Returns(mockTableClient.Object);

        var options = new IdempotencyOptions { TableName = "test" };
        var mockLogger = new Mock<ILogger<TableStorageIdempotencyService>>();
        var service = new TableStorageIdempotencyService(mockTableServiceClient.Object, Options.Create(options), mockLogger.Object);

        // Act & Assert - Should not throw
        await service.MarkAsProcessedAsync("testBlob", new byte[] { 1, 2, 3 }, ProcessingOutcome.Success);
    }
}
