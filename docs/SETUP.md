## Blazor WASM Project Creation

The LabResultsGateway project was created using the following command:

```bash
dotnet new blazorwasm -n LabResultsGateway.Client -au IndividualB2C -o src/Client -p -e -f net9.0
```

The Azure Functions API was created using the command:

```bash
func init src/API --worker-runtime dotnet-isolated --target-framework net9.0 --name LabResultsGateway.API
```

## Health Checks

The application provides several health check endpoints for monitoring:

- `/api/health/live` - Liveness check (basic ping to verify the service is running)
- `/api/health/ready` - Readiness check (verifies the service can accept traffic)
- `/api/health/check` - Comprehensive health check (validates all dependent components)

Configure the health check options in `local.settings.json`:

```json
"HealthCheck": {
  "TimeoutSeconds": 30,
  "BlobStorageConnection": "AzureWebJobsStorage",
  "QueueStorageConnection": "AzureWebJobsStorage",
  "MetadataApiUrl": "http://localhost:7071/api"
}
```
