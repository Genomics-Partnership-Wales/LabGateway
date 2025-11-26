---
goal: Implement DDD-based Lab Results Processing System with HL7 v2.5.1 Integration
version: 1.0
date_created: 2025-11-25
last_updated: 2025-11-25
owner: Development Team
status: 'Implementation Complete - All Phases Finished'
tags: [feature, architecture, ddd, hl7, azure-functions, integration]
---

# Introduction

![Status: In Progress](https://img.shields.io/badge/status-In%20Progress-yellow)

This implementation plan defines the complete architecture and implementation steps for building a Domain-Driven Design (DDD) based lab results processing system. The system processes lab result PDF files uploaded to Azure Blob Storage, extracts metadata, converts data to HL7 v2.5.1 ORU^R01 messages, queues them for processing, and posts to an external NHS Wales endpoint. The implementation follows YAGNI (You Aren't Gonna Need It) and DRY (Don't Repeat Yourself) principles while maintaining clean architecture with Domain, Application, and Infrastructure layers.

**Naming Improvements (2025-11-25)**: Updated Azure Function names for better clarity and maintainability:
- `FileProcessor` → `LabResultBlobProcessor` (blob-triggered function processing lab result uploads)
- `TimeTriggeredProcessor` → `PoisonQueueRetryProcessor` (timer-triggered function retrying failed messages)

## Business Workflow

1. PDF file uploaded to Azure Blob Storage container `lab-results-gateway/`
2. BlobTrigger Azure Function (LabResultBlobProcessor) activates
3. Extract LabNumber from filename
4. Fetch lab metadata from external API using LabNumber
5. Convert lab report data to HL7 v2.5.1 ORU^R01 message (PDF as Base64 in OBX-5)
6. Submit message to Azure Queue (`lab-reports-queue`)
7. Queue-triggered HTTP POST to NHS Wales UAT endpoint
8. Failed messages moved to poison queue (`lab-reports-poison`)
10. Timer-triggered function (PoisonQueueRetryProcessor) retries poison queue messages (max 3 attempts)
10. Structured logging with OpenTelemetry and correlation IDs throughout

## 1. Requirements & Constraints

### Functional Requirements

- **REQ-001**: System SHALL process PDF files uploaded to `lab-results-gateway/` blob container via BlobTrigger Azure Function
- **REQ-002**: System SHALL extract LabNumber from PDF filename and validate format
- **REQ-003**: System SHALL retrieve lab metadata by calling external API: `GET /metadata?labNumber={labNumber}` with `X-API-Key` header
- **REQ-004**: System SHALL convert lab report data to HL7 v2.5.1 ORU^R01 message format
- **REQ-005**: System SHALL embed PDF content as Base64-encoded string in HL7 OBX-5 segment
- **REQ-006**: System SHALL submit HL7 messages to Azure Queue Storage (`lab-reports-queue`)
- **REQ-007**: System SHALL POST HL7 messages from queue to external endpoint: `https://wrrsendoscopyserviceuat.wales.nhs.uk:1065/SubmitHL7Message` with `X-API-Key` header
- **REQ-008**: System SHALL implement poison queue pattern for failed message processing (`lab-reports-poison`)
- **REQ-009**: System SHALL move failed PDF blobs to `Failed/` subfolder within container
- **REQ-010**: TimeTriggeredProcessor SHALL poll poison queue every 5 minutes and retry failed messages (max 3 attempts)
- **REQ-011**: System SHALL track retry count per message and implement exponential backoff

### Non-Functional Requirements

- **NFR-001**: System SHALL use Domain-Driven Design (DDD) architecture with Domain, Application, and Infrastructure layers
- **NFR-002**: System SHALL follow YAGNI and DRY principles - implement only what is needed, avoid duplication
- **NFR-003**: System SHALL implement structured logging with contextual information at all stages
- **NFR-004**: System SHALL use OpenTelemetry ActivitySource for distributed tracing with correlation IDs
- **NFR-005**: System SHALL integrate with Azure Application Insights for telemetry
- **NFR-006**: System SHALL implement resilience patterns using Polly v8 (retry with exponential backoff, circuit breaker)
- **NFR-007**: System SHALL use Azure Key Vault for production API key storage with Key Vault references
- **NFR-008**: System SHALL use IHttpClientFactory with named clients for HTTP operations
- **NFR-009**: System SHALL be built on .NET 10.0 with Azure Functions v4 isolated worker model
- **NFR-010**: System SHALL run locally against Azurite storage emulator during development

### Security Requirements

- **SEC-001**: API keys SHALL be stored in Azure Key Vault for production environments
- **SEC-002**: API keys SHALL use Key Vault reference syntax in application settings: `@Microsoft.KeyVault(SecretUri=https://{vault}.vault.azure.net/secrets/{secret})`
- **SEC-003**: Local development SHALL use local.settings.json with plain text keys (not committed to source control)
- **SEC-004**: All external HTTP calls SHALL use HTTPS protocol
- **SEC-005**: All API authentication SHALL use `X-API-Key` HTTP header

### Technical Constraints

- **CON-001**: System MUST use .NET 10.0 framework
- **CON-002**: System MUST use Azure Functions v4 with isolated worker model
- **CON-003**: System MUST use nHapi library (v3.x) for HL7 v2.x parsing and generation
- **CON-004**: HL7 messages MUST conform to v2.5.1 ORU^R01 message type specification
- **CON-005**: System MUST use Azure Storage SDK v12.x for Blob and Queue operations
- **CON-006**: System MUST NOT include business logic in Azure Functions classes (orchestration only)
- **CON-007**: Domain layer MUST NOT depend on Infrastructure or Application layers
- **CON-008**: Application layer MUST NOT depend on Infrastructure layer (only interfaces)

### Guidelines & Best Practices

- **GUD-001**: Use immutable value objects for domain primitives (LabNumber, LabMetadata)
- **GUD-002**: Use aggregate roots for domain entities (LabReport)
- **GUD-003**: Use custom domain exceptions for business rule violations
- **GUD-004**: Inject dependencies via constructor injection
- **GUD-005**: Use async/await throughout for I/O operations
- **GUD-006**: Create Activity spans with correlation IDs for each operation
- **GUD-007**: Log structured data using strongly-typed parameters
- **GUD-008**: Use Result pattern or exceptions for error handling (prefer exceptions for domain violations)
- **GUD-009**: Keep Azure Functions classes thin - delegate to application services
- **GUD-010**: Use descriptive task identifiers and variable names

### Design Patterns

- **PAT-001**: Repository Pattern for data access abstraction
- **PAT-002**: Service Layer Pattern for application orchestration
- **PAT-003**: Builder Pattern for HL7 message construction
- **PAT-004**: Dependency Injection for loose coupling
- **PAT-005**: Poison Queue Pattern for failed message handling
- **PAT-006**: Retry Pattern with exponential backoff for transient failures
- **PAT-007**: Circuit Breaker Pattern for cascading failure prevention

## 2. Implementation Steps

### Implementation Phase 1: Domain Layer Foundation

**GOAL-001**: Establish core domain models, value objects, and domain exceptions following DDD principles

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `src/API/Domain/` folder structure with subfolders: `Entities/`, `ValueObjects/`, `Exceptions/`, `Interfaces/` | ✅ | 2025-11-25 |
| TASK-002 | Implement `Domain/ValueObjects/LabNumber.cs` immutable value object with validation (non-empty, alphanumeric pattern), equality comparison (IEquatable<LabNumber>), implicit string conversion operator, ToString() override | ✅ | 2025-11-25 |
| TASK-003 | Implement `Domain/ValueObjects/LabMetadata.cs` immutable record with properties: PatientId, FirstName, LastName, DateOfBirth, Gender, TestType, CollectionDate - exact properties TBD based on metadata API JSON schema | ✅ | 2025-11-25 |
| TASK-004 | Implement `Domain/Entities/LabReport.cs` aggregate root with properties: LabNumber (ValueObject), PdfContent (byte[]), Metadata (LabMetadata ValueObject), CreatedAt (DateTimeOffset), CorrelationId (string), constructor validation ensuring all required fields present | ✅ | 2025-11-25 |
| TASK-005 | Implement `Domain/Exceptions/LabNumberInvalidException.cs` custom exception inheriting from ArgumentException with descriptive message and LabNumber value in exception data | ✅ | 2025-11-25 |
| TASK-006 | Implement `Domain/Exceptions/MetadataNotFoundException.cs` custom exception inheriting from InvalidOperationException with LabNumber and descriptive message | ✅ | 2025-11-25 |
| TASK-007 | Implement `Domain/Exceptions/Hl7GenerationException.cs` custom exception inheriting from InvalidOperationException for HL7 message building failures | ✅ | 2025-11-25 |

### Implementation Phase 2: Application Layer Services

**GOAL-002**: Define application service interfaces and DTOs for orchestrating business workflows

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-008 | Create `src/API/Application/` folder structure with subfolders: `Services/`, `DTOs/` | ✅ | 2025-11-25 |
| TASK-009 | Implement `Application/DTOs/LabMetadataDto.cs` record matching JSON response from metadata API - properties TBD based on actual API schema | ✅ | 2025-11-25 |
| TASK-010 | Define `Application/Services/ILabMetadataService.cs` interface with method: `Task<LabMetadata> GetLabMetadataAsync(LabNumber labNumber, CancellationToken cancellationToken = default)` throwing MetadataNotFoundException on 404 | ✅ | 2025-11-25 |
| TASK-011 | Define `Application/Services/IHl7MessageBuilder.cs` interface with method: `string BuildOruR01Message(LabReport labReport)` throwing Hl7GenerationException on failure | ✅ | 2025-11-25 |
| TASK-012 | Define `Application/Services/IMessageQueueService.cs` interface with methods: `Task SendToProcessingQueueAsync(string message, CancellationToken cancellationToken = default)`, `Task SendToPoisonQueueAsync(string message, int retryCount, CancellationToken cancellationToken = default)` | ✅ | 2025-11-25 |
| TASK-013 | Define `Application/Services/IBlobStorageService.cs` interface with method: `Task MoveToFailedFolderAsync(string blobName, CancellationToken cancellationToken = default)` | ✅ | 2025-11-25 |
| TASK-014 | Define `Application/Services/ILabReportProcessor.cs` orchestration interface with method: `Task ProcessLabReportAsync(string blobName, byte[] pdfContent, CancellationToken cancellationToken = default)` coordinating full workflow: extract LabNumber → fetch metadata → build HL7 → queue message | ✅ | 2025-11-25 |
| TASK-015 | Implement `Application/Services/LabReportProcessor.cs` orchestration service with constructor injecting ILabMetadataService, IHl7MessageBuilder, IMessageQueueService, ILogger<LabReportProcessor>, implementing ProcessLabReportAsync with try-catch error handling and structured logging | ✅ | 2025-11-25 |

### Implementation Phase 3: Infrastructure Layer Implementations

**GOAL-003**: Implement concrete infrastructure services for external integrations, HL7 generation, Azure Storage operations

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-016 | Create `src/API/Infrastructure/` folder structure with subfolders: `ExternalServices/`, `Hl7/`, `Messaging/`, `Storage/` | ✅ | 2025-11-26 |
| TASK-017 | Add NuGet package `NHapi.Base` latest v3.x version supporting .NET 10.0 to `LabResultsGateway.API.csproj` | ✅ | 2025-11-26 |
| TASK-018 | Add NuGet package `NHapi.Model.V251` latest version to `LabResultsGateway.API.csproj` for HL7 v2.5.1 support | ✅ | 2025-11-26 |
| TASK-019 | Add NuGet packages for OpenTelemetry: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Exporter.AzureMonitor` latest versions to `LabResultsGateway.API.csproj` | ✅ | 2025-11-26 |
| TASK-020 | Add NuGet package `Polly` v8.x (resilience framework) to `LabResultsGateway.API.csproj` | ✅ | 2025-11-26 |
| TASK-021 | Add NuGet package `Azure.Extensions.AspNetCore.Configuration.Secrets` for Key Vault integration to `LabResultsGateway.API.csproj` | ✅ | 2025-11-26 |
| TASK-022 | Implement `Infrastructure/ExternalServices/LabMetadataApiClient.cs` with constructor injecting IHttpClientFactory (named client "MetadataApi"), IConfiguration for API key/URL, ILogger<LabMetadataApiClient>; implement ILabMetadataService.GetLabMetadataAsync using HttpClient GET /metadata?labNumber={labNumber} with X-API-Key header, deserialize LabMetadataDto, map to LabMetadata ValueObject, handle 404 as MetadataNotFoundException, log request/response with correlation ID | ✅ | 2025-11-26 |
| TASK-023 | Implement `Infrastructure/Hl7/Hl7MessageBuilder.cs` with constructor injecting IConfiguration for MSH segment values, ILogger<Hl7MessageBuilder>; implement IHl7MessageBuilder.BuildOruR01Message using NHapi PipeParser and ORU_R01 message structure, populate MSH segment (MSH-3 from config, MSH-4 from config, MSH-5 from config, MSH-6 from config, MSH-7 timestamp, MSH-9 ORU^R01, MSH-10 unique message ID, MSH-11 from config, MSH-12 "2.5.1"), populate PID segment from LabMetadata (PID-3 PatientId, PID-5 Name, PID-7 DOB, PID-8 Gender), populate OBR segment (OBR-4 TestType, OBR-7 CollectionDate), populate OBX segment with OBX-5 containing Base64-encoded PDF, encode message using PipeParser.Encode(), handle exceptions as Hl7GenerationException, log message generation with correlation ID | ✅ | 2025-11-26 |
| TASK-024 | Implement `Infrastructure/Messaging/AzureQueueService.cs` with constructor injecting QueueServiceClient (via IConfiguration for connection string), IConfiguration for queue names ("ProcessingQueueName", "PoisonQueueName"), ILogger<AzureQueueService>; implement IMessageQueueService.SendToProcessingQueueAsync creating queue if not exists and sending message with Base64 encoding, implement SendToPoisonQueueAsync with retry count metadata, log queue operations with correlation ID | ✅ | 2025-11-26 |
| TASK-025 | Implement `Infrastructure/Storage/BlobStorageService.cs` with constructor injecting BlobServiceClient (via IConfiguration for connection string), IConfiguration for container name, ILogger<BlobStorageService>; implement IBlobStorageService.MoveToFailedFolderAsync copying blob to "Failed/{originalName}" and deleting original, log move operation with correlation ID | ✅ | 2025-11-26 |
| TASK-026 | Implement `Infrastructure/ExternalServices/ExternalEndpointService.cs` with constructor injecting IHttpClientFactory (named client "ExternalEndpoint"), IConfiguration for endpoint URL/API key, ILogger<ExternalEndpointService>; define method `Task<bool> PostHl7MessageAsync(string hl7Message, CancellationToken cancellationToken = default)` using HttpClient POST with X-API-Key header and HL7 message as body content (text/plain), return true on success (2xx), false on failure, log request/response with correlation ID | ✅ | 2025-11-26 |

### Implementation Phase 4: Dependency Injection and OpenTelemetry Configuration

**GOAL-004**: Configure complete DI container, OpenTelemetry tracing, Azure Key Vault integration, and HTTP clients with Polly resilience policies in Program.cs

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-027 | In `Program.cs` after `builder.ConfigureFunctionsWebApplication()`, add Azure Key Vault configuration provider ONLY if environment variable "KeyVaultUri" exists: `if (!string.IsNullOrEmpty(builder.Configuration["KeyVaultUri"])) { builder.Configuration.AddAzureKeyVault(new Uri(builder.Configuration["KeyVaultUri"]!), new DefaultAzureCredential()); }` | ✅ | 2025-11-26 |
| TASK-028 | In `Program.cs` register ActivitySource singleton: `builder.Services.AddSingleton(new ActivitySource("LabResultsGateway"))` | ✅ | 2025-11-26 |
| TASK-029 | In `Program.cs` register HttpClient for metadata API with base address from configuration "MetadataApiUrl" and default X-API-Key header from configuration "MetadataApiKey", add Polly retry policy (3 attempts with exponential backoff: 2s, 4s, 8s) and circuit breaker (break after 5 consecutive failures, 30s duration): `builder.Services.AddHttpClient("MetadataApi", (serviceProvider, client) => { var config = serviceProvider.GetRequiredService<IConfiguration>(); client.BaseAddress = new Uri(config["MetadataApiUrl"]!); client.DefaultRequestHeaders.Add("X-API-Key", config["MetadataApiKey"]!); }).AddStandardResilienceHandler()` | ✅ | 2025-11-26 |
| TASK-030 | In `Program.cs` register HttpClient for external endpoint with base address from configuration "ExternalEndpointUrl" and default X-API-Key header from configuration "ExternalEndpointApiKey", add Polly retry policy (3 attempts with exponential backoff: 2s, 4s, 8s) and circuit breaker (break after 5 consecutive failures, 30s duration): `builder.Services.AddHttpClient("ExternalEndpoint", (serviceProvider, client) => { var config = serviceProvider.GetRequiredService<IConfiguration>(); client.BaseAddress = new Uri(config["ExternalEndpointUrl"]!); client.DefaultRequestHeaders.Add("X-API-Key", config["ExternalEndpointApiKey"]!); }).AddStandardResilienceHandler()` | ✅ | 2025-11-26 |
| TASK-031 | In `Program.cs` register Azure Blob Storage client: `builder.Services.AddSingleton(serviceProvider => { var config = serviceProvider.GetRequiredService<IConfiguration>(); return new BlobServiceClient(config["StorageConnection"]!); })` | ✅ | 2025-11-26 |
| TASK-032 | In `Program.cs` register Azure Queue Storage client: `builder.Services.AddSingleton(serviceProvider => { var config = serviceProvider.GetRequiredService<IConfiguration>(); return new QueueServiceClient(config["StorageConnection"]!); })` | ✅ | 2025-11-26 |
| TASK-033 | In `Program.cs` register all application services as scoped: `builder.Services.AddScoped<ILabMetadataService, LabMetadataApiClient>(); builder.Services.AddScoped<IHl7MessageBuilder, Hl7MessageBuilder>(); builder.Services.AddScoped<IMessageQueueService, AzureQueueService>(); builder.Services.AddScoped<IBlobStorageService, BlobStorageService>(); builder.Services.AddScoped<ILabReportProcessor, LabReportProcessor>(); builder.Services.AddScoped<ExternalEndpointService>();` | ✅ | 2025-11-26 |

### Implementation Phase 5: FileProcessor Azure Function Refactoring

**GOAL-005**: Refactor FileProcessor.cs BlobTrigger function to orchestrate complete lab report processing workflow with OpenTelemetry tracing

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-034 | In `FileProcessor.cs` add constructor parameters: `ILabReportProcessor labReportProcessor`, `IBlobStorageService blobStorageService`, `ActivitySource activitySource`, update field assignments | ✅ | 2025-11-26 |
| TASK-035 | In `FileProcessor.cs` Run method, wrap entire logic in Activity span: `using var activity = _activitySource.StartActivity("ProcessLabReport", ActivityKind.Consumer);` | ✅ | 2025-11-26 |
| TASK-036 | In `FileProcessor.cs` Run method, generate correlation ID: `var correlationId = activity?.Id ?? Guid.NewGuid().ToString(); activity?.SetTag("correlation.id", correlationId); activity?.SetTag("blob.name", name);` | ✅ | 2025-11-26 |
| TASK-037 | In `FileProcessor.cs` Run method, add try-catch block around processing logic | ✅ | 2025-11-26 |
| TASK-038 | In try block, read PDF content: `using var memoryStream = new MemoryStream(); await stream.CopyToAsync(memoryStream); var pdfBytes = memoryStream.ToArray();` | ✅ | 2025-11-26 |
| TASK-039 | In try block, log start: `_logger.LogInformation("Starting lab report processing. BlobName: {BlobName}, CorrelationId: {CorrelationId}, Size: {Size} bytes", name, correlationId, pdfBytes.Length);` | ✅ | 2025-11-26 |
| TASK-040 | In try block, call processor: `await labReportProcessor.ProcessLabReportAsync(name, pdfBytes, cancellationToken);` (add CancellationToken parameter to Run method) | ✅ | 2025-11-26 |
| TASK-041 | In try block, log success: `_logger.LogInformation("Lab report processed successfully. BlobName: {BlobName}, CorrelationId: {CorrelationId}", name, correlationId); activity?.SetStatus(ActivityStatusCode.Ok);` | ✅ | 2025-11-26 |
| TASK-042 | In catch block for LabNumberInvalidException, log error and move blob to Failed folder: `_logger.LogError(ex, "Invalid lab number in blob name. BlobName: {BlobName}, CorrelationId: {CorrelationId}", name, correlationId); await blobStorageService.MoveToFailedFolderAsync(name); activity?.SetStatus(ActivityStatusCode.Error, ex.Message);` | ✅ | 2025-11-26 |
| TASK-043 | In catch block for MetadataNotFoundException, log error and move blob to Failed folder: `_logger.LogError(ex, "Metadata not found for lab number. BlobName: {BlobName}, CorrelationId: {CorrelationId}", name, correlationId); await blobStorageService.MoveToFailedFolderAsync(name); activity?.SetStatus(ActivityStatusCode.Error, ex.Message);` | ✅ | 2025-11-26 |
| TASK-044 | In catch block for Hl7GenerationException, log error and move blob to Failed folder: `_logger.LogError(ex, "HL7 message generation failed. BlobName: {BlobName}, CorrelationId: {CorrelationId}", name, correlationId); await blobStorageService.MoveToFailedFolderAsync(name); activity?.SetStatus(ActivityStatusCode.Error, ex.Message);` | ✅ | 2025-11-26 |
| TASK-045 | In catch block for Exception (general), log error, move blob to Failed folder, send to poison queue with retry count 0: `_logger.LogError(ex, "Unexpected error processing lab report. BlobName: {BlobName}, CorrelationId: {CorrelationId}", name, correlationId); await blobStorageService.MoveToFailedFolderAsync(name); // Send to poison queue logic TBD based on queue message format; activity?.SetStatus(ActivityStatusCode.Error, ex.Message);` | ✅ | 2025-11-26 |

### Implementation Phase 6: TimeTriggeredProcessor Poison Queue Retry Logic

**GOAL-006**: Refactor TimeTriggeredProcessor.cs to poll poison queue, retry failed messages with exponential backoff, track retry count (max 3), implement dead letter handling

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-046 | In `TimeTriggeredProcessor.cs` add constructor parameters: `IMessageQueueService messageQueueService`, `ExternalEndpointService externalEndpointService`, `IConfiguration configuration`, `ActivitySource activitySource`, update field assignments | ✅ | 2025-11-26 |
| TASK-047 | In `TimeTriggeredProcessor.cs` Run method, wrap logic in Activity span: `using var activity = _activitySource.StartActivity("RetryPoisonQueue", ActivityKind.Consumer);` | ✅ | 2025-11-26 |
| TASK-048 | In `TimeTriggeredProcessor.cs` Run method, log execution start: `_logger.LogInformation("Poison queue retry processor starting at: {ExecutionTime}", DateTime.UtcNow);` | ✅ | 2025-11-26 |
| TASK-049 | In Run method, create QueueClient for poison queue: `var queueServiceClient = new QueueServiceClient(configuration["StorageConnection"]!); var queueClient = queueServiceClient.GetQueueClient(configuration["PoisonQueueName"]!); await queueClient.CreateIfNotExistsAsync();` | ✅ | 2025-11-26 |
| TASK-050 | In Run method, peek messages from poison queue (batch of 10): `var messages = await queueClient.ReceiveMessagesAsync(maxMessages: 10, visibilityTimeout: TimeSpan.FromMinutes(5));` | ✅ | 2025-11-26 |
| TASK-051 | In Run method, log batch info: `_logger.LogInformation("Retrieved {MessageCount} messages from poison queue", messages.Value?.Length ?? 0); activity?.SetTag("message.count", messages.Value?.Length ?? 0);` | ✅ | 2025-11-26 |
| TASK-052 | In Run method, iterate through messages with foreach loop, generate correlation ID per message: `var correlationId = Guid.NewGuid().ToString();` | ✅ | 2025-11-26 |
| TASK-053 | Inside foreach loop, deserialize message - format TBD based on queue message format decision (plain HL7 string vs JSON wrapper with metadata), extract HL7 message string and current retry count | ✅ | 2025-11-26 |
| TASK-054 | Inside foreach loop, create child Activity for retry attempt: `using var retryActivity = _activitySource.StartActivity("RetryMessage", ActivityKind.Consumer, activity.Context); retryActivity?.SetTag("correlation.id", correlationId); retryActivity?.SetTag("retry.count", retryCount);` | ✅ | 2025-11-26 |
| TASK-055 | Inside foreach loop, check if retry count exceeds max (3): if exceeded, log and implement dead letter handling based on dead letter strategy decision (permanent storage vs log/delete): `if (retryCount >= 3) { _logger.LogWarning("Message exceeded max retries. CorrelationId: {CorrelationId}, RetryCount: {RetryCount}", correlationId, retryCount); // Dead letter handling TBD; await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt); continue; }` | ✅ | 2025-11-26 |
| TASK-056 | Inside foreach loop, if retry count < 3, attempt to post HL7 message: `var success = await externalEndpointService.PostHl7MessageAsync(hl7Message, cancellationToken);` | ✅ | 2025-11-26 |
| TASK-057 | Inside foreach loop, if POST successful (success == true), delete message from poison queue and log success: `_logger.LogInformation("Message retry successful. CorrelationId: {CorrelationId}, RetryCount: {RetryCount}", correlationId, retryCount); await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt); retryActivity?.SetStatus(ActivityStatusCode.Ok);` | ✅ | 2025-11-26 |
| TASK-058 | Inside foreach loop, if POST failed (success == false), increment retry count, update message visibility timeout with exponential backoff (2^retryCount minutes), send back to poison queue with updated retry count: `retryCount++; _logger.LogWarning("Message retry failed. CorrelationId: {CorrelationId}, RetryCount: {RetryCount}, NextRetryIn: {NextRetry} minutes", correlationId, retryCount, Math.Pow(2, retryCount)); var visibilityTimeout = TimeSpan.FromMinutes(Math.Pow(2, retryCount)); await queueClient.UpdateMessageAsync(message.MessageId, message.PopReceipt, visibilityTimeout: visibilityTimeout); retryActivity?.SetStatus(ActivityStatusCode.Error, "Retry failed");` | ✅ | 2025-11-26 |
| TASK-059 | At end of Run method, log completion: `_logger.LogInformation("Poison queue retry processor completed at: {ExecutionTime}", DateTime.UtcNow); activity?.SetStatus(ActivityStatusCode.Ok);` | ✅ | 2025-11-26 |

### Implementation Phase 7: Configuration and Settings

**GOAL-007**: Update local.settings.json with all required configuration values, document Key Vault setup for production

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-060 | Update `local.settings.json` add "KeyVaultUri": "" (empty for local dev, production will use actual Key Vault URI) | ✅ | 2025-11-26 |
| TASK-061 | Update `local.settings.json` add "MetadataApiUrl": "https://[TBD-metadata-api-url]" (actual URL TBD) | ✅ | 2025-11-26 |
| TASK-062 | Update `local.settings.json` add "MetadataApiKey": "[LOCAL-DEV-API-KEY]" (actual key TBD) | ✅ | 2025-11-26 |
| TASK-063 | Update `local.settings.json` add "ExternalEndpointUrl": "https://wrrsendoscopyserviceuat.wales.nhs.uk:1065/SubmitHL7Message" | ✅ | 2025-11-26 |
| TASK-064 | Update `local.settings.json` add "ExternalEndpointApiKey": "[LOCAL-DEV-API-KEY]" (actual key TBD) | ✅ | 2025-11-26 |
| TASK-065 | Update `local.settings.json` add "ProcessingQueueName": "lab-reports-queue" | ✅ | 2025-11-26 |
| TASK-066 | Update `local.settings.json` add "PoisonQueueName": "lab-reports-poison" | ✅ | 2025-11-26 |
| TASK-067 | Update `local.settings.json` add "MSH_SendingApplication": "[TBD]" (MSH-3 value TBD) | ✅ | 2025-11-26 |
| TASK-068 | Update `local.settings.json` add "MSH_SendingFacility": "[TBD]" (MSH-4 value TBD) | ✅ | 2025-11-26 |
| TASK-069 | Update `local.settings.json` add "MSH_ReceivingApplication": "[TBD-possibly-WRRS]" (MSH-5 value TBD) | ✅ | 2025-11-26 |
| TASK-070 | Update `local.settings.json` add "MSH_ReceivingFacility": "[TBD]" (MSH-6 value TBD) | ✅ | 2025-11-26 |
| TASK-071 | Update `local.settings.json` add "MSH_ProcessingId": "T" (T for test/UAT environment, P for production) | ✅ | 2025-11-26 |
| TASK-072 | Create `docs/KEYVAULT-SETUP.md` documentation file with instructions for production Key Vault setup: create Azure Key Vault, add secrets (MetadataApiKey, ExternalEndpointApiKey), configure Azure Function App managed identity, grant "Key Vault Secrets User" role, update application settings with Key Vault references format: `@Microsoft.KeyVault(SecretUri=https://{vault}.vault.azure.net/secrets/{secret})` | ✅ | 2025-11-26 |

## 3. Alternatives

- **ALT-001**: **Plain Azure Functions without DDD** - Could implement logic directly in Azure Functions classes without domain/application layers. REJECTED because: violates separation of concerns, makes testing difficult, couples business logic to infrastructure, not scalable for complex business rules, contradicts stated requirement for DDD architecture.

- **ALT-002**: **Durable Functions for workflow orchestration** - Could use Durable Functions for orchestrating the multi-step workflow (fetch metadata → build HL7 → queue → POST). REJECTED because: adds unnecessary complexity for linear workflow, YAGNI principle - current workflow doesn't need durable state management or complex orchestration patterns, simple service orchestration is sufficient, increases Azure costs.

- **ALT-003**: **Azure Service Bus instead of Azure Queue Storage** - Could use Azure Service Bus for message queuing with advanced features (dead-lettering, sessions, duplicate detection). REJECTED because: YAGNI - current requirements don't need Service Bus features, Azure Queue Storage is simpler and cheaper, poison queue pattern with retry logic is sufficient for error handling, existing project already has Azure Storage SDK.

- **ALT-004**: **Custom HL7 serialization instead of nHapi** - Could manually build HL7 v2.5.1 pipe-delimited strings without library. REJECTED because: error-prone manual string manipulation, doesn't handle HL7 escape sequences correctly, difficult to maintain segment structure, nHapi is industry-standard library with proper v2.5.1 support, violates DRY principle (reinventing wheel).

- **ALT-005**: **Separate Azure Function for queue processing** - Could create dedicated QueueTrigger Azure Function instead of repurposing TimeTriggeredProcessor. REJECTED because: QueueTrigger would process messages immediately which prevents exponential backoff retry strategy, poison queue pattern requires delayed retry with increasing intervals, TimeTriggeredProcessor allows controlled polling with visibility timeout management, requirement explicitly states repurpose TimeTriggeredProcessor.

- **ALT-006**: **Store HL7 messages in Cosmos DB or Table Storage** - Could persist HL7 messages to database before/after sending. REJECTED because: YAGNI - no requirement for message persistence beyond queue, adds unnecessary complexity and storage costs, queue messages provide sufficient transient storage, audit logging via Application Insights is sufficient for traceability.

- **ALT-007**: **Synchronous HTTP POST without queue** - Could POST HL7 message directly to external endpoint from FileProcessor without queue intermediary. REJECTED because: violates fault tolerance requirement, no retry mechanism for transient failures, blocks BlobTrigger execution during HTTP call, doesn't support poison queue pattern, queue decouples processing stages allowing independent scaling and resilience.

## 4. Dependencies

- **DEP-001**: **NHapi.Base v3.x** - HL7 v2.x parsing and generation library, required for building HL7 v2.5.1 ORU^R01 messages, must support .NET 10.0
- **DEP-002**: **NHapi.Model.V251** - HL7 v2.5.1 message model definitions, required for ORU^R01 message structure
- **DEP-003**: **OpenTelemetry.Extensions.Hosting** - OpenTelemetry SDK for .NET hosting integration, required for distributed tracing
- **DEP-004**: **OpenTelemetry.Instrumentation.Http** - OpenTelemetry HTTP client instrumentation, required for automatic HTTP span creation
- **DEP-005**: **OpenTelemetry.Exporter.AzureMonitor** - Azure Monitor exporter for OpenTelemetry, required for sending traces to Application Insights
- **DEP-006**: **Polly v8** - Resilience and transient fault handling library, required for retry policies and circuit breakers
- **DEP-007**: **Azure.Extensions.AspNetCore.Configuration.Secrets** - Azure Key Vault configuration provider, required for production secret management
- **DEP-008**: **Azure.Storage.Blobs v12.26.0** - Already installed, required for blob storage operations
- **DEP-009**: **Azure.Storage.Queues v12.24.0** - Already installed, required for queue storage operations
- **DEP-010**: **Microsoft.Azure.Functions.Worker v2.51.0** - Already installed, Azure Functions isolated worker runtime
- **DEP-011**: **Microsoft.ApplicationInsights.WorkerService v2.23.0** - Already installed, Application Insights telemetry
- **DEP-012**: **Metadata API availability** - External metadata API must be accessible and return JSON response with lab metadata, endpoint URL and schema TBD
- **DEP-013**: **NHS Wales endpoint availability** - External endpoint https://wrrsendoscopyserviceuat.wales.nhs.uk:1065/SubmitHL7Message must be accessible for HL7 message submission
- **DEP-014**: **API Key credentials** - Valid API keys must be provided for both metadata API and NHS Wales endpoint
- **DEP-015**: **Azure Key Vault for production** - Azure Key Vault instance must be provisioned and configured with managed identity access for production deployment

## 5. Files

### Files to Create

- **FILE-001**: `src/API/Domain/ValueObjects/LabNumber.cs` - Immutable value object representing validated lab number with pattern matching and equality
- **FILE-002**: `src/API/Domain/ValueObjects/LabMetadata.cs` - Immutable record containing patient demographics and test information from metadata API
- **FILE-003**: `src/API/Domain/Entities/LabReport.cs` - Aggregate root combining LabNumber, PDF content, metadata, and correlation ID
- **FILE-004**: `src/API/Domain/Exceptions/LabNumberInvalidException.cs` - Custom exception for invalid lab number format
- **FILE-005**: `src/API/Domain/Exceptions/MetadataNotFoundException.cs` - Custom exception when metadata API returns 404
- **FILE-006**: `src/API/Domain/Exceptions/Hl7GenerationException.cs` - Custom exception for HL7 message building failures
- **FILE-007**: `src/API/Application/DTOs/LabMetadataDto.cs` - DTO for deserializing metadata API JSON response
- **FILE-008**: `src/API/Application/Services/ILabMetadataService.cs` - Interface for fetching lab metadata from external API
- **FILE-009**: `src/API/Application/Services/IHl7MessageBuilder.cs` - Interface for building HL7 v2.5.1 ORU^R01 messages
- **FILE-010**: `src/API/Application/Services/IMessageQueueService.cs` - Interface for Azure Queue Storage operations
- **FILE-011**: `src/API/Application/Services/IBlobStorageService.cs` - Interface for blob storage operations
- **FILE-012**: `src/API/Application/Services/ILabReportProcessor.cs` - Interface for orchestrating lab report processing workflow
- **FILE-013**: `src/API/Application/Services/LabReportProcessor.cs` - Concrete orchestration service implementation
- **FILE-014**: `src/API/Infrastructure/ExternalServices/LabMetadataApiClient.cs` - HTTP client for metadata API with X-API-Key authentication
- **FILE-015**: `src/API/Infrastructure/Hl7/Hl7MessageBuilder.cs` - nHapi-based HL7 message builder for ORU^R01 messages
- **FILE-016**: `src/API/Infrastructure/Messaging/AzureQueueService.cs` - Azure Queue Storage service implementation
- **FILE-017**: `src/API/Infrastructure/Storage/BlobStorageService.cs` - Azure Blob Storage service implementation for moving failed blobs
- **FILE-018**: `src/API/Infrastructure/ExternalServices/ExternalEndpointService.cs` - HTTP client for NHS Wales endpoint with retry logic
- **FILE-019**: `docs/KEYVAULT-SETUP.md` - Documentation for Azure Key Vault production setup and configuration

### Files to Modify

- **FILE-020**: `src/API/Program.cs` - Add OpenTelemetry configuration, DI registrations, HTTP client factory, Polly policies, Key Vault integration (EXISTING: basic Application Insights setup)
- **FILE-021**: `src/API/FileProcessor.cs` - Refactor to orchestrate workflow with error handling and OpenTelemetry tracing (EXISTING: basic BlobTrigger logging)
- **FILE-022**: `src/API/TimeTriggeredProcessor.cs` - Refactor to implement poison queue retry logic with exponential backoff (EXISTING: simple timer logging)
- **FILE-023**: `src/API/local.settings.json` - Add all configuration values for APIs, queues, MSH segments, Key Vault URI (EXISTING: storage connection strings)
- **FILE-024**: `src/API/LabResultsGateway.API.csproj` - Add NuGet package references for nHapi, OpenTelemetry, Polly, Key Vault (EXISTING: Azure Storage and Functions packages)

## 6. Testing

### Unit Tests

- **TEST-001**: **LabNumber value object validation** - Test LabNumber constructor with valid formats (alphanumeric, with hyphens), invalid formats (null, empty, special chars), verify equality comparison and ToString()
- **TEST-002**: **LabMetadata value object immutability** - Test LabMetadata record creation, verify properties are readonly, test equality comparison
- **TEST-003**: **LabReport aggregate validation** - Test LabReport constructor with valid data, test with missing required fields (throws exceptions), verify CreatedAt is UTC
- **TEST-004**: **LabReportProcessor orchestration** - Mock ILabMetadataService, IHl7MessageBuilder, IMessageQueueService, test ProcessLabReportAsync success path, test with MetadataNotFoundException (verify not queued), test with Hl7GenerationException (verify not queued)
- **TEST-005**: **Hl7MessageBuilder message structure** - Test BuildOruR01Message with sample LabReport, verify MSH segment values (sending/receiving app/facility, message type ORU^R01, version 2.5.1), verify PID segment populated from metadata, verify OBR segment with test type, verify OBX segment contains Base64-encoded PDF, verify message parses correctly with nHapi PipeParser
- **TEST-006**: **AzureQueueService message sending** - Mock QueueClient, test SendToProcessingQueueAsync encodes message to Base64, test SendToPoisonQueueAsync includes retry count metadata, verify queue name configuration used
- **TEST-007**: **BlobStorageService move operation** - Mock BlobContainerClient, test MoveToFailedFolderAsync copies to "Failed/" prefix and deletes original, verify error handling if source blob doesn't exist
- **TEST-008**: **LabMetadataApiClient HTTP integration** - Mock HttpMessageHandler, test GetLabMetadataAsync sends GET with correct URL query parameter and X-API-Key header, verify JSON deserialization to LabMetadata, test 404 response throws MetadataNotFoundException, test 500 response throws exception

### Integration Tests

- **TEST-009**: **FileProcessor end-to-end with Azurite** - Upload test PDF to Azurite blob storage, verify FileProcessor triggers, mock external metadata API and queue service, verify blob processed or moved to Failed folder, verify structured logging output
- **TEST-010**: **Full workflow with in-memory services** - Create test lab report, run through LabReportProcessor with in-memory implementations of all services, verify HL7 message generated correctly, verify message queued, verify no exceptions thrown
- **TEST-011**: **TimeTriggeredProcessor retry logic** - Seed Azurite poison queue with test messages (varying retry counts), run TimeTriggeredProcessor, mock ExternalEndpointService (return success/failure), verify messages deleted on success, verify retry count incremented on failure, verify dead letter handling after 3 retries
- **TEST-012**: **OpenTelemetry tracing** - Execute FileProcessor with ActivityListener, verify Activity created with name "ProcessLabReport", verify correlation ID tag present, verify child Activities for metadata fetch and HL7 generation, verify Activity status set correctly on success/failure

### Manual Testing Checklist

- **TEST-013**: **Local Azurite end-to-end** - Start Azurite emulator, run Azure Functions locally, upload test PDF to lab-results-gateway container (filename with valid LabNumber), verify metadata API called (use mock API or stub), verify HL7 message generated in queue, verify message POSTed to external endpoint (use RequestBin for testing), verify structured logs in console, verify Application Insights telemetry
- **TEST-014**: **Error scenarios** - Test with invalid filename (no LabNumber), verify blob moved to Failed/, test with metadata API returning 404, verify blob moved to Failed/, test with external endpoint returning 500, verify message in poison queue
- **TEST-015**: **Poison queue retry** - Manually add message to poison queue, wait for TimeTriggeredProcessor (5 min), verify retry attempt logged, verify retry count incremented, test with 3 failed attempts, verify dead letter handling
- **TEST-016**: **Key Vault integration** - Deploy to Azure with Key Vault configured, verify API keys loaded from Key Vault via managed identity, verify application functions correctly with Key Vault references
- **TEST-017**: **Performance and load** - Upload batch of 100 PDFs, verify all processed successfully, check Application Insights for performance metrics, verify no throttling or errors

## 7. Risks & Assumptions

### Risks

- **RISK-001**: **Metadata API schema unknown** - Metadata API JSON response structure not provided, cannot implement LabMetadataDto or mapping to HL7 segments until schema confirmed. MITIGATION: Request sample JSON response from API provider, create placeholder DTO structure that can be updated, use flexible JSON deserialization with JsonProperty attributes.

- **RISK-002**: **MSH segment values undefined** - HL7 MSH segment requires specific values for sending/receiving application and facility (MSH-3, MSH-4, MSH-5, MSH-6, MSH-11) which are not yet provided. MITIGATION: Use configuration placeholders "[TBD]", request values from NHS Wales integration team, ensure values are configurable not hardcoded.

- **RISK-003**: **Queue message format undecided** - Choice between plain HL7 string vs JSON wrapper affects serialization, retry logic, and poison queue handling. MITIGATION: Recommend JSON wrapper with metadata (HL7 message, LabNumber, correlation ID, retry count, timestamp) for better traceability and retry management, document decision in ADR.

- **RISK-004**: **Dead letter strategy undefined** - After 3 failed retries from poison queue, unclear whether to persist to permanent storage or just log and delete. MITIGATION: Recommend persisting to "dead-letter-queue" blob container for manual investigation, implement configurable strategy, document in operations runbook.

- **RISK-005**: **External endpoint reliability** - NHS Wales UAT endpoint may have downtime, rate limits, or unexpected errors affecting message delivery. MITIGATION: Implement Polly circuit breaker (30s break after 5 failures), comprehensive error logging with correlation IDs, poison queue retry with exponential backoff, monitor Application Insights for endpoint health.

- **RISK-006**: **Large PDF file sizes** - Very large PDF files (>10MB) could cause memory issues, slow processing, or exceed Azure Functions timeout (default 5 minutes). MITIGATION: Implement file size validation in FileProcessor, log file sizes, consider blob streaming for very large files, increase function timeout if needed, set max file size limit in documentation.

- **RISK-007**: **nHapi compatibility with .NET 10** - nHapi library may not have stable release for .NET 10 (preview) causing runtime issues. MITIGATION: Test nHapi thoroughly in .NET 10 environment during implementation, have fallback to manual HL7 string building if library incompatible, consider targeting .NET 9 LTS if .NET 10 causes issues.

- **RISK-008**: **Poison queue message growth** - If external endpoint has prolonged outage, poison queue could accumulate thousands of messages. MITIGATION: Implement queue depth monitoring with Application Insights metrics, set alerts for queue depth >100 messages, document operational procedure for bulk reprocessing, consider queue TTL (7 days) for old messages.

- **RISK-009**: **Correlation ID propagation** - Correlation IDs may not propagate correctly through all layers causing difficulty in distributed tracing. MITIGATION: Use OpenTelemetry Activity.Current throughout, log correlation ID at every stage, include correlation ID in all exceptions, test end-to-end tracing with multiple concurrent requests.

### Assumptions

- **ASSUMPTION-001**: **Metadata API availability** - Assume metadata API is available during development and testing, returns consistent JSON structure, has reasonable SLA for production use.

- **ASSUMPTION-002**: **LabNumber in filename** - Assume PDF blob filenames always contain valid LabNumber that can be extracted (format not yet specified), assume filename pattern is consistent and documented.

- **ASSUMPTION-003**: **Single PDF per LabNumber** - Assume one PDF per lab report, no scenarios with multiple PDFs for same LabNumber requiring aggregation or multiple HL7 messages.

- **ASSUMPTION-004**: **HL7 message size limits** - Assume Base64-encoded PDF within OBX-5 segment doesn't exceed HL7 message size limits or Azure Queue message size (64KB for Azure Queue Storage), may need queue upgrade to Service Bus if messages exceed limits.

- **ASSUMPTION-005**: **External endpoint accepts HL7 text** - Assume NHS Wales endpoint accepts raw HL7 pipe-delimited text as POST body with Content-Type: text/plain, no additional envelope or transformation required.

- **ASSUMPTION-006**: **No duplicate processing required** - Assume blob trigger at-least-once semantics is acceptable, duplicate processing of same PDF is idempotent on receiver side or unlikely due to blob name uniqueness.

- **ASSUMPTION-007**: **UTC timestamps throughout** - Assume all timestamps (MSH-7, OBR collection date, CreatedAt) use UTC timezone, no timezone conversion required for UK/Wales time.

- **ASSUMPTION-008**: **Development environment connectivity** - Assume developers have network access to metadata API and NHS Wales UAT endpoint from development machines (not blocked by firewall), Azurite emulator sufficient for local storage testing.

- **ASSUMPTION-009**: **Managed Identity for production** - Assume Azure Functions will be deployed with system-assigned managed identity for Key Vault access, no service principal or connection string authentication needed.

- **ASSUMPTION-010**: **Retry logic sufficient** - Assume 3 retry attempts with exponential backoff (2min, 4min, 8min) is sufficient for most transient failures, permanent failures acceptable to move to dead letter after 3 attempts.

## 8. Related Specifications / Further Reading

- [OWASP Top 10 Security](https://owasp.org/www-project-top-ten/) - Security best practices referenced in SEC requirements
- [HL7 v2.5.1 Standard](http://www.hl7.eu/refactored/index.html) - Official HL7 v2.5.1 specification for message structure
- [nHapi Documentation](https://github.com/nHapiNET/nHapi) - HL7 library documentation and examples
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/) - OpenTelemetry SDK and instrumentation guide
- [Polly Documentation](https://www.pollydocs.org/) - Resilience and retry patterns library
- [Azure Functions Best Practices](https://learn.microsoft.com/en-us/azure/azure-functions/functions-best-practices) - Microsoft guidance for Azure Functions development
- [Domain-Driven Design Reference](https://www.domainlanguage.com/ddd/reference/) - Eric Evans DDD patterns and terminology
- [Azure Key Vault Configuration Provider](https://learn.microsoft.com/en-us/azure/key-vault/general/tutorial-net-create-vault-azure-web-app) - Guide for Key Vault integration with .NET apps
- [Azure Storage Queue Best Practices](https://learn.microsoft.com/en-us/azure/storage/queues/storage-queues-introduction) - Queue storage patterns and limitations

---

## Open Questions Requiring Clarification Before Implementation

1. **Metadata API JSON Schema** - What exact JSON structure does `GET /metadata?labNumber=X` return? Need property names for: patient ID, first name, last name, date of birth, gender, test type, collection date for mapping to PID/OBR/OBX segments.

2. **HL7 MSH Segment Values** - What specific values for MSH-3 (Sending Application name), MSH-4 (Sending Facility ID/name), MSH-5 (Receiving Application - 'WRRS'?), MSH-6 (Receiving Facility), and MSH-11 (Processing ID: P=production, T=test)?

3. **Queue Message Format** - Should the Azure Queue contain the complete HL7 message as plain string, or a JSON wrapper with metadata (HL7 message, LabNumber, correlation ID, retry count, timestamp) for better retry tracking?

4. **Dead Letter Strategy** - After 3 failed retries from poison queue, should messages move to a permanent dead-letter queue/blob storage for manual intervention, or just log error and delete?

5. **LabNumber Filename Pattern** - What exact pattern/format is the LabNumber in the PDF filename? (e.g., `{LabNumber}.pdf`, `LAB-{LabNumber}-{date}.pdf`, prefix/suffix rules)

6. **PDF File Size Limits** - What is maximum expected PDF file size? Need to validate against Azure Queue message size limit (64KB) or Azure Service Bus if larger messages required.
