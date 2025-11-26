using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using LabResultsGateway.API.Application.Services;
using LabResultsGateway.API.Infrastructure.ExternalServices;
using LabResultsGateway.API.Infrastructure.Hl7;
using LabResultsGateway.API.Infrastructure.Messaging;
using LabResultsGateway.API.Infrastructure.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Resources;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using System.Diagnostics;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Add Azure Key Vault configuration provider (only if KeyVaultUri is configured)
if (!string.IsNullOrEmpty(builder.Configuration["KeyVaultUri"]))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(builder.Configuration["KeyVaultUri"]!),
        new DefaultAzureCredential());
}

// Register ActivitySource singleton for OpenTelemetry tracing
builder.Services.AddSingleton(new ActivitySource("LabResultsGateway"));

// Configure OpenTelemetry with Azure Monitor exporter
builder.Services.AddOpenTelemetry()
    // .ConfigureResource(resource => resource.AddService("LabResultsGateway"))
    .WithTracing(tracerProviderBuilder =>
        tracerProviderBuilder
            .AddSource("LabResultsGateway")
            // .AddHttpClientInstrumentation() // TODO: Add when available
            // .AddAzureMonitorTraceExporter(options => // TODO: Add when available
            // {
            //     options.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            // })
    );

// Register HttpClient for metadata API with resilience policies
builder.Services.AddHttpClient("MetadataApi", (serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["MetadataApiUrl"]!);
    client.DefaultRequestHeaders.Add("X-API-Key", config["MetadataApiKey"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register HttpClient for external endpoint with resilience policies
builder.Services.AddHttpClient("ExternalEndpoint", (serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["ExternalEndpointUrl"]!);
    client.DefaultRequestHeaders.Add("X-API-Key", config["ExternalEndpointApiKey"]!);
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Register Azure Blob Storage client
builder.Services.AddSingleton(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    return new BlobServiceClient(config["StorageConnection"]!);
});

// Register Azure Queue Storage client
builder.Services.AddSingleton(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    return new QueueServiceClient(config["StorageConnection"]!);
});

// Register all application services as scoped
builder.Services.AddScoped<ILabMetadataService, LabMetadataApiClient>();
builder.Services.AddScoped<IHl7MessageBuilder, Hl7MessageBuilder>();

// Register messaging and storage services with factory pattern to resolve configuration
builder.Services.AddScoped<IMessageQueueService>(serviceProvider =>
{
    var queueServiceClient = serviceProvider.GetRequiredService<QueueServiceClient>();
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<AzureQueueService>>();

    var processingQueueName = config["ProcessingQueueName"] ?? "lab-reports-queue";
    var poisonQueueName = config["PoisonQueueName"] ?? "lab-reports-poison";
    var deadLetterQueueName = config["DeadLetterQueueName"] ?? "lab-reports-dead-letter";

    return new AzureQueueService(queueServiceClient, processingQueueName, poisonQueueName, deadLetterQueueName, logger);
});

builder.Services.AddScoped<IBlobStorageService>(serviceProvider =>
{
    var blobServiceClient = serviceProvider.GetRequiredService<BlobServiceClient>();
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<BlobStorageService>>();

    var containerName = config["BlobContainerName"] ?? "lab-results-gateway";

    return new BlobStorageService(blobServiceClient, containerName, logger);
});

builder.Services.AddScoped<ILabReportProcessor, LabReportProcessor>();
builder.Services.AddScoped<IExternalEndpointService, ExternalEndpointService>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
