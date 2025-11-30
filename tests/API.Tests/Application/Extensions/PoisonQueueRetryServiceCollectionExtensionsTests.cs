using FluentAssertions;
using LabResultsGateway.API.Application.Extensions;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Application.Processing;
using LabResultsGateway.API.Application.Retry;
using LabResultsGateway.API.Infrastructure.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LabResultsGateway.API.Tests.Application.Extensions;

public class PoisonQueueRetryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPoisonQueueRetryServices_RegistersAllExpectedServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PoisonQueueRetry:MaxMessagesPerBatch"] = "10",
                ["PoisonQueueRetry:MaxRetryAttempts"] = "3",
                ["PoisonQueueRetry:BaseRetryDelayMinutes"] = "2.0",
                ["PoisonQueueRetry:ProcessingVisibilityTimeoutMinutes"] = "5",
                ["PoisonQueueRetry:UseJitter"] = "true",
                ["PoisonQueueRetry:MaxJitterPercentage"] = "0.3",
                ["StorageConnection"] = "UseDevelopmentStorage=true",
                ["PoisonQueueName"] = "poison-queue"
            })
            .Build();

        // Act
        services.AddPoisonQueueRetryServices(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Verify options are bound correctly
        var options = serviceProvider.GetRequiredService<PoisonQueueRetryOptions>();
        options.MaxMessagesPerBatch.Should().Be(10);
        options.MaxRetryAttempts.Should().Be(3);
        options.BaseRetryDelayMinutes.Should().Be(2.0);
        options.ProcessingVisibilityTimeoutMinutes.Should().Be(5);
        options.UseJitter.Should().BeTrue();
        options.MaxJitterPercentage.Should().Be(0.3);

        // Verify services are registered
        serviceProvider.GetRequiredService<IAzureQueueClient>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IRetryStrategy>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IPoisonQueueMessageProcessor>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IPoisonQueueRetryOrchestrator>().Should().NotBeNull();
    }

    [Fact]
    public void AddPoisonQueueRetryServices_ThrowsInvalidOperationException_WhenStorageConnectionIsMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PoisonQueueRetry:MaxMessagesPerBatch"] = "10",
                // ["StorageConnection"] = "UseDevelopmentStorage=true", // Missing
                ["PoisonQueueName"] = "poison-queue"
            })
            .Build();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddPoisonQueueRetryServices(configuration));

        exception.Message.Should().Contain("StorageConnection");
    }

    [Fact]
    public void AddPoisonQueueRetryServices_ThrowsInvalidOperationException_WhenPoisonQueueNameIsMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PoisonQueueRetry:MaxMessagesPerBatch"] = "10",
                ["StorageConnection"] = "UseDevelopmentStorage=true"
                // ["PoisonQueueName"] = "poison-queue" // Missing
            })
            .Build();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddPoisonQueueRetryServices(configuration));

        exception.Message.Should().Contain("PoisonQueueName");
    }

    [Fact]
    public void AddPoisonQueueRetryServices_RegistersServicesWithCorrectLifetimes()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PoisonQueueRetry:MaxMessagesPerBatch"] = "10",
                ["PoisonQueueRetry:MaxRetryAttempts"] = "3",
                ["StorageConnection"] = "UseDevelopmentStorage=true",
                ["PoisonQueueName"] = "poison-queue"
            })
            .Build();

        // Act
        services.AddPoisonQueueRetryServices(configuration);

        // Assert
        var serviceDescriptors = services.Where(sd =>
            sd.ServiceType == typeof(IAzureQueueClient) ||
            sd.ServiceType == typeof(IRetryStrategy) ||
            sd.ServiceType == typeof(IPoisonQueueMessageProcessor) ||
            sd.ServiceType == typeof(IPoisonQueueRetryOrchestrator)).ToList();

        // All services should be scoped
        serviceDescriptors.Should().AllSatisfy(sd => sd.Lifetime.Should().Be(ServiceLifetime.Scoped));
    }

    [Fact]
    public void AddPoisonQueueRetryServices_BindsOptionsFromConfigurationSection()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PoisonQueueRetry:MaxMessagesPerBatch"] = "25",
                ["PoisonQueueRetry:MaxRetryAttempts"] = "5",
                ["PoisonQueueRetry:BaseRetryDelayMinutes"] = "1.5",
                ["PoisonQueueRetry:ProcessingVisibilityTimeoutMinutes"] = "10",
                ["PoisonQueueRetry:UseJitter"] = "false",
                ["PoisonQueueRetry:MaxJitterPercentage"] = "0.2",
                ["StorageConnection"] = "UseDevelopmentStorage=true",
                ["PoisonQueueName"] = "test-queue"
            })
            .Build();

        // Act
        services.AddPoisonQueueRetryServices(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<PoisonQueueRetryOptions>();

        options.MaxMessagesPerBatch.Should().Be(25);
        options.MaxRetryAttempts.Should().Be(5);
        options.BaseRetryDelayMinutes.Should().Be(1.5);
        options.ProcessingVisibilityTimeoutMinutes.Should().Be(10);
        options.UseJitter.Should().BeFalse();
        options.MaxJitterPercentage.Should().Be(0.2);
    }
}
