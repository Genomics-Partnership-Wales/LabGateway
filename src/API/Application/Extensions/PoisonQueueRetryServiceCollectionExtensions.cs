using Azure.Storage.Queues;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Application.Processing;
using LabResultsGateway.API.Application.Retry;
using LabResultsGateway.API.Infrastructure.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
            // Use the same connection string resolution as in Program.cs
            var connectionString = config.GetConnectionString("queues")
                                ?? config["AzureWebJobsStorage"]
                                ?? "UseDevelopmentStorage=true";
            var queueServiceClient = new QueueServiceClient(connectionString);
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
        // Use the same connection string resolution as in Program.cs
        var connectionString = configuration.GetConnectionString("queues")
                            ?? configuration["AzureWebJobsStorage"]
                            ?? "UseDevelopmentStorage=true";
        var poisonQueueName = configuration["PoisonQueueName"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Storage connection configuration is required (queues connection string, AzureWebJobsStorage, or UseDevelopmentStorage=true)");
        }

        if (string.IsNullOrWhiteSpace(poisonQueueName))
        {
            throw new InvalidOperationException("PoisonQueueName configuration is required");
        }
    }
}
