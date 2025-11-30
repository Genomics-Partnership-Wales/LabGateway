---
goal: Implement Saga Coordinator Pattern for Distributed Transaction Management
version: 1.0
date_created: 2025-11-27
last_updated: 2025-11-27
owner: Development Team
status: 'Pending'
priority: 'Lower'
tags: [feature, architecture, saga-pattern, distributed-transactions, compensation, reliability]
---

# Introduction

![Status: Pending](https://img.shields.io/badge/status-Pending-yellow)
![Priority: Lower](https://img.shields.io/badge/priority-Lower-blue)

This implementation plan defines the architecture and implementation steps for adding the Saga Coordinator pattern to the Lab Results Gateway. The Saga pattern manages distributed transactions across multiple services by coordinating a sequence of local transactions with compensating actions for rollback scenarios.

## Business Context

The Lab Results Gateway workflow involves multiple steps that span different services:
1. **Blob Processing** → 2. **Metadata Retrieval** → 3. **HL7 Generation** → 4. **Queue Publishing** → 5. **External Delivery**

Currently, if step 5 fails after steps 1-4 have completed, there's no automated way to handle the partial completion state. The Saga Coordinator pattern provides:

- **Explicit state machine** for tracking multi-step workflows
- **Compensating transactions** to undo completed steps on failure
- **Workflow visibility** through saga state persistence
- **Manual intervention** capabilities for stuck or failed sagas

## Business Value

- **Consistency**: Ensures system reaches consistent state even after partial failures
- **Visibility**: Operations team can see workflow progress and intervene when needed
- **Reliability**: Automated compensation reduces manual error recovery

## 1. Requirements & Constraints

### Functional Requirements - Saga Coordinator

- **REQ-SC-001**: System SHALL track each lab report processing workflow as a saga instance
- **REQ-SC-002**: System SHALL define saga steps: BlobReceived, MetadataRetrieved, Hl7Generated, MessageQueued, MessageDelivered
- **REQ-SC-003**: System SHALL persist saga state to durable storage (Azure Table Storage)
- **REQ-SC-004**: System SHALL support forward execution (happy path) and backward compensation (failure path)
- **REQ-SC-005**: System SHALL define compensating actions for each step:
  - BlobReceived → Move blob back from Failed folder (if moved)
  - MetadataRetrieved → No compensation needed (read-only)
  - Hl7Generated → No compensation needed (in-memory)
  - MessageQueued → Delete message from queue
  - MessageDelivered → Send cancellation notification (if supported by endpoint)
- **REQ-SC-006**: System SHALL mark sagas as: Started, InProgress, Completed, Failed, Compensating, Compensated
- **REQ-SC-007**: System SHALL support saga timeout with automatic compensation after configurable period
- **REQ-SC-008**: System SHALL provide API endpoint to query saga status by correlation ID
- **REQ-SC-009**: System SHALL provide API endpoint to manually trigger compensation for stuck sagas
- **REQ-SC-010**: System SHALL emit domain events for saga state transitions

### Functional Requirements - Saga Dashboard (Future)

- **REQ-SD-001**: System SHOULD provide API endpoints for listing active/failed sagas
- **REQ-SD-002**: System SHOULD provide API endpoint for saga retry/resume
- **REQ-SD-003**: System SHOULD provide metrics for saga completion rates and durations

### Non-Functional Requirements

- **NFR-001**: Saga state persistence SHALL be < 50ms latency
- **NFR-002**: Saga coordinator overhead SHALL be < 100ms total per workflow
- **NFR-003**: System SHALL support 100+ concurrent saga instances
- **NFR-004**: Saga state SHALL be retained for 30 days for audit purposes
- **NFR-005**: System SHALL use structured logging for all saga state transitions
- **NFR-006**: System SHALL integrate with existing OpenTelemetry tracing

### Technical Constraints

- **CON-001**: Must use Azure Table Storage for saga state (consistent with other patterns)
- **CON-002**: Must not introduce Durable Functions dependency (keep architecture simple)
- **CON-003**: Must maintain backward compatibility with existing processing flow
- **CON-004**: Compensating actions must be idempotent

## 2. Implementation Steps

### Phase 1: Saga State Machine Definition

**GOAL-001**: Define the saga state machine and core abstractions

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `src/API/Domain/Saga/ISaga.cs` interface with properties: SagaId, CorrelationId, CurrentStep, Status, Steps, StartedAt, CompletedAt | ⬜ | |
| TASK-002 | Create `src/API/Domain/Saga/SagaStep.cs` record with properties: Name, Status, StartedAt, CompletedAt, Error, CompensationRequired | ⬜ | |
| TASK-003 | Create `src/API/Domain/Saga/SagaStatus.cs` enum: NotStarted, InProgress, Completed, Failed, Compensating, Compensated, Abandoned | ⬜ | |
| TASK-004 | Create `src/API/Domain/Saga/StepStatus.cs` enum: Pending, InProgress, Completed, Failed, Compensated | ⬜ | |
| TASK-005 | Create `src/API/Domain/Saga/LabReportProcessingSaga.cs` defining the specific steps for lab report workflow | ⬜ | |
| TASK-006 | Create `src/API/Domain/Saga/Events/SagaStartedEvent.cs` domain event | ⬜ | |
| TASK-007 | Create `src/API/Domain/Saga/Events/SagaStepCompletedEvent.cs` domain event | ⬜ | |
| TASK-008 | Create `src/API/Domain/Saga/Events/SagaCompletedEvent.cs` domain event | ⬜ | |
| TASK-009 | Create `src/API/Domain/Saga/Events/SagaFailedEvent.cs` domain event | ⬜ | |
| TASK-010 | Create `src/API/Domain/Saga/Events/SagaCompensationStartedEvent.cs` domain event | ⬜ | |

### Phase 2: Saga Persistence Layer

**GOAL-002**: Create the saga state persistence infrastructure

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-011 | Create `src/API/Application/Services/ISagaRepository.cs` interface with methods: `Task<ISaga> GetByIdAsync(string sagaId)`, `Task<ISaga> GetByCorrelationIdAsync(string correlationId)`, `Task SaveAsync(ISaga saga)`, `Task<IList<ISaga>> GetActiveAsync()`, `Task<IList<ISaga>> GetFailedAsync()` | ⬜ | |
| TASK-012 | Create `src/API/Application/Options/SagaOptions.cs` configuration: TableName, TimeoutMinutes, RetentionDays | ⬜ | |
| TASK-013 | Create `src/API/Infrastructure/Saga/TableStorageSagaRepository.cs` implementing ISagaRepository with Azure Table Storage | ⬜ | |
| TASK-014 | Create `src/API/Infrastructure/Saga/SagaTableEntity.cs` mapping entity for Table Storage | ⬜ | |
| TASK-015 | Register ISagaRepository in `Program.cs` with configuration binding | ⬜ | |
| TASK-016 | Add saga configuration to `local.settings.json`: SagaTableName, SagaTimeoutMinutes, SagaRetentionDays | ⬜ | |

### Phase 3: Saga Coordinator Service

**GOAL-003**: Create the saga coordinator that manages workflow execution

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-017 | Create `src/API/Application/Saga/ISagaCoordinator.cs` interface with methods: `Task<string> StartSagaAsync(string correlationId, string blobName)`, `Task CompleteStepAsync(string sagaId, string stepName)`, `Task FailStepAsync(string sagaId, string stepName, string error)`, `Task StartCompensationAsync(string sagaId)` | ⬜ | |
| TASK-018 | Create `src/API/Application/Saga/SagaCoordinator.cs` implementing ISagaCoordinator with state machine logic | ⬜ | |
| TASK-019 | Implement forward execution logic: advance to next step on completion | ⬜ | |
| TASK-020 | Implement failure handling: mark saga as failed, optionally trigger compensation | ⬜ | |
| TASK-021 | Implement compensation logic: execute compensating actions in reverse order | ⬜ | |
| TASK-022 | Inject IDomainEventDispatcher and raise saga events on state transitions | ⬜ | |
| TASK-023 | Register ISagaCoordinator in `Program.cs` | ⬜ | |

### Phase 4: Compensating Action Handlers

**GOAL-004**: Create compensating action implementations for each saga step

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-024 | Create `src/API/Application/Saga/ICompensatingAction.cs` interface with method: `Task CompensateAsync(ISaga saga, SagaStep step)` | ⬜ | |
| TASK-025 | Create `src/API/Application/Saga/Compensations/BlobReceivedCompensation.cs` - moves blob back from Failed folder if needed | ⬜ | |
| TASK-026 | Create `src/API/Application/Saga/Compensations/MessageQueuedCompensation.cs` - deletes message from queue if possible | ⬜ | |
| TASK-027 | Create `src/API/Application/Saga/Compensations/NoOpCompensation.cs` - for read-only steps that don't need compensation | ⬜ | |
| TASK-028 | Create `src/API/Application/Saga/CompensationRegistry.cs` mapping step names to compensating actions | ⬜ | |
| TASK-029 | Register compensating actions in `Program.cs` | ⬜ | |

### Phase 5: Integrate Saga into Processing Flow

**GOAL-005**: Integrate the saga coordinator into existing processing components

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-030 | Inject ISagaCoordinator into LabResultBlobProcessor constructor | ⬜ | |
| TASK-031 | Start saga at beginning of LabResultBlobProcessor.Run with correlation ID | ⬜ | |
| TASK-032 | Inject ISagaCoordinator into LabReportProcessor constructor | ⬜ | |
| TASK-033 | Complete "MetadataRetrieved" step after successful metadata fetch | ⬜ | |
| TASK-034 | Complete "Hl7Generated" step after successful HL7 building | ⬜ | |
| TASK-035 | Complete "MessageQueued" step after successful queue publish | ⬜ | |
| TASK-036 | Inject ISagaCoordinator into QueueMessageProcessor constructor | ⬜ | |
| TASK-037 | Complete "MessageDelivered" step on successful external delivery | ⬜ | |
| TASK-038 | Fail step and trigger compensation on processing exceptions | ⬜ | |

### Phase 6: Saga Management API

**GOAL-006**: Create HTTP endpoints for saga monitoring and management

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-039 | Create `src/API/Functions/SagaManagementFunction.cs` with HTTP triggers | ⬜ | |
| TASK-040 | Implement `GET /api/saga/{correlationId}` - get saga status by correlation ID | ⬜ | |
| TASK-041 | Implement `GET /api/saga?status=failed` - list failed sagas | ⬜ | |
| TASK-042 | Implement `POST /api/saga/{sagaId}/compensate` - trigger manual compensation | ⬜ | |
| TASK-043 | Implement `POST /api/saga/{sagaId}/retry` - retry failed saga from last successful step | ⬜ | |
| TASK-044 | Add Function-level authorization for saga management endpoints | ⬜ | |

### Phase 7: Saga Timeout Handler

**GOAL-007**: Create background processor for timed-out sagas

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-045 | Create `src/API/Functions/SagaTimeoutFunction.cs` timer-triggered function (every 5 minutes) | ⬜ | |
| TASK-046 | Implement logic to find sagas that have been InProgress beyond timeout threshold | ⬜ | |
| TASK-047 | Mark timed-out sagas as Failed and optionally trigger compensation | ⬜ | |
| TASK-048 | Log warnings for timed-out sagas with correlation IDs | ⬜ | |
| TASK-049 | Emit SagaTimedOutEvent domain event | ⬜ | |

### Phase 8: Testing and Documentation

**GOAL-008**: Create comprehensive tests and documentation

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-050 | Create `tests/API.Tests/Domain/Saga/LabReportProcessingSagaTests.cs` testing state machine transitions | ⬜ | |
| TASK-051 | Create `tests/API.Tests/Application/Saga/SagaCoordinatorTests.cs` testing coordinator logic | ⬜ | |
| TASK-052 | Create `tests/API.Tests/Infrastructure/Saga/TableStorageSagaRepositoryTests.cs` testing persistence | ⬜ | |
| TASK-053 | Create `tests/API.IntegrationTests/SagaIntegrationTests.cs` testing full saga lifecycle with Azurite | ⬜ | |
| TASK-054 | Create `tests/API.IntegrationTests/SagaCompensationIntegrationTests.cs` testing compensation flow | ⬜ | |
| TASK-055 | Create `docs/architecture/ADR-006-saga-coordinator.md` documenting saga design decisions | ⬜ | |
| TASK-056 | Update `docs/SETUP.md` with saga configuration and monitoring documentation | ⬜ | |

## 3. Alternatives

### Saga Implementation Alternatives

- **ALT-001**: **Azure Durable Functions** - Could use Durable Functions Orchestrations for saga management. REJECTED because: adds significant complexity, different programming model, harder to debug, current requirements don't justify the overhead.

- **ALT-002**: **NServiceBus Saga support** - Could use NServiceBus with built-in saga persistence. REJECTED because: adds commercial license dependency, overkill for single-service scenario, introduces NServiceBus framework dependency.

- **ALT-003**: **MassTransit Saga State Machine** - Could use MassTransit Automatonymous for saga state machines. REJECTED because: designed for distributed scenarios, adds message broker dependency, overcomplicates in-process workflow management.

- **ALT-004**: **Event Sourcing for saga state** - Could use event sourcing to track saga state transitions. REJECTED because: adds complexity without clear benefit, Table Storage provides sufficient audit trail, event sourcing requires additional infrastructure.

### Compensation Strategy Alternatives

- **ALT-005**: **Backward recovery only (no forward retry)** - Could only support compensation, not retry. REJECTED because: reduces operational flexibility, many failures are transient and benefit from retry.

- **ALT-006**: **Semantic locks** - Could use locking to prevent concurrent saga modifications. REJECTED because: adds complexity, optimistic concurrency with Table Storage ETag is sufficient.

## 4. Dependencies

### New NuGet Packages

- None required (uses existing Azure.Data.Tables)

### Existing Dependencies Used

- **DEP-001**: `Azure.Data.Tables v12.x` - Already added in Plan 1, reused for saga state
- **DEP-002**: `Azure.Storage.Queues v12.x` - Used for queue-based compensation
- **DEP-003**: `Azure.Storage.Blobs v12.x` - Used for blob-based compensation

### Internal Dependencies

- **DEP-004**: Domain Events (from Plan 2) - Saga emits domain events for state transitions
- **DEP-005**: Idempotency Service (from Plan 1) - Compensating actions must be idempotent
- **DEP-006**: Outbox Pattern (from Plan 2) - Optional integration for reliable saga event publishing

## 5. Files

### Files to Create

| File | Description |
|------|-------------|
| `src/API/Domain/Saga/ISaga.cs` | Core saga interface |
| `src/API/Domain/Saga/SagaStep.cs` | Step definition record |
| `src/API/Domain/Saga/SagaStatus.cs` | Saga status enum |
| `src/API/Domain/Saga/StepStatus.cs` | Step status enum |
| `src/API/Domain/Saga/LabReportProcessingSaga.cs` | Lab report workflow saga definition |
| `src/API/Domain/Saga/Events/SagaStartedEvent.cs` | Domain event |
| `src/API/Domain/Saga/Events/SagaStepCompletedEvent.cs` | Domain event |
| `src/API/Domain/Saga/Events/SagaCompletedEvent.cs` | Domain event |
| `src/API/Domain/Saga/Events/SagaFailedEvent.cs` | Domain event |
| `src/API/Domain/Saga/Events/SagaCompensationStartedEvent.cs` | Domain event |
| `src/API/Application/Services/ISagaRepository.cs` | Repository interface |
| `src/API/Application/Options/SagaOptions.cs` | Configuration options |
| `src/API/Application/Saga/ISagaCoordinator.cs` | Coordinator interface |
| `src/API/Application/Saga/SagaCoordinator.cs` | Coordinator implementation |
| `src/API/Application/Saga/ICompensatingAction.cs` | Compensation interface |
| `src/API/Application/Saga/CompensationRegistry.cs` | Maps steps to compensations |
| `src/API/Application/Saga/Compensations/BlobReceivedCompensation.cs` | Blob compensation |
| `src/API/Application/Saga/Compensations/MessageQueuedCompensation.cs` | Queue compensation |
| `src/API/Application/Saga/Compensations/NoOpCompensation.cs` | No-op for read-only steps |
| `src/API/Infrastructure/Saga/TableStorageSagaRepository.cs` | Table Storage implementation |
| `src/API/Infrastructure/Saga/SagaTableEntity.cs` | Table entity mapping |
| `src/API/Functions/SagaManagementFunction.cs` | HTTP management endpoints |
| `src/API/Functions/SagaTimeoutFunction.cs` | Timer-triggered timeout handler |
| `tests/API.Tests/Domain/Saga/LabReportProcessingSagaTests.cs` | State machine tests |
| `tests/API.Tests/Application/Saga/SagaCoordinatorTests.cs` | Coordinator tests |
| `tests/API.Tests/Infrastructure/Saga/TableStorageSagaRepositoryTests.cs` | Repository tests |
| `tests/API.IntegrationTests/SagaIntegrationTests.cs` | Full saga lifecycle tests |
| `tests/API.IntegrationTests/SagaCompensationIntegrationTests.cs` | Compensation tests |
| `docs/architecture/ADR-006-saga-coordinator.md` | Architecture decision record |

### Files to Modify

| File | Description |
|------|-------------|
| `src/API/Program.cs` | Register saga services |
| `src/API/local.settings.json` | Add saga configuration |
| `src/API/LabResultBlobProcessor.cs` | Integrate saga start |
| `src/API/Application/Services/LabReportProcessor.cs` | Integrate saga step completions |
| `src/API/Functions/QueueMessageProcessor.cs` | Integrate saga completion |
| `docs/SETUP.md` | Add saga documentation |

## 6. Testing

### Unit Tests

| Test | Description |
|------|-------------|
| TEST-001 | LabReportProcessingSaga initializes with correct steps |
| TEST-002 | Saga transitions from NotStarted to InProgress on start |
| TEST-003 | Saga transitions to Completed when all steps complete |
| TEST-004 | Saga transitions to Failed on step failure |
| TEST-005 | Saga transitions to Compensating when compensation triggered |
| TEST-006 | SagaCoordinator persists state after each transition |
| TEST-007 | SagaCoordinator raises domain events for transitions |
| TEST-008 | CompensationRegistry returns correct action for each step |

### Integration Tests

| Test | Description |
|------|-------------|
| TEST-009 | Full saga happy path: start → all steps complete → Completed |
| TEST-010 | Saga failure at step 3: start → 2 steps complete → failure → compensation |
| TEST-011 | Saga timeout: start → InProgress for too long → Failed |
| TEST-012 | Manual compensation via API endpoint |
| TEST-013 | Saga retry from failed step via API endpoint |
| TEST-014 | Concurrent saga instances don't interfere |

### Manual Testing Checklist

| Test | Description |
|------|-------------|
| TEST-015 | Upload PDF, verify saga created and completed in Table Storage |
| TEST-016 | Force metadata API failure, verify saga marked as Failed |
| TEST-017 | Call `/api/saga/{correlationId}`, verify JSON response with steps |
| TEST-018 | Call `/api/saga?status=failed`, verify list of failed sagas |
| TEST-019 | Trigger manual compensation, verify blob moved back |
| TEST-020 | Wait for timeout, verify saga marked as Failed automatically |

## 7. Risks & Assumptions

### Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| RISK-001: Saga coordinator adds latency to processing | Slower blob processing | Keep persistence async where possible, use efficient Table Storage queries |
| RISK-002: Compensation actions fail | System in inconsistent state | Make compensations idempotent, support manual retry, log all failures |
| RISK-003: Too many active sagas overwhelm storage | Performance degradation | Implement saga cleanup, monitor active saga count, set alerts |
| RISK-004: Saga state becomes stale during long operations | Incorrect state decisions | Use optimistic concurrency (ETags), refresh state before transitions |
| RISK-005: Compensating actions have side effects | Unintended data changes | Ensure compensations only undo, never create new data, extensive testing |

### Assumptions

| Assumption | Validation |
|------------|------------|
| ASM-001: External endpoint doesn't support cancellation messages | Confirmed - compensation is best-effort for delivery step |
| ASM-002: Most sagas complete within 5 minutes | Monitor saga durations in production |
| ASM-003: Operations team will monitor saga dashboard | Training and documentation provided |
| ASM-004: Compensating actions are safe to retry multiple times | Design constraint enforced through code review |
| ASM-005: Plans 1 and 2 implemented before this plan | Dependency order in implementation |

## 8. Related Specifications / Further Reading

- [Saga Pattern](https://microservices.io/patterns/data/saga.html)
- [Compensating Transaction Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction)
- [Azure Durable Functions Sagas](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview) (for comparison)
- [CQRS and Event Sourcing](https://martinfowler.com/bliki/CQRS.html)
- [Plan 1: Idempotency and Health Checks](./feature-idempotency-health-checks-1.md) - Prerequisite patterns
- [Plan 2: Outbox and Domain Events](./feature-outbox-domain-events-1.md) - Prerequisite patterns
