using Azure.Storage.Queues;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Application.Processing;
using LabResultsGateway.API.Application.Retry;
using LabResultsGateway.API.Infrastructure.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LabResultsGateway.API.Application.Extensions;

/// <summary>
/// Extension methods for registering poison queue retry services.
/// </summary>
public static class PoisonQueueRetryServiceCollectionExtensions
{
    /// <summary>
    /// Adds poison queue retry services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection with registered services.</returns>
    public static IServiceCollection AddPoisonQueueRetryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration options
        services.Configure<PoisonQueueRetryOptions>(
            configuration.GetSection(PoisonQueueRetryOptions.SectionName));

        // Validate configuration
        ValidateConfiguration(configuration);

        // Register Azure Queue client factory
        services.AddScoped<IAzureQueueClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var queueServiceClient = new QueueServiceClient(config["StorageConnection"]);
            var queueClient = queueServiceClient.GetQueueClient(config["PoisonQueueName"]);
            var logger = sp.GetRequiredService<ILogger<AzureQueueClient>>();
            return new AzureQueueClient(queueClient, logger);
        });

        // Register retry strategy
        services.AddScoped<IRetryStrategy, ExponentialBackoffRetryStrategy>();

        // Register message processor
        services.AddScoped<IPoisonQueueMessageProcessor, PoisonQueueMessageProcessor>();

        // Register orchestrator
        services.AddScoped<IPoisonQueueRetryOrchestrator, PoisonQueueRetryOrchestrator>();

        return services;
    }

    private static void ValidateConfiguration(IConfiguration configuration)
    {
        var storageConnection = configuration["StorageConnection"];
        var poisonQueueName = configuration["PoisonQueueName"];

        if (string.IsNullOrWhiteSpace(storageConnection))
        {
            throw new InvalidOperationException("StorageConnection configuration is required");
        }

        if (string.IsNullOrWhiteSpace(poisonQueueName))
        {
            throw new InvalidOperationException("PoisonQueueName configuration is required");
        }
    }
}