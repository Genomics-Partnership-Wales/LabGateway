# .NET Aspire Cheatsheet

> Quick reference for .NET Aspire development with Azure Functions and container runtimes.

**Aspire Version**: 13.0.1  
**Last Updated**: December 2, 2025

---

## 🚨 Critical Gotchas

### 1. Never Use `dotnet watch` with Aspire AppHost

```powershell
# ❌ WRONG - Causes DCP IDE timeout errors
dotnet watch run --project .\src\AppHost\

# ✅ CORRECT - Use plain dotnet run
dotnet run --project .\src\AppHost\
```

**Why?** `dotnet watch` interferes with Aspire's DCP orchestration and debugger integration, causing:
```
Timeout of 120 seconds exceeded waiting for the IDE to start a run session
```

### 2. Container Runtime Must Be Running

Before starting Aspire, ensure your container runtime is healthy:

```powershell
# For Podman
podman machine start
podman machine list  # Verify status is "Running"

# For Docker
# Ensure Docker Desktop is running
docker info
```

**Symptoms of unhealthy container runtime:**
```
Container runtime 'podman' was found but appears to be unhealthy.
Exited with error code -532462766
```

### 3. local.settings.json Not Loaded via Aspire

When running Azure Functions through Aspire, `local.settings.json` is **NOT** automatically loaded. Pass configuration via AppHost:

```csharp
// In AppHost.cs
var api = builder.AddAzureFunctionsProject<Projects.MyApi>("api")
    .WithEnvironment("MyConfigKey", "value")
    .WithEnvironment("ConnectionStrings__MyDb", connectionString);
```

---

## Quick Commands

### Running the Application

| Action | Command |
|--------|---------|
| **Run AppHost** | `dotnet run --project .\src\AppHost\` |
| **Run with specific profile** | `dotnet run --project .\src\AppHost\ --launch-profile https` |
| **Build solution** | `dotnet build` |
| **Clean and rebuild** | `dotnet clean && dotnet build` |

### Debugging

| Action | Command |
|--------|---------|
| **Increase DCP timeout** | `$env:DCP_IDE_REQUEST_TIMEOUT_SECONDS = 300` |
| **Run without debugger** | `dotnet run --project .\src\AppHost\ --no-launch-profile` |
| **View Aspire Dashboard** | Navigate to URL shown in console (e.g., `https://localhost:17169`) |

### Container Runtime (Podman)

| Action | Command |
|--------|---------|
| Start Podman | `podman machine start` |
| Stop Podman | `podman machine stop` |
| Restart Podman | `podman machine stop; podman machine start` |
| Check status | `podman machine list` |
| SSH into machine | `podman machine ssh` |
| List containers | `podman ps -a` |
| View container logs | `podman logs <container-id>` |
| Remove all containers | `podman rm -f $(podman ps -aq)` |

### Container Runtime (Docker)

| Action | Command |
|--------|---------|
| Check Docker status | `docker info` |
| List containers | `docker ps -a` |
| View container logs | `docker logs <container-id>` |
| Remove all containers | `docker rm -f $(docker ps -aq)` |

---

## AppHost Configuration Patterns

### Basic Azure Storage with Emulator

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage emulator (Azurite)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobs = storage.AddBlobs("blobs");
var queues = storage.AddQueues("queues");

builder.Build().Run();
```

### Azure Functions with Storage

```csharp
var api = builder.AddAzureFunctionsProject<Projects.MyApi>("api")
    .WithHostStorage(storage)      // Configures AzureWebJobsStorage
    .WithReference(blobs)          // Makes blob connection available
    .WithReference(queues)         // Makes queue connection available
    .WaitFor(storage)              // Wait for Azurite to be ready
    .WithExternalHttpEndpoints()   // Expose to Dashboard
    .WithHttpHealthCheck("/api/health");
```

### Adding Environment Variables

```csharp
var api = builder.AddAzureFunctionsProject<Projects.MyApi>("api")
    .WithEnvironment("MyApiKey", "dev-key-123")
    .WithEnvironment("FeatureFlags__EnableNewFeature", "true");
```

### Resource Dependencies

```csharp
// Wait for dependencies before starting
var api = builder.AddProject<Projects.MyApi>("api")
    .WaitFor(database)
    .WaitFor(cache)
    .WaitForCompletion(migrationJob);  // Wait for job to complete
```

### Connection String References

```csharp
var db = builder.AddSqlServer("sql")
    .AddDatabase("mydb");

var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(db);  // Adds ConnectionStrings__mydb
```

---

## Common Error Codes

| Exit Code | Hex | Meaning | Solution |
|-----------|-----|---------|----------|
| `-532462766` | `0xE0434352` | Unhandled .NET exception | Check container runtime, configuration |
| Timeout 120s | - | DCP IDE timeout | Use `dotnet run` not `dotnet watch` |

---

## Project Structure

```
src/
├── AppHost/                    # Aspire orchestration
│   ├── AppHost.cs              # Resource definitions
│   └── LabResultsGateway.AppHost.csproj
├── ServiceDefaults/            # Shared configuration
│   ├── Extensions.cs           # AddServiceDefaults()
│   └── LabResultsGateway.ServiceDefaults.csproj
├── API/                        # Azure Functions project
│   ├── Program.cs
│   ├── local.settings.json     # NOT used when running via Aspire!
│   └── LabResultsGateway.API.csproj
└── Client/                     # Other projects...
```

---

## Dashboard Features

Access the Aspire Dashboard at the URL shown in console output:
```
Login to the dashboard at https://localhost:17169/login?t=<token>
```

### Dashboard Sections

| Section | Purpose |
|---------|---------|
| **Resources** | View all running services, containers, databases |
| **Console** | Live console output from each resource |
| **Structured Logs** | Searchable logs with OpenTelemetry integration |
| **Traces** | Distributed tracing across services |
| **Metrics** | Performance metrics and dashboards |

---

## Debugging Tips

### 1. Check Resource Logs in Dashboard
Navigate to **Console** tab for each resource to see startup errors.

### 2. Verify Emulator Connectivity
```powershell
# Test Azurite blob endpoint
curl http://localhost:10000/devstoreaccount1
```

### 3. Check Container Status
```powershell
podman ps -a  # See all containers including stopped ones
podman logs <container-id>  # View container logs
```

### 4. Environment Variable Inspection
In your code, log configuration on startup:
```csharp
var config = builder.Configuration;
Console.WriteLine($"Storage: {config.GetConnectionString("blobs")}");
```

---

## Related Documentation

- [PODMAN_TLS_FIX.md](../../PODMAN_TLS_FIX.md) - Fix Podman certificate errors
- [Official Aspire Docs](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Azure Functions with Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/serverless/functions)

---

## Version History

| Date | Changes |
|------|---------|
| 2025-12-02 | Initial version with gotchas from debugging session |
