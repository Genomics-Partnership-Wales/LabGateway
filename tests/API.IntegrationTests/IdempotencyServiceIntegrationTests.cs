using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Azure.Data.Tables;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Domain.Interfaces;
using LabResultsGateway.API.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.Azurite;
using Xunit;

namespace LabResultsGateway.API.IntegrationTests;

public class IdempotencyServiceIntegrationTests : IAsyncLifetime
{
    private AzuriteContainer? _azuriteContainer;
    private IServiceProvider? _serviceProvider;

    public async Task InitializeAsync()
    {
        // Start Azurite container
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithPortBinding(10001, 10001) // Blob
            .WithPortBinding(10002, 10002) // Queue
            .WithPortBinding(10000, 10000) // Table
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10001))
            .Build();

        await _azuriteContainer.StartAsync();

        // Setup configuration
        var options = new IdempotencyOptions
        {
            TableName = "idempotency-test",
            TTLHours = 24
        };

        // Setup DI container
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton<ActivitySource>(new ActivitySource("Test"));
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        // Register idempotency service
        services.AddScoped<IIdempotencyService, TableStorageIdempotencyService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TableStorageIdempotencyService>>();
            var opts = sp.GetRequiredService<IdempotencyOptions>();
            // Create TableServiceClient pointing to Azurite using connection string
            var connectionString = "UseDevelopmentStorage=true";
            var tableServiceClient = new TableServiceClient(connectionString);
            return new TableStorageIdempotencyService(tableServiceClient, Options.Create(opts), logger);
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_azuriteContainer != null)
        {
            await _azuriteContainer.DisposeAsync();
        }
    }

    [Fact(Skip = "Requires Docker/Azurite container")]
    public async Task HasBeenProcessedAsync_ReturnsFalse_WhenNotProcessed()
    {
        // Arrange
        var serviceProvider = _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized");
        var service = serviceProvider.GetRequiredService<IIdempotencyService>();

        // Act
        var result = await service.HasBeenProcessedAsync("testBlob", new byte[] { 1, 2, 3 });

        // Assert
        result.Should().BeFalse();
    }

    [Fact(Skip = "Requires Docker/Azurite container")]
    public async Task MarkAsProcessedAsync_ThenHasBeenProcessedAsync_ReturnsTrue()
    {
        // Arrange
        var serviceProvider = _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized");
        var service = serviceProvider.GetRequiredService<IIdempotencyService>();
        var blobName = "testBlob";
        var contentHash = new byte[] { 1, 2, 3 };

        // Act
        await service.MarkAsProcessedAsync(blobName, contentHash, ProcessingOutcome.Success);
        var result = await service.HasBeenProcessedAsync(blobName, contentHash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(Skip = "Requires Docker/Azurite container")]
    public async Task HasBeenProcessedAsync_ReturnsFalse_AfterTTLExpires()
    {
        // Arrange
        var serviceProvider = _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized");
        var service = serviceProvider.GetRequiredService<IIdempotencyService>();
        var blobName = "testBlob";
        var contentHash = new byte[] { 1, 2, 3 };

        // Set a very short TTL for testing
        var options = new IdempotencyOptions { TableName = "idempotency-test", TTLHours = 0.001 }; // ~3.6 seconds
        var shortTTlService = new TableStorageIdempotencyService(
            new TableServiceClient("UseDevelopmentStorage=true"),
            Options.Create(options),
            serviceProvider.GetRequiredService<ILogger<TableStorageIdempotencyService>>());

        // Act
        await shortTTlService.MarkAsProcessedAsync(blobName, contentHash, ProcessingOutcome.Success);

        // Wait for TTL to expire
        await Task.Delay(TimeSpan.FromSeconds(5));

        var result = await shortTTlService.HasBeenProcessedAsync(blobName, contentHash);

        // Assert
        result.Should().BeFalse();
    }
}
