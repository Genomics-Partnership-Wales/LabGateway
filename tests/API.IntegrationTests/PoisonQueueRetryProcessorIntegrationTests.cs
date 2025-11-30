using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Azure.Storage.Queues;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using LabResultsGateway.API.Application.DTOs;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Application.Processing;
using LabResultsGateway.API.Application.Retry;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Domain.Entities;
using LabResultsGateway.API.Infrastructure.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.Azurite;
using Xunit;

namespace LabResultsGateway.API.IntegrationTests;

public class PoisonQueueRetryProcessorIntegrationTests : IAsyncLifetime
{
    private AzuriteContainer? _azuriteContainer;
    private IServiceProvider? _serviceProvider;
    private PoisonQueueRetryOptions? _options;
    private Mock<IMessageQueueService>? _messageQueueServiceMock;

    /// <summary>
    /// Ensures a nullable field was properly initialized during test setup.
    /// Throws InvalidOperationException with a meaningful message if the field is null.
    /// </summary>
    /// <typeparam name="T">The type of the field (must be a reference type).</typeparam>
    /// <param name="field">The nullable field to check.</param>
    /// <param name="fieldName">Auto-captured field name via CallerArgumentExpression.</param>
    /// <returns>The non-null field value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when field is null.</exception>
    private static T EnsureInitialized<T>(
        [NotNull] T? field,
        [CallerArgumentExpression(nameof(field))] string? fieldName = null) where T : class
    {
        return field ?? throw new InvalidOperationException(
            $"Test setup failed: {fieldName} was not initialized. Ensure InitializeAsync() completed successfully.");
    }

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
        _options = new PoisonQueueRetryOptions
        {
            MaxMessagesPerBatch = 5,
            MaxRetryAttempts = 2,
            BaseRetryDelayMinutes = 1.0,
            ProcessingVisibilityTimeoutMinutes = 2,
            UseJitter = false,
            MaxJitterPercentage = 0.0
        };

        // Initialize mocks
        _messageQueueServiceMock = new Mock<IMessageQueueService>();
        _messageQueueServiceMock.Setup(x => x.SendToDeadLetterQueueAsync(It.IsAny<DeadLetterMessage>()))
            .Returns(Task.CompletedTask);

