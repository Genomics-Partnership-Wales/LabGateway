using System.Diagnostics;
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
    private AzuriteContainer _azuriteContainer;
    private IServiceProvider _serviceProvider;
    private PoisonQueueRetryOptions _options;

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
            // Create QueueServiceClient pointing to Azurite
            var queueServiceClient = new QueueServiceClient(
                new Uri($"http://127.0.0.1:10001/devstoreaccount1"),
                new AzureNamedKeyCredential("devstoreaccount1",
                    "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="));

            var queueClient = queueServiceClient.GetQueueClient("poison-queue");
            return new AzureQueueClient(queueClient, logger);
        });

        services.AddScoped<IRetryStrategy, ExponentialBackoffRetryStrategy>();
        services.AddScoped<IPoisonQueueMessageProcessor, PoisonQueueMessageProcessor>();
        services.AddScoped<IPoisonQueueRetryOrchestrator, PoisonQueueRetryOrchestrator>();

        // Mock external dependencies
        services.AddScoped<IMessageQueueService>(sp =>
        {
            var mock = new Mock<IMessageQueueService>();
            mock.Setup(x => x.SendToDeadLetterQueueAsync(It.IsAny<DeadLetterMessage>()))
                .Returns(Task.CompletedTask);
            return mock.Object;
        });

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

    [Fact]
    public async Task EndToEndFlow_SeedsPoisonQueueWithTestMessage_VerifyMessageProcessedAndDeleted()
    {
        // Arrange
        var queueClient = _serviceProvider.GetRequiredService<IAzureQueueClient>();
        var orchestrator = _serviceProvider.GetRequiredService<IPoisonQueueRetryOrchestrator>();

        // Seed poison queue with test message
        var testMessage = new QueueMessageDto
        {
            CorrelationId = $"test-{Guid.NewGuid()}",
            RetryCount = 0,
            MessageId = Guid.NewGuid().ToString(),
            Hl7Message = "MSH|^~\\&|LAB|FACILITY|APP|DEST|202411291200||ORU^R01|123|P|2.5|||AL|NE|||||"
        };

        var messageJson = System.Text.Json.JsonSerializer.Serialize(testMessage);
        var base64Message = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(messageJson));

        // Send message to queue using the queue client
        await queueClient.EnsureQueueExistsAsync();

        // Get the underlying QueueClient for direct message sending
        var queueServiceClient = new QueueServiceClient(
            new Uri($"http://127.0.0.1:10001/devstoreaccount1"),
            new AzureNamedKeyCredential("devstoreaccount1",
                "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="));

        var queueClientDirect = queueServiceClient.GetQueueClient("poison-queue");
        await queueClientDirect.SendMessageAsync(base64Message);

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        // Verify message was processed (first attempt fails, second succeeds)
        // The message should be deleted after successful processing
        var messages = await queueClient.ReceiveMessagesAsync(10, TimeSpan.FromMinutes(1));
        messages.Should().BeEmpty("Message should be deleted after successful processing");
    }

    [Fact]
    public async Task EndToEndFlow_MessageExceedsMaxRetries_SentToDeadLetterQueue()
    {
        // Arrange
        var queueClient = _serviceProvider.GetRequiredService<IAzureQueueClient>();
        var messageQueueService = _serviceProvider.GetRequiredService<IMessageQueueService>();
        var orchestrator = _serviceProvider.GetRequiredService<IPoisonQueueRetryOrchestrator>();

        // Seed poison queue with test message that exceeds max retries
        var testMessage = new QueueMessageDto
        {
            CorrelationId = $"test-{Guid.NewGuid()}",
            RetryCount = _options.MaxRetryAttempts, // Already at max retries
            MessageId = Guid.NewGuid().ToString(),
            Hl7Message = "MSH|^~\\&|LAB|FACILITY|APP|DEST|202411291200||ORU^R01|123|P|2.5|||AL|NE|||||"
        };

        var messageJson = System.Text.Json.JsonSerializer.Serialize(testMessage);
        var base64Message = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(messageJson));

        await queueClient.EnsureQueueExistsAsync();

        var queueServiceClient = new QueueServiceClient(
            new Uri($"http://127.0.0.1:10001/devstoreaccount1"),
            new AzureNamedKeyCredential("devstoreaccount1",
                "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="));

        var queueClientDirect = queueServiceClient.GetQueueClient("poison-queue");
        await queueClientDirect.SendMessageAsync(base64Message);

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        // Verify message was sent to dead letter queue
        messageQueueService.Verify(
            x => x.SendToDeadLetterQueueAsync(It.Is<DeadLetterMessage>(
                dlm => dlm.CorrelationId == testMessage.CorrelationId &&
                       dlm.MessageId == testMessage.MessageId &&
                       dlm.RetryCount == testMessage.RetryCount)),
            Times.Once);

        // Verify message was deleted from poison queue
        var messages = await queueClient.ReceiveMessagesAsync(10, TimeSpan.FromMinutes(1));
        messages.Should().BeEmpty("Message should be deleted after being sent to dead letter queue");
    }

    [Fact]
    public async Task EndToEndFlow_MultipleMessagesProcessedConcurrently()
    {
        // Arrange
        var queueClient = _serviceProvider.GetRequiredService<IAzureQueueClient>();
        var orchestrator = _serviceProvider.GetRequiredService<IPoisonQueueRetryOrchestrator>();

        await queueClient.EnsureQueueExistsAsync();

        var queueServiceClient = new QueueServiceClient(
            new Uri($"http://127.0.0.1:10001/devstoreaccount1"),
            new AzureNamedKeyCredential("devstoreaccount1",
                "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="));

        var queueClientDirect = queueServiceClient.GetQueueClient("poison-queue");

        // Seed multiple messages
        for (int i = 0; i < 3; i++)
        {
            var testMessage = new QueueMessageDto
            {
                CorrelationId = $"test-{Guid.NewGuid()}",
                RetryCount = 0,
                MessageId = Guid.NewGuid().ToString(),
                Hl7Message = $"MSH|^~\\&|LAB|FACILITY|APP|DEST|202411291200||ORU^R01|{i}|P|2.5|||AL|NE|||||"
            };

            var messageJson = System.Text.Json.JsonSerializer.Serialize(testMessage);
            var base64Message = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(messageJson));
            await queueClientDirect.SendMessageAsync(base64Message);
        }

        // Act
        await orchestrator.ProcessPoisonQueueAsync(CancellationToken.None);

        // Assert
        // All messages should be processed and deleted
        var messages = await queueClient.ReceiveMessagesAsync(10, TimeSpan.FromMinutes(1));
        messages.Should().BeEmpty("All messages should be processed and deleted");
    }
}
