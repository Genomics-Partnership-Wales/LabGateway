var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage emulator (Azurite) for local development
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

// Add blob and queue resources for explicit modeling
var blobs = storage.AddBlobs("blobs");
var queues = storage.AddQueues("queues");

// Add Azure Functions project with storage references
// This automatically configures AzureWebJobsStorage via WithHostStorage
// and makes blob/queue connections available via WithReference
var api = builder.AddAzureFunctionsProject<Projects.LabResultsGateway_API>("api")
    .WithHostStorage(storage)
    .WithReference(blobs)
    .WithReference(queues)
    .WaitFor(storage)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/api/health");

builder.Build().Run();
