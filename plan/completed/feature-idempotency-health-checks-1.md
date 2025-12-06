---
goal: Implement Idempotency Service and Health Check Endpoints for Lab Results Gateway
version: 1.0
date_created: 2025-11-27
last_updated: 2025-12-04
owner: Development Team
status: 'Completed'
priority: 'High'
tags: [feature, reliability, resilience, idempotency, health-checks, azure-functions]
---

# Introduction

![Status: Pending](https://img.shields.io/badge/status-Pending-yellow)
![Priority: High](https://img.shields.io/badge/priority-High-red)

This implementation plan defines the architecture and implementation steps for adding two high-priority reliability features to the Lab Results Gateway:

1. **Idempotency Service**: Prevents duplicate processing of lab result blobs by tracking processed blob identifiers using Azure Table Storage. This ensures at-most-once processing semantics even when blob triggers fire multiple times for the same file.

2. **Health Check Endpoints**: Provides HTTP endpoints to monitor the health of all dependencies including Azure Blob Storage, Queue Storage, External Metadata API, and NHS Wales endpoint. Enables integration with Azure Monitor, load balancers, and operational dashboards.

## Business Value

- **Idempotency**: Prevents duplicate HL7 messages being sent to NHS Wales, avoiding data quality issues and duplicate patient records
- **Health Checks**: Enables proactive monitoring and faster incident response, reducing mean time to detection (MTTD) for system failures

## 1. Requirements & Constraints

### Functional Requirements - Idempotency

- **REQ-ID-001**: System SHALL track processed blob names in Azure Table Storage with timestamp metadata
- **REQ-ID-002**: System SHALL check idempotency store before processing any blob in LabResultBlobProcessor
- **REQ-ID-003**: System SHALL skip processing and log warning if blob was already processed within 24 hours
- **REQ-ID-004**: System SHALL allow reprocessing of blobs older than 24 hours (configurable TTL)
- **REQ-ID-005**: System SHALL generate unique idempotency keys from blob name and content hash
- **REQ-ID-006**: System SHALL record processing outcome (success/failure) in idempotency store

### Functional Requirements - Health Checks

- **REQ-HC-001**: System SHALL expose HTTP GET endpoint `/api/health` returning aggregated health status
- **REQ-HC-002**: System SHALL expose HTTP GET endpoint `/api/health/ready` for readiness probe (all dependencies)
- **REQ-HC-003**: System SHALL expose HTTP GET endpoint `/api/health/live` for liveness probe (basic function health)
- **REQ-HC-004**: System SHALL check Azure Blob Storage connectivity and permissions
- **REQ-HC-005**: System SHALL check Azure Queue Storage connectivity for all queues (processing, poison, dead-letter)
- **REQ-HC-006**: System SHALL check External Metadata API availability (HTTP HEAD or lightweight GET)
- **REQ-HC-007**: System SHALL return JSON response with individual component health and overall status
- **REQ-HC-008**: System SHALL return HTTP 200 for healthy, HTTP 503 for unhealthy status

### Non-Functional Requirements

- **NFR-001**: Idempotency check latency SHALL be < 100ms for Table Storage lookup
- **NFR-002**: Health check endpoints SHALL respond within 5 seconds
- **NFR-003**: Health check timeouts for individual dependencies SHALL be configurable
- **NFR-004**: System SHALL cache health check results for 30 seconds to prevent overload
- **NFR-005**: System SHALL use structured logging for all idempotency and health check operations
- **NFR-006**: System SHALL record telemetry metrics for idempotency hits/misses and health check results

### Technical Constraints

- **CON-001**: Must use Azure Table Storage for idempotency (cost-effective, low-latency)
- **CON-002**: Must not break existing Azure Functions triggers or bindings
- **CON-003**: Health checks must be anonymous (no authentication required for monitoring tools)
- **CON-004**: Must integrate with existing OpenTelemetry ActivitySource for tracing

## 2. Implementation Steps

### Phase 1: Idempotency Service Infrastructure

**GOAL-001**: Create the idempotency service with Azure Table Storage backend

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `src/API/Domain/Interfaces/IIdempotencyService.cs` interface with methods: `Task<bool> HasBeenProcessedAsync(string blobName, byte[] contentHash)`, `Task MarkAsProcessedAsync(string blobName, byte[] contentHash, ProcessingOutcome outcome)` | ✅ | 2025-12-04 |
| TASK-002 | Create `src/API/Domain/ValueObjects/IdempotencyKey.cs` value object combining blob name and content hash with validation | ✅ | 2025-12-04 |
| TASK-003 | Create `src/API/Domain/Entities/ProcessingRecord.cs` entity representing a processed blob with properties: IdempotencyKey, BlobName, ContentHash, ProcessedAt, Outcome, CorrelationId | ✅ | 2025-12-04 |
| TASK-004 | Create `src/API/Application/Options/IdempotencyOptions.cs` configuration class with properties: TableName, TTLHours, StorageConnection | ✅ | 2025-12-04 |
| TASK-005 | Create `src/API/Infrastructure/Storage/TableStorageIdempotencyService.cs` implementing IIdempotencyService with Azure Table Storage operations | ✅ | 2025-12-04 |
| TASK-006 | Add Azure.Data.Tables NuGet package reference to `LabResultsGateway.API.csproj` | ✅ | 2025-12-04 |
| TASK-007 | Register TableServiceClient and IIdempotencyService in `Program.cs` with configuration binding | ✅ | 2025-12-04 |
| TASK-008 | Add idempotency configuration to `local.settings.json`: IdempotencyTableName, IdempotencyTTLHours | ✅ | 2025-12-04 |

### Phase 2: Integrate Idempotency into Blob Processor

**GOAL-002**: Add idempotency checks to LabResultBlobProcessor

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-009 | Inject IIdempotencyService into LabResultBlobProcessor constructor | ✅ | 2025-12-04 |
| TASK-010 | Compute SHA256 content hash from PDF bytes in LabResultBlobProcessor.Run | ✅ | 2025-12-04 |
| TASK-011 | Check idempotency before processing: if already processed, log and return early | ✅ | 2025-12-04 |
| TASK-012 | Mark blob as processed after successful processing with ProcessingOutcome.Success | ✅ | 2025-12-04 |
| TASK-013 | Mark blob with ProcessingOutcome.Failed on exception before moving to Failed folder | ✅ | 2025-12-04 |
| TASK-014 | Add telemetry metrics for idempotency hits (skipped) vs misses (processed) | ✅ | 2025-12-04 |

### Phase 3: Health Check Infrastructure

**GOAL-003**: Create health check service and individual dependency checks

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | Create `src/API/Application/Services/IHealthCheckService.cs` interface with methods: `Task<HealthCheckResult> CheckAllAsync()`, `Task<HealthCheckResult> CheckLivenessAsync()`, `Task<HealthCheckResult> CheckReadinessAsync()` | ⬜ | |
| TASK-016 | Create `src/API/Application/DTOs/HealthCheckResult.cs` record with properties: Status (Healthy/Unhealthy/Degraded), Components (Dictionary<string, ComponentHealth>), Timestamp, Duration | ⬜ | |
| TASK-017 | Create `src/API/Application/DTOs/ComponentHealth.cs` record with properties: Name, Status, Description, Duration, Exception | ⬜ | |
| TASK-018 | Create `src/API/Application/Options/HealthCheckOptions.cs` with timeout configurations per dependency | ⬜ | |
| TASK-019 | Create `src/API/Infrastructure/HealthChecks/BlobStorageHealthCheck.cs` checking container exists and accessible | ⬜ | |
| TASK-020 | Create `src/API/Infrastructure/HealthChecks/QueueStorageHealthCheck.cs` checking all queues exist and accessible | ⬜ | |
| TASK-021 | Create `src/API/Infrastructure/HealthChecks/MetadataApiHealthCheck.cs` with lightweight connectivity test | ⬜ | |
| TASK-022 | Create `src/API/Application/Services/HealthCheckService.cs` orchestrating all individual checks with caching | ⬜ | |
| TASK-023 | Register IHealthCheckService and all health check classes in `Program.cs` | ⬜ | |
| TASK-024 | Add health check configuration to `local.settings.json`: HealthCheckTimeoutSeconds, HealthCheckCacheSeconds | ⬜ | |

### Phase 4: Health Check Azure Functions

**GOAL-004**: Create HTTP-triggered Azure Functions for health endpoints

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-025 | Create `src/API/Functions/HealthCheckFunction.cs` with three HTTP triggers: `/api/health`, `/api/health/ready`, `/api/health/live` | ⬜ | |
| TASK-026 | Implement `/api/health` endpoint returning full health check result as JSON | ⬜ | |
| TASK-027 | Implement `/api/health/ready` endpoint for Kubernetes readiness probe compatibility | ⬜ | |
| TASK-028 | Implement `/api/health/live` endpoint for Kubernetes liveness probe compatibility | ⬜ | |
| TASK-029 | Configure anonymous authorization level for health check functions | ⬜ | |
| TASK-030 | Add OpenTelemetry Activity spans for health check operations | ⬜ | |

### Phase 5: Testing and Documentation

**GOAL-005**: Create comprehensive tests and documentation

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-031 | Create `tests/API.Tests/Infrastructure/IdempotencyServiceTests.cs` with unit tests for Table Storage operations | ⬜ | |
| TASK-032 | Create `tests/API.Tests/Application/HealthCheckServiceTests.cs` with unit tests mocking dependencies | ⬜ | |
| TASK-033 | Create `tests/API.IntegrationTests/HealthCheckIntegrationTests.cs` testing HTTP endpoints with WebApplicationFactory | ⬜ | |
| TASK-034 | Create `tests/API.IntegrationTests/IdempotencyIntegrationTests.cs` testing with Azurite Table Storage | ⬜ | |
| TASK-035 | Update `docs/SETUP.md` with health check endpoint documentation and monitoring configuration | ⬜ | |
| TASK-036 | Create `docs/architecture/ADR-003-idempotency-pattern.md` documenting design decisions | ⬜ | |

## 3. Alternatives

### Idempotency Alternatives

- **ALT-001**: **Redis for idempotency store** - Could use Azure Cache for Redis instead of Table Storage. REJECTED because: higher cost for simple key-value lookups, Table Storage provides sufficient performance (<100ms), adds additional Azure service dependency, overkill for current scale requirements.

- **ALT-002**: **Cosmos DB for idempotency** - Could use Cosmos DB with TTL for automatic expiration. REJECTED because: significantly higher cost than Table Storage, global distribution not needed, Table Storage supports sufficient throughput and has built-in TTL policies.

- **ALT-003**: **In-memory idempotency cache** - Could use in-memory dictionary with periodic persistence. REJECTED because: lost on function cold start, doesn't work across multiple function instances, not durable, violates idempotency guarantee.

- **ALT-004**: **Blob metadata for idempotency** - Could store processing status in blob metadata. REJECTED because: metadata can be lost if blob moved/deleted, doesn't provide centralized view, harder to query processing history, doesn't work for Failed folder blobs.

### Health Check Alternatives

- **ALT-005**: **Azure Functions built-in health monitoring** - Could rely on Azure Monitor and Application Insights only. REJECTED because: no custom dependency health visibility, no readiness/liveness probe support for Kubernetes/containers, no real-time status endpoint for load balancers.

- **ALT-006**: **Third-party health check library (AspNetCore.Diagnostics.HealthChecks)** - Could use community library. REJECTED because: designed for ASP.NET Core not Azure Functions, adds unnecessary dependency, custom implementation is straightforward for our needs.

## 4. Dependencies

### New NuGet Packages

- **DEP-001**: `Azure.Data.Tables v12.x` - Azure Table Storage SDK for idempotency store
- **DEP-002**: `System.Security.Cryptography.Algorithms` (built-in) - SHA256 for content hashing

### Existing Dependencies Used

- **DEP-003**: `Azure.Storage.Blobs v12.x` - Already installed, used for blob health checks
- **DEP-004**: `Azure.Storage.Queues v12.x` - Already installed, used for queue health checks
- **DEP-005**: `Microsoft.Azure.Functions.Worker v2.x` - Already installed, HTTP trigger support

### External Dependencies

- **DEP-006**: Azure Table Storage account (same storage account as blob/queue)
- **DEP-007**: External Metadata API for health check (existing dependency)

## 5. Files

### Files to Create

| File | Description |
|------|-------------|
| `src/API/Domain/Interfaces/IIdempotencyService.cs` | Interface for idempotency operations |
| `src/API/Domain/ValueObjects/IdempotencyKey.cs` | Value object for idempotency key generation |
| `src/API/Domain/Entities/ProcessingRecord.cs` | Entity for Table Storage row |
| `src/API/Application/Options/IdempotencyOptions.cs` | Configuration options for idempotency |
| `src/API/Application/Options/HealthCheckOptions.cs` | Configuration options for health checks |
| `src/API/Application/Services/IHealthCheckService.cs` | Interface for health check service |
| `src/API/Application/Services/HealthCheckService.cs` | Orchestrates all health checks |
| `src/API/Application/DTOs/HealthCheckResult.cs` | DTO for health check response |
| `src/API/Application/DTOs/ComponentHealth.cs` | DTO for individual component health |
| `src/API/Infrastructure/Storage/TableStorageIdempotencyService.cs` | Table Storage implementation |
| `src/API/Infrastructure/HealthChecks/BlobStorageHealthCheck.cs` | Blob health check |
| `src/API/Infrastructure/HealthChecks/QueueStorageHealthCheck.cs` | Queue health check |
| `src/API/Infrastructure/HealthChecks/MetadataApiHealthCheck.cs` | API health check |
| `src/API/Functions/HealthCheckFunction.cs` | HTTP-triggered health endpoints |
| `tests/API.Tests/Infrastructure/IdempotencyServiceTests.cs` | Unit tests for idempotency |
| `tests/API.Tests/Application/HealthCheckServiceTests.cs` | Unit tests for health checks |
| `tests/API.IntegrationTests/HealthCheckIntegrationTests.cs` | Integration tests for health endpoints |
| `tests/API.IntegrationTests/IdempotencyIntegrationTests.cs` | Integration tests for idempotency |
| `docs/architecture/ADR-003-idempotency-pattern.md` | Architecture decision record |

### Files to Modify

| File | Description |
|------|-------------|
| `src/API/LabResultsGateway.API.csproj` | Add Azure.Data.Tables package reference |
| `src/API/Program.cs` | Register idempotency and health check services |
| `src/API/local.settings.json` | Add idempotency and health check configuration |
| `src/API/LabResultBlobProcessor.cs` | Integrate idempotency checks |
| `docs/SETUP.md` | Add health check documentation |

## 6. Testing

### Unit Tests

| Test | Description |
|------|-------------|
| TEST-001 | IdempotencyKey generates consistent hash from blob name and content |
| TEST-002 | TableStorageIdempotencyService correctly checks for existing records |
| TEST-003 | TableStorageIdempotencyService correctly creates new records with TTL |
| TEST-004 | HealthCheckService aggregates component health statuses correctly |
| TEST-005 | HealthCheckService returns cached result within cache TTL |
| TEST-006 | BlobStorageHealthCheck returns healthy when container exists |
| TEST-007 | QueueStorageHealthCheck returns unhealthy when queue missing |
| TEST-008 | MetadataApiHealthCheck handles timeout gracefully |

### Integration Tests

| Test | Description |
|------|-------------|
| TEST-009 | Idempotency prevents duplicate processing of same blob with Azurite |
| TEST-010 | Idempotency allows reprocessing after TTL expiration |
| TEST-011 | Health endpoint returns 200 when all dependencies healthy |
| TEST-012 | Health endpoint returns 503 when any dependency unhealthy |
| TEST-013 | Readiness probe fails when Metadata API unavailable |
| TEST-014 | Liveness probe succeeds even when external dependencies down |

### Manual Testing Checklist

| Test | Description |
|------|-------------|
| TEST-015 | Upload same PDF twice, verify second upload skipped with warning log |
| TEST-016 | Verify Table Storage contains processing record after successful processing |
| TEST-017 | Call `/api/health` endpoint, verify JSON response with all components |
| TEST-018 | Stop Azurite, call health endpoint, verify blob/queue components show unhealthy |
| TEST-019 | Verify health check caching by calling endpoint twice within 30 seconds |

## 7. Risks & Assumptions

### Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| RISK-001: Table Storage throttling under high load | Idempotency checks fail, duplicate processing possible | Use Table Storage reserved capacity, implement retry with exponential backoff |
| RISK-002: Content hash collision (SHA256) | Two different blobs could have same hash, causing false positive | Extremely unlikely (1 in 2^256), use blob name + hash combination |
| RISK-003: Health check timeout causes cascading delays | Health endpoint slow response affects load balancer health checks | Implement per-component timeouts with parallel execution |
| RISK-004: Clock skew affecting TTL calculations | Records may expire prematurely or persist too long | Use server-side TTL in Table Storage, not client-side expiration check |

### Assumptions

| Assumption | Validation |
|------------|------------|
| ASM-001: Blob names are unique within container | Validated by Azure Storage behavior |
| ASM-002: Table Storage in same region as Functions for low latency | To be verified in deployment configuration |
| ASM-003: Health check endpoints don't need authentication | Confirmed acceptable for monitoring tools integration |
| ASM-004: 24-hour TTL is sufficient for preventing duplicates | Business requirement - may need adjustment based on operational experience |

## 8. Related Specifications / Further Reading

- [Azure Table Storage Best Practices](https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-design-guidelines)
- [Idempotency Patterns](https://microservices.io/patterns/communication-style/idempotent-consumer.html)
- [Azure Functions HTTP Triggers](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger)
- [Kubernetes Health Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [ADR-001: Exception Hierarchy](./completed/feature-lab-results-ddd-processing-1.md) - Existing architecture patterns
