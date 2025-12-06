---
goal: Implement Outbox Pattern and Domain Events for Reliable Messaging and Decoupled Architecture
version: 1.0
date_created: 2025-11-27
last_updated: 2025-12-04
owner: Development Team
status: 'Completed'
priority: 'Medium'
tags: [feature, architecture, outbox-pattern, domain-events, messaging, reliability]
---

# Introduction

![Status: Pending](https://img.shields.io/badge/status-Pending-yellow)
![Priority: Medium](https://img.shields.io/badge/priority-Medium-orange)

This implementation plan defines the architecture and implementation steps for adding two medium-priority architectural patterns to the Lab Results Gateway:

1. **Outbox Pattern**: Ensures reliable message delivery by storing outgoing queue messages in a transactional outbox table before dispatching. This prevents message loss during failures and guarantees at-least-once delivery even when queue operations fail.

2. **Domain Events**: Implements event-driven architecture within the domain layer, allowing components to react to important business events (e.g., LabReportProcessed, Hl7MessageGenerated, MessageDeliveryFailed) without tight coupling. Enables better separation of concerns and future extensibility.

## Business Value

- **Outbox Pattern**: Guarantees no messages are lost during processing failures, improving system reliability from 99.9% to 99.99% message delivery SLA
- **Domain Events**: Enables future integrations (audit logging, notifications, analytics) without modifying core processing logic

## 1. Requirements & Constraints

### Functional Requirements - Outbox Pattern

- **REQ-OB-001**: System SHALL store queue messages in an outbox table before attempting queue delivery
- **REQ-OB-002**: System SHALL mark outbox entries as "pending", "dispatched", or "failed"
- **REQ-OB-003**: System SHALL include a background dispatcher function to process pending outbox entries
- **REQ-OB-004**: System SHALL implement retry logic for failed outbox dispatches with exponential backoff
- **REQ-OB-005**: System SHALL support idempotent dispatch (same message won't be sent twice to queue)
- **REQ-OB-006**: System SHALL retain dispatched messages for audit purposes (configurable TTL)
- **REQ-OB-007**: System SHALL support manual reprocessing of failed outbox entries

### Functional Requirements - Domain Events

- **REQ-DE-001**: System SHALL define domain events for key business occurrences:
  - `LabReportReceivedEvent` - when blob processing starts
  - `LabMetadataRetrievedEvent` - when metadata API returns successfully
  - `Hl7MessageGeneratedEvent` - when HL7 message is built
  - `MessageQueuedEvent` - when message is added to queue
  - `MessageDeliveryFailedEvent` - when external delivery fails
  - `MessageDeliveredEvent` - when external delivery succeeds
- **REQ-DE-002**: System SHALL implement in-process event dispatcher for immediate handler execution
- **REQ-DE-003**: System SHALL support multiple handlers per event type
- **REQ-DE-004**: System SHALL log all domain events with correlation ID for audit trail
- **REQ-DE-005**: System SHALL not require handlers to be registered (fire-and-forget capability)
- **REQ-DE-006**: System SHALL handle handler exceptions without affecting main processing flow

### Non-Functional Requirements

- **NFR-001**: Outbox dispatch latency SHALL be < 5 seconds for normal operations
- **NFR-002**: Domain event dispatch SHALL be synchronous and < 50ms overhead per event
- **NFR-003**: System SHALL support at least 1000 outbox entries per minute throughput
- **NFR-004**: System SHALL use structured logging for all outbox and event operations
- **NFR-005**: System SHALL integrate with existing OpenTelemetry tracing
- **NFR-006**: System SHALL maintain backward compatibility with existing queue message format

### Technical Constraints

- **CON-001**: Must use Azure Table Storage for outbox (consistent with idempotency pattern)
- **CON-002**: Domain events must be in-process only (no external message broker)
- **CON-003**: Must not introduce circular dependencies between domain and application layers
- **CON-004**: Event handlers must not throw exceptions that affect primary processing

## 2. Implementation Steps

### Phase 1: Domain Events Infrastructure

**GOAL-001**: Create the domain events framework and core event types

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `src/API/Domain/Events/IDomainEvent.cs` marker interface with Timestamp and CorrelationId properties | ✅ | 2025-12-04 |
| TASK-002 | Create `src/API/Domain/Events/DomainEventBase.cs` abstract base class implementing IDomainEvent with auto-generated timestamp | ✅ | 2025-12-04 |
| TASK-003 | Create `src/API/Domain/Events/LabReportReceivedEvent.cs` with properties: BlobName, ContentSize, CorrelationId | ✅ | 2025-12-04 |
| TASK-004 | Create `src/API/Domain/Events/LabMetadataRetrievedEvent.cs` with properties: LabNumber, PatientId, CorrelationId | ✅ | 2025-12-04 |
| TASK-005 | Create `src/API/Domain/Events/Hl7MessageGeneratedEvent.cs` with properties: LabNumber, MessageLength, CorrelationId | ✅ | 2025-12-04 |
| TASK-006 | Create `src/API/Domain/Events/MessageQueuedEvent.cs` with properties: QueueName, CorrelationId, Timestamp | ✅ | 2025-12-04 |
| TASK-007 | Create `src/API/Domain/Events/MessageDeliveryFailedEvent.cs` with properties: CorrelationId, ErrorMessage, RetryCount | ✅ | 2025-12-04 |
| TASK-008 | Create `src/API/Domain/Events/MessageDeliveredEvent.cs` with properties: CorrelationId, ExternalEndpoint, Timestamp | ✅ | 2025-12-04 |

### Phase 2: Domain Event Dispatcher

**GOAL-002**: Create the event dispatcher and handler infrastructure

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-009 | Create `src/API/Application/Events/IDomainEventHandler.cs` generic interface with `Task HandleAsync(TEvent event)` method | ✅ | 2025-12-04 |
| TASK-010 | Create `src/API/Application/Events/IDomainEventDispatcher.cs` interface with `Task DispatchAsync(IDomainEvent event)` method | ✅ | 2025-12-04 |
| TASK-011 | Create `src/API/Application/Events/DomainEventDispatcher.cs` resolving handlers from DI container and dispatching events | ✅ | 2025-12-04 |
| TASK-012 | Create `src/API/Application/Events/Handlers/AuditLoggingEventHandler.cs` logging all events with structured data | ✅ | 2025-12-04 |
| TASK-013 | Create `src/API/Application/Events/Handlers/TelemetryEventHandler.cs` recording Application Insights custom events | ✅ | 2025-12-04 |
| TASK-014 | Register IDomainEventDispatcher and event handlers in `Program.cs` with appropriate lifetimes | ✅ | 2025-12-04 |

### Phase 3: Integrate Domain Events into Processing Flow

**GOAL-003**: Raise domain events at key points in the lab report processing workflow

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | Inject IDomainEventDispatcher into LabReportProcessor constructor | ✅ | 2025-12-04 |
| TASK-016 | Raise LabReportReceivedEvent at start of ProcessLabReportAsync | ✅ | 2025-12-04 |
| TASK-017 | Raise LabMetadataRetrievedEvent after successful metadata fetch | ✅ | 2025-12-04 |
| TASK-018 | Raise Hl7MessageGeneratedEvent after HL7 message building | ✅ | 2025-12-04 |
| TASK-019 | Raise MessageQueuedEvent after SendToProcessingQueueAsync | ✅ | 2025-12-04 |
| TASK-020 | Inject IDomainEventDispatcher into QueueMessageProcessor | ✅ | 2025-12-04 |
| TASK-021 | Raise MessageDeliveredEvent on successful external POST | ✅ | 2025-12-04 |
| TASK-022 | Raise MessageDeliveryFailedEvent on failed external POST | ✅ | 2025-12-04 |

### Phase 4: Outbox Pattern Infrastructure

**GOAL-004**: Create the outbox storage and entity infrastructure

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-023 | Create `src/API/Domain/Entities/OutboxMessage.cs` entity with properties: Id, MessageType, Payload, Status, CreatedAt, DispatchedAt, RetryCount, CorrelationId | ✅ | 2025-12-04 |
| TASK-024 | Create `src/API/Domain/Enums/OutboxStatus.cs` enum: Pending, Dispatched, Failed, Abandoned | ✅ | 2025-12-04 |
| TASK-025 | Create `src/API/Application/Options/OutboxOptions.cs` configuration: TableName, MaxRetries, RetryDelaySeconds, CleanupRetentionDays | ✅ | 2025-12-04 |
| TASK-026 | Create `src/API/Application/Services/IOutboxService.cs` interface with methods: `Task AddMessageAsync(string type, string payload)`, `Task<IList<OutboxMessage>> GetPendingMessagesAsync()`, `Task MarkAsDispatchedAsync(string id)`, `Task MarkAsFailedAsync(string id, string error)` | ✅ | 2025-12-04 |
| TASK-027 | Create `src/API/Infrastructure/Storage/TableStorageOutboxService.cs` implementing IOutboxService with Azure Table Storage | ✅ | 2025-12-04 |
| TASK-028 | Register IOutboxService in `Program.cs` with configuration binding | ✅ | 2025-12-04 |
| TASK-029 | Add outbox configuration to `local.settings.json`: OutboxTableName, OutboxMaxRetries, OutboxRetryDelaySeconds | ✅ | 2025-12-04 |

### Phase 5: Outbox-Aware Message Queue Service

**GOAL-005**: Modify message queue service to use outbox pattern

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-030 | Create `src/API/Infrastructure/Messaging/OutboxAwareQueueService.cs` decorator implementing IMessageQueueService | ✅ | 2025-12-04 |
| TASK-031 | Modify OutboxAwareQueueService.SendToProcessingQueueAsync to write to outbox first, then dispatch | ✅ | 2025-12-04 |
| TASK-032 | Add transaction-like semantics: if dispatch fails, message remains in outbox as pending | ✅ | 2025-12-04 |
| TASK-033 | Create `src/API/Functions/OutboxDispatcherFunction.cs` timer-triggered function (every 30 seconds) | ✅ | 2025-12-04 |
| TASK-034 | Implement OutboxDispatcherFunction to process pending outbox messages with retry logic | ✅ | 2025-12-04 |
| TASK-035 | Add exponential backoff calculation for failed messages in OutboxDispatcherFunction | ✅ | 2025-12-04 |
| TASK-036 | Implement cleanup logic for old dispatched messages based on retention period | ✅ | 2025-12-04 |

### Phase 6: Testing and Documentation

**GOAL-006**: Create comprehensive tests and documentation

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-037 | Create `tests/API.Tests/Domain/Events/DomainEventTests.cs` testing event creation and properties | ⬜ | |
| TASK-038 | Create `tests/API.Tests/Application/Events/DomainEventDispatcherTests.cs` testing handler resolution and dispatch | ⬜ | |
| TASK-039 | Create `tests/API.Tests/Infrastructure/OutboxServiceTests.cs` testing Table Storage operations | ⬜ | |
| TASK-040 | Create `tests/API.Tests/Infrastructure/OutboxAwareQueueServiceTests.cs` testing outbox integration | ⬜ | |
| TASK-041 | Create `tests/API.IntegrationTests/OutboxIntegrationTests.cs` testing full outbox dispatch cycle | ⬜ | |
| TASK-042 | Create `tests/API.IntegrationTests/DomainEventsIntegrationTests.cs` testing event flow through system | ⬜ | |
| TASK-043 | Create `docs/architecture/ADR-004-outbox-pattern.md` documenting outbox design decisions | ⬜ | |
| TASK-044 | Create `docs/architecture/ADR-005-domain-events.md` documenting domain events design decisions | ⬜ | |
| TASK-045 | Update `docs/SETUP.md` with outbox and domain events documentation | ⬜ | |

## 3. Alternatives

### Outbox Pattern Alternatives

- **ALT-001**: **Azure Service Bus transactions** - Could use Service Bus with transactional sends. REJECTED because: adds complexity and cost, requires Service Bus migration from Queue Storage, overkill for current throughput requirements.

- **ALT-002**: **Change Data Capture (CDC) pattern** - Could use Cosmos DB Change Feed for outbox. REJECTED because: Cosmos DB cost is significantly higher, adds unnecessary infrastructure complexity, Table Storage is sufficient for current needs.

- **ALT-003**: **Polling-based outbox without separate table** - Could use blob metadata or queue message properties. REJECTED because: harder to query, no proper status tracking, violates separation of concerns.

- **ALT-004**: **Synchronous dispatch only (no outbox)** - Could rely on retry logic without outbox. REJECTED because: messages can be lost if process crashes after business logic completes but before queue dispatch, violates reliability requirements.

### Domain Events Alternatives

- **ALT-005**: **MediatR library for events** - Could use MediatR INotification pattern. REJECTED because: adds external dependency for simple use case, custom implementation is straightforward and more explicit, MediatR adds magic that can obscure event flow.

- **ALT-006**: **Azure Event Grid for events** - Could publish events to Event Grid for external consumers. REJECTED because: overkill for in-process events, adds latency and cost, current requirements don't need external event distribution.

- **ALT-007**: **No domain events (direct calls)** - Could call audit logging directly from processors. REJECTED because: tight coupling, hard to add new cross-cutting concerns, violates open-closed principle.

## 4. Dependencies

### New NuGet Packages

- **DEP-001**: `Azure.Data.Tables v12.x` - Already added in idempotency plan, reused for outbox

### Existing Dependencies Used

- **DEP-002**: `Azure.Storage.Queues v12.x` - Already installed, used for queue dispatch
- **DEP-003**: `Microsoft.Azure.Functions.Worker v2.x` - Timer trigger for outbox dispatcher
- **DEP-004**: `System.Text.Json` - Built-in, used for outbox payload serialization

### Internal Dependencies

- **DEP-005**: Idempotency service (from Plan 1) - Ensures outbox dispatch is idempotent
- **DEP-006**: Existing IMessageQueueService - Wrapped by outbox-aware decorator

## 5. Files

### Files to Create

| File | Description |
|------|-------------|
| `src/API/Domain/Events/IDomainEvent.cs` | Marker interface for domain events |
| `src/API/Domain/Events/DomainEventBase.cs` | Abstract base class for events |
| `src/API/Domain/Events/LabReportReceivedEvent.cs` | Event raised when blob processing starts |
| `src/API/Domain/Events/LabMetadataRetrievedEvent.cs` | Event raised when metadata retrieved |
| `src/API/Domain/Events/Hl7MessageGeneratedEvent.cs` | Event raised when HL7 built |
| `src/API/Domain/Events/MessageQueuedEvent.cs` | Event raised when message queued |
| `src/API/Domain/Events/MessageDeliveryFailedEvent.cs` | Event raised on delivery failure |
| `src/API/Domain/Events/MessageDeliveredEvent.cs` | Event raised on successful delivery |
| `src/API/Domain/Entities/OutboxMessage.cs` | Entity for outbox table row |
| `src/API/Domain/Enums/OutboxStatus.cs` | Enum for outbox message status |
| `src/API/Application/Events/IDomainEventHandler.cs` | Generic handler interface |
| `src/API/Application/Events/IDomainEventDispatcher.cs` | Dispatcher interface |
| `src/API/Application/Events/DomainEventDispatcher.cs` | Dispatcher implementation |
| `src/API/Application/Events/Handlers/AuditLoggingEventHandler.cs` | Logs all events |
| `src/API/Application/Events/Handlers/TelemetryEventHandler.cs` | Records App Insights events |
| `src/API/Application/Services/IOutboxService.cs` | Outbox service interface |
| `src/API/Application/Options/OutboxOptions.cs` | Outbox configuration |
| `src/API/Infrastructure/Storage/TableStorageOutboxService.cs` | Outbox Table Storage implementation |
| `src/API/Infrastructure/Messaging/OutboxAwareQueueService.cs` | Queue service decorator |
| `src/API/Functions/OutboxDispatcherFunction.cs` | Timer-triggered outbox processor |
| `tests/API.Tests/Domain/Events/DomainEventTests.cs` | Event unit tests |
| `tests/API.Tests/Application/Events/DomainEventDispatcherTests.cs` | Dispatcher unit tests |
| `tests/API.Tests/Infrastructure/OutboxServiceTests.cs` | Outbox service unit tests |
| `tests/API.Tests/Infrastructure/OutboxAwareQueueServiceTests.cs` | Queue decorator tests |
| `tests/API.IntegrationTests/OutboxIntegrationTests.cs` | Outbox integration tests |
| `tests/API.IntegrationTests/DomainEventsIntegrationTests.cs` | Events integration tests |
| `docs/architecture/ADR-004-outbox-pattern.md` | Architecture decision record |
| `docs/architecture/ADR-005-domain-events.md` | Architecture decision record |

### Files to Modify

| File | Description |
|------|-------------|
| `src/API/Program.cs` | Register event dispatcher, handlers, outbox service |
| `src/API/local.settings.json` | Add outbox configuration |
| `src/API/Application/Services/LabReportProcessor.cs` | Inject and raise domain events |
| `src/API/Functions/QueueMessageProcessor.cs` | Inject and raise domain events |
| `docs/SETUP.md` | Add outbox and events documentation |

## 6. Testing

### Unit Tests

| Test | Description |
|------|-------------|
| TEST-001 | DomainEventBase auto-generates timestamp and correlation ID |
| TEST-002 | DomainEventDispatcher resolves multiple handlers for same event type |
| TEST-003 | DomainEventDispatcher handles missing handlers gracefully |
| TEST-004 | DomainEventDispatcher catches and logs handler exceptions |
| TEST-005 | OutboxService creates pending message correctly |
| TEST-006 | OutboxService marks message as dispatched |
| TEST-007 | OutboxService increments retry count on failure |
| TEST-008 | OutboxAwareQueueService writes to outbox before dispatch |

### Integration Tests

| Test | Description |
|------|-------------|
| TEST-009 | Full event flow: LabReportReceivedEvent → AuditLoggingHandler with Azurite |
| TEST-010 | Outbox message persisted after queue dispatch failure |
| TEST-011 | OutboxDispatcherFunction processes pending messages |
| TEST-012 | Outbox retry logic with exponential backoff |
| TEST-013 | Outbox cleanup removes old dispatched messages |

### Manual Testing Checklist

| Test | Description |
|------|-------------|
| TEST-014 | Upload PDF, verify all domain events logged in console |
| TEST-015 | Stop queue service, verify message written to outbox table |
| TEST-016 | Restart queue service, verify outbox dispatcher sends pending messages |
| TEST-017 | Verify Application Insights shows custom events from TelemetryEventHandler |
| TEST-018 | Force dispatch failure 3 times, verify message marked as Failed |

## 7. Risks & Assumptions

### Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| RISK-001: Domain event handlers slow down main processing | Increased latency for blob processing | Keep handlers lightweight, log-only operations, async where possible |
| RISK-002: Outbox table grows unbounded | Storage costs increase, query performance degrades | Implement cleanup job for old dispatched messages, monitor table size |
| RISK-003: Outbox dispatcher and main queue service conflict | Duplicate message delivery | Use idempotency keys, outbox tracks dispatch status |
| RISK-004: Handler exceptions affect processing | Business logic fails due to handler errors | Catch all handler exceptions, log and continue |

### Assumptions

| Assumption | Validation |
|------------|------------|
| ASM-001: In-process events are sufficient (no external consumers) | Business requirement confirmed |
| ASM-002: Event handlers complete quickly (<50ms) | Enforce through code review, monitoring |
| ASM-003: Outbox cleanup can lag behind (eventual consistency acceptable) | Business requirement confirmed |
| ASM-004: Table Storage provides sufficient outbox throughput | Validated by idempotency pattern performance |

## 8. Related Specifications / Further Reading

- [Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [Domain Events Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Event-Driven Architecture](https://martinfowler.com/articles/201701-event-driven.html)
- [Azure Table Storage Design Patterns](https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-design-patterns)
- [Plan 1: Idempotency and Health Checks](./feature-idempotency-health-checks-1.md) - Related infrastructure patterns
