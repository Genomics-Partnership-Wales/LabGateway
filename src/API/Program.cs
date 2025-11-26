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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

// Register HttpClient for metadata API with resilience policies
builder.Services.AddHttpClient("MetadataApi", (serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["MetadataApiUrl"]!);
    client.DefaultRequestHeaders.Add("X-API-Key", config["MetadataApiKey"]!);
}).AddStandardResilienceHandler();

// Register HttpClient for external endpoint with resilience policies
builder.Services.AddHttpClient("ExternalEndpoint", (serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["ExternalEndpointUrl"]!);
    client.DefaultRequestHeaders.Add("X-API-Key", config["ExternalEndpointApiKey"]!);
}).AddStandardResilienceHandler();

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
builder.Services.AddScoped<IMessageQueueService, AzureQueueService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<ILabReportProcessor, LabReportProcessor>();
builder.Services.AddScoped<ExternalEndpointService>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
