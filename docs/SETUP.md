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

## Outbox Pattern Configuration

The outbox pattern ensures reliable message delivery by storing messages transactionally and dispatching them via a background process.

### Configuration Options

Add the following to your `local.settings.json`:

```json
"OutboxOptions": {
  "TableName": "OutboxMessages",
  "MaxRetryCount": 5,
  "InitialRetryDelaySeconds": 30,
  "MaxRetryDelaySeconds": 3600,
  "MessageTimeToLiveHours": 168
}
```

### Configuration Properties

- **TableName**: Azure Table Storage table name for outbox messages (default: "OutboxMessages")
- **MaxRetryCount**: Maximum number of retry attempts for failed deliveries (default: 5)
- **InitialRetryDelaySeconds**: Initial delay between retries in seconds (default: 30)
- **MaxRetryDelaySeconds**: Maximum delay between retries in seconds (default: 3600)
- **MessageTimeToLiveHours**: How long to keep successfully dispatched messages for auditing (default: 168 hours = 7 days)

### Azure Resources Required

1. **Azure Table Storage**: For outbox message persistence
2. **Azure Functions Timer Trigger**: For background message dispatching

### Local Development with Azurite

For local development, ensure Azurite is running and configure the storage connection:

```json
"AzureWebJobsStorage": "UseDevelopmentStorage=true"
```

### Monitoring

Monitor the outbox table size and dispatcher function health:

- Check for growing outbox table (indicates dispatch failures)
- Monitor dispatcher function logs for delivery errors
- Set up alerts for high retry counts

## Domain Events Configuration

Domain events enable loose coupling between components and provide comprehensive observability.

### Event Types

The system publishes the following domain events:

- **LabReportReceivedEvent**: When a lab report blob is received
- **LabMetadataRetrievedEvent**: When lab metadata is successfully extracted
- **Hl7MessageGeneratedEvent**: When an HL7 message is created
- **MessageQueuedEvent**: When a message is queued for processing
- **MessageDeliveryFailedEvent**: When external delivery fails
- **MessageDeliveredEvent**: When a message is successfully delivered

### Event Handlers

Currently implemented handlers:

- **AuditLoggingEventHandler**: Logs all events for compliance and debugging
- **TelemetryEventHandler**: Sends metrics to Application Insights

### Adding New Event Handlers

To add a new event handler:

1. Implement `IDomainEventHandler<TEvent>` interface
2. Register the handler in the DI container:

```csharp
services.AddTransient<IDomainEventHandler<YourEvent>, YourEventHandler>();
```

### Correlation IDs

All events include a correlation ID for tracing requests across the system. Use this ID to correlate logs and telemetry data.

### Testing Events

Events can be tested by publishing them through the `IDomainEventDispatcher`:

```csharp
var @event = new LabReportReceivedEvent(blobName, contentSize, correlationId);
await eventDispatcher.DispatchAsync(@event);
```

### Monitoring Events

Monitor event processing through:

- Application Insights telemetry
- Structured logging with correlation IDs
- Health check endpoints for event processing status