        // Setup DI container
        var services = new ServiceCollection();
        services.AddSingleton(_options);
        services.AddSingleton<ActivitySource>(new ActivitySource("Test"));
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        // Register services
        services.AddScoped<IAzureQueueClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AzureQueueClient>>();
            // Create QueueServiceClient pointing to Azurite using connection string
            var connectionString = "UseDevelopmentStorage=true";
            var queueServiceClient = new QueueServiceClient(connectionString);
            var queueClient = queueServiceClient.GetQueueClient("poison-queue");
            return new AzureQueueClient(queueClient, logger);
        });

        services.AddScoped<IRetryStrategy, ExponentialBackoffRetryStrategy>();
        services.AddScoped<IPoisonQueueMessageProcessor, PoisonQueueMessageProcessor>();
        services.AddScoped<IPoisonQueueRetryOrchestrator, PoisonQueueRetryOrchestrator>();

        // Mock external dependencies
        services.AddScoped<IMessageQueueService>(_ => _messageQueueServiceMock.Object);

        services.AddScoped<IExternalEndpointService>(sp =>
        {
            var mock = new Mock<IExternalEndpointService>();
            // First call fails, second succeeds (simulating retry success)
            var callCount = 0;
            mock.Setup(x => x.PostHl7MessageAsync(It.IsAny<string>()))
                .ReturnsAsync(() => ++callCount > 1);
            return mock.Object;
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
    public async Task EndToEndFlow_SeedsPoisonQueueWithTestMessage_VerifyMessageProcessedAndDeleted()
    {
        // Arrange
        var serviceProvider = EnsureInitialized(_serviceProvider);
        var queueClient = serviceProvider.GetRequiredService<IAzureQueueClient>();
        var orchestrator = serviceProvider.GetRequiredService<IPoisonQueueRetryOrchestrator>();

        // Seed poison queue with test message
        var testMessage = new QueueMessage(
            "MSH|^~\\&|LAB|FACILITY|APP|DEST|202411291200||ORU^R01|123|P|2.5|||AL|NE|||||",
            $"test-{Guid.NewGuid()}",
            0,
            DateTimeOffset.UtcNow,
            "test-blob"
        );

        var messageJson = System.Text.Json.JsonSerializer.Serialize(testMessage);

        // Send message to queue using the queue client
        await queueClient.EnsureQueueExistsAsync();

        // Get the underlying QueueClient for direct message sending
        var connectionString = "UseDevelopmentStorage=true";
        var queueServiceClient = new QueueServiceClient(connectionString);
        var queueClientDirect = queueServiceClient.GetQueueClient("poison-queue");
        await queueClientDirect.SendMessageAsync(messageJson);

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        // Verify message was processed (first attempt fails, second succeeds)
        // The message should be deleted after successful processing
        var messages = await queueClient.ReceiveMessagesAsync(10, TimeSpan.FromMinutes(1));
        messages.Should().BeEmpty("Message should be deleted after successful processing");
    }

    [Fact(Skip = "Requires Docker/Azurite container")]
    public async Task EndToEndFlow_MessageExceedsMaxRetries_SentToDeadLetterQueue()
    {
        // Arrange
        var serviceProvider = EnsureInitialized(_serviceProvider);
        var options = EnsureInitialized(_options);
        var messageQueueServiceMock = EnsureInitialized(_messageQueueServiceMock);

        var queueClient = serviceProvider.GetRequiredService<IAzureQueueClient>();
        var messageQueueService = serviceProvider.GetRequiredService<IMessageQueueService>();
        var orchestrator = serviceProvider.GetRequiredService<IPoisonQueueRetryOrchestrator>();

        // Seed poison queue with test message that exceeds max retries
        var testMessage = new QueueMessage(
            "MSH|^~\\&|LAB|FACILITY|APP|DEST|202411291200||ORU^R01|123|P|2.5|||AL|NE|||||",
            $"test-{Guid.NewGuid()}",
            options.MaxRetryAttempts, // Already at max retries
            DateTimeOffset.UtcNow,
            "test-blob"
        );

        var messageJson = System.Text.Json.JsonSerializer.Serialize(testMessage);

        await queueClient.EnsureQueueExistsAsync();

        var connectionString = "UseDevelopmentStorage=true";
        var queueServiceClient = new QueueServiceClient(connectionString);
        var queueClientDirect = queueServiceClient.GetQueueClient("poison-queue");
        await queueClientDirect.SendMessageAsync(messageJson);

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        // Verify message was sent to dead letter queue
        messageQueueServiceMock.Verify(
            x => x.SendToDeadLetterQueueAsync(It.Is<DeadLetterMessage>(
                dlm => dlm.CorrelationId == testMessage.CorrelationId &&
                       dlm.RetryCount == testMessage.RetryCount)),
            Times.Once);

        // Verify message was deleted from poison queue
        var messages = await queueClient.ReceiveMessagesAsync(10, TimeSpan.FromMinutes(1));
        messages.Should().BeEmpty("Message should be deleted after being sent to dead letter queue");
    }

    [Fact(Skip = "Requires Docker/Azurite container")]
    public async Task EndToEndFlow_MultipleMessagesProcessedConcurrently()
    {
        // Arrange
        var serviceProvider = EnsureInitialized(_serviceProvider);
        var queueClient = serviceProvider.GetRequiredService<IAzureQueueClient>();
        var orchestrator = serviceProvider.GetRequiredService<IPoisonQueueRetryOrchestrator>();

        await queueClient.EnsureQueueExistsAsync();

        var connectionString = "UseDevelopmentStorage=true";
        var queueServiceClient = new QueueServiceClient(connectionString);
        var queueClientDirect = queueServiceClient.GetQueueClient("poison-queue");

        // Seed multiple messages
        for (int i = 0; i < 3; i++)
        {
            var testMessage = new QueueMessage(
                $"MSH|^~\\&|LAB|FACILITY|APP|DEST|202411291200||ORU^R01|{i}|P|2.5|||AL|NE|||||",
                $"test-{Guid.NewGuid()}",
                0,
                DateTimeOffset.UtcNow,
                $"test-blob-{i}"
            );

            var messageJson = System.Text.Json.JsonSerializer.Serialize(testMessage);
            await queueClientDirect.SendMessageAsync(messageJson);
        }

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        // All messages should be processed and deleted
        var messages = await queueClient.ReceiveMessagesAsync(10, TimeSpan.FromMinutes(1));
        messages.Should().BeEmpty("All messages should be processed and deleted");
    }
}
