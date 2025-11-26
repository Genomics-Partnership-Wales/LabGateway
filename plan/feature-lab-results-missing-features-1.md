---
goal: Complete Missing Features for DDD-based Lab Results Processing System
version: 1.0
date_created: 2025-11-26
last_updated: 2025-11-26
owner: Development Team
status: 'Completed'
tags: [feature, completion, opentelemetry, resilience, queue-processing]
---

# Introduction

![Status: In progress](https://img.shields.io/badge/status-In%20Progress-yellow)

This implementation plan addresses all missing features identified in the codebase review of the DDD-based Lab Results Processing System. The plan focuses on completing Phase 4 (Dependency Injection and OpenTelemetry Configuration) and implementing critical missing components including the queue-triggered function, resilience policies, and standardized message formats.

## 1. Requirements & Constraints

### Functional Requirements

- **REQ-012**: System SHALL implement queue-triggered Azure Function to process HL7 messages from `lab-reports-queue`
- **REQ-013**: System SHALL POST processed HL7 messages to external NHS Wales endpoint with proper error handling
- **REQ-014**: System SHALL implement standardized queue message format with HL7 content, metadata, and retry tracking
- **REQ-015**: System SHALL implement dead letter handling for messages exceeding max retry attempts
- **REQ-016**: System SHALL configure complete OpenTelemetry tracing with Azure Monitor exporter
- **REQ-017**: System SHALL implement Polly resilience policies for HTTP clients (retry, circuit breaker, timeout)
- **REQ-018**: System SHALL configure MSH segment values for HL7 message generation

### Non-Functional Requirements

- **NFR-011**: System SHALL maintain DDD architecture principles with proper dependency injection
- **NFR-012**: System SHALL implement distributed tracing with correlation IDs across all components
- **NFR-013**: System SHALL provide resilience against transient failures with configurable policies
- **NFR-014**: System SHALL ensure all configurations are properly documented and validated

### Security Requirements

- **SEC-006**: System SHALL maintain secure API key handling in production environments
- **SEC-007**: System SHALL implement proper timeout configurations to prevent resource exhaustion

### Technical Constraints

- **CON-009**: System MUST use OpenTelemetry standards for distributed tracing
- **CON-010**: System MUST implement Polly v8 patterns for resilience
- **CON-011**: Queue messages MUST include structured metadata for retry tracking
- **CON-012**: Dead letter messages MUST be archived with full context for analysis

### Guidelines & Best Practices

- **GUD-011**: Use structured logging with OpenTelemetry baggage for correlation
- **GUD-012**: Implement exponential backoff with jitter for retry policies
- **GUD-013**: Define clear message schemas for queue communication
- **GUD-014**: Configure appropriate timeout values based on external API SLAs

### Design Patterns

- **PAT-008**: Message Envelope Pattern for queue message structure
- **PAT-009**: Circuit Breaker Pattern for external API resilience
- **PAT-010**: Dead Letter Channel Pattern for failed message handling

## 2. Implementation Steps

### Implementation Phase 1: Queue Message Format and Dead Letter Handling

**GOAL-004**: Define standardized queue message structure and implement dead letter processing

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-027 | Define `Application/DTOs/QueueMessage.cs` record with properties: Hl7Message (string), CorrelationId (string), RetryCount (int), Timestamp (DateTimeOffset), BlobName (string) | ✅ | 2025-11-26 |
| TASK-028 | Define `Application/DTOs/DeadLetterMessage.cs` record extending QueueMessage with FailureReason (string), LastAttempt (DateTimeOffset) | ✅ | 2025-11-26 |
| TASK-029 | Update `IMessageQueueService.cs` to include methods: `Task<QueueMessage> DeserializeMessageAsync(string message)`, `Task<string> SerializeMessageAsync(QueueMessage message)`, `Task SendToDeadLetterQueueAsync(DeadLetterMessage message)` | ✅ | 2025-11-26 |
| TASK-030 | Implement dead letter handling in `AzureQueueService.cs` with dead letter queue operations and structured logging | ✅ | 2025-11-26 |
| TASK-031 | Update `PoisonQueueRetryProcessor.cs` to deserialize QueueMessage, track retry count from metadata, implement dead letter logic for max retries exceeded | ✅ | 2025-11-26 |

### Implementation Phase 2: Queue-Triggered Function Implementation

**GOAL-005**: Implement the missing queue-triggered Azure Function for processing HL7 messages

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-032 | Create `src/API/QueueMessageProcessor.cs` Azure Function with [QueueTrigger("lab-reports-queue")] attribute | ✅ | 2025-11-26 |
| TASK-033 | Implement message deserialization, correlation ID extraction, and structured logging in QueueMessageProcessor | ✅ | 2025-11-26 |
| TASK-034 | Add HL7 message validation and external endpoint posting logic with proper error handling | ✅ | 2025-11-26 |
| TASK-035 | Implement poison queue routing for failed messages with retry count increment | ✅ | 2025-11-26 |
| TASK-036 | Add OpenTelemetry activity spans and correlation ID propagation in queue processing | ✅ | 2025-11-26 |

### Implementation Phase 3: OpenTelemetry Configuration

**GOAL-006**: Complete OpenTelemetry setup with Azure Monitor exporter and HTTP instrumentation

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-037 | Add OpenTelemetry service registration in Program.cs with Azure Monitor exporter configuration | ✅ | 2025-11-26 |
| TASK-038 | Configure HTTP client instrumentation for MetadataApi and ExternalEndpoint named clients | ✅ | 2025-11-26 |
| TASK-039 | Add Azure Storage instrumentation for blob and queue operations |  |  |
| TASK-040 | Implement baggage propagation for correlation IDs across service boundaries |  |  |
| TASK-041 | Configure activity source naming and resource attributes for proper telemetry | ✅ | 2025-11-26 |

### Implementation Phase 4: Polly Resilience Policies

**GOAL-007**: Implement comprehensive resilience patterns using Polly v8

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-042 | Configure retry policy with exponential backoff for MetadataApi HTTP client | ✅ | 2025-11-26 |
| TASK-043 | Configure circuit breaker policy for ExternalEndpoint HTTP client with failure threshold | ✅ | 2025-11-26 |
| TASK-044 | Add timeout policies to prevent hanging requests on external APIs | ✅ | 2025-11-26 |
| TASK-045 | Implement bulkhead isolation for concurrent request limiting | ✅ | 2025-11-26 |
| TASK-046 | Add Polly context propagation for distributed tracing integration | ✅ | 2025-11-26 |

### Implementation Phase 5: Configuration Completion

**GOAL-008**: Complete all missing configuration values and validation

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-047 | Configure MSH segment values in local.settings.json and document required production values | ✅ | 2025-11-26 |
| TASK-048 | Add dead letter queue name configuration (DeadLetterQueueName) | ✅ | 2025-11-26 |
| TASK-049 | Implement configuration validation in Program.cs startup |  |  |
| TASK-050 | Add environment-specific configuration profiles for dev/test/prod |  |  |

## 3. Alternatives

- **ALT-001**: Use Azure Service Bus instead of Queue Storage for advanced messaging features (rejected due to additional complexity and cost)
- **ALT-002**: Implement custom retry logic instead of Polly (rejected due to Polly's proven reliability and feature set)
- **ALT-003**: Use Application Insights SDK directly instead of OpenTelemetry (rejected due to OpenTelemetry's industry standard status)

## 4. Dependencies

- **DEP-001**: OpenTelemetry.Exporter.AzureMonitor v1.1.0 or later
- **DEP-002**: Polly v8.4.0 or later (already added)
- **DEP-003**: Azure.Storage.Queues v12.18.0 or later (already added)
- **DEP-004**: Microsoft.Extensions.Http.Resilience v8.4.0 (for .NET 8+ resilience patterns)

## 5. Files

- **FILE-001**: `src/API/Application/DTOs/QueueMessage.cs` - New queue message DTO
- **FILE-002**: `src/API/Application/DTOs/DeadLetterMessage.cs` - New dead letter message DTO
- **FILE-003**: `src/API/QueueMessageProcessor.cs` - New queue-triggered function
- **FILE-004**: `src/API/Program.cs` - Updated with OpenTelemetry and Polly configuration
- **FILE-005**: `src/API/Infrastructure/Messaging/AzureQueueService.cs` - Updated with message serialization
- **FILE-006**: `src/API/PoisonQueueRetryProcessor.cs` - Updated with proper message handling
- **FILE-007**: `src/API/local.settings.json` - Updated with new configuration values

## 6. Testing

- **TEST-001**: Unit tests for QueueMessage and DeadLetterMessage serialization/deserialization
- **TEST-002**: Integration tests for queue-triggered function with mocked external endpoint
- **TEST-003**: Resilience testing with Polly policies (retry, circuit breaker scenarios)
- **TEST-004**: OpenTelemetry tracing validation across function calls
- **TEST-005**: Dead letter queue processing tests with max retry scenarios

## 7. Risks & Assumptions

- **RISK-001**: External NHS Wales API may have undocumented rate limits affecting retry policies
- **ASSUMPTION-001**: Queue message format changes won't break existing poison queue messages during deployment
- **RISK-002**: OpenTelemetry configuration may impact cold start performance of Azure Functions
- **ASSUMPTION-002**: Polly policies are compatible with Azure Functions isolated worker model

## 9. Runtime Setup Notes

**Azurite Local Storage Emulator**: The application requires Azurite to be running for local development. Start it with:
```bash
npx azurite --silent --location .
```

This will start the blob and queue storage emulators on localhost:10000 and localhost:10001 respectively, using the existing `__azurite_db_*.json` files in the workspace root.

**Configuration**: All required configurations have been added to `local.settings.json`. For production, ensure Key Vault is configured and environment variables are set appropriately.
