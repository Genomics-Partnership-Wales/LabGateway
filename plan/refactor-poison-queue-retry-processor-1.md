---
goal: Refactor PoisonQueueRetryProcessor for SOLID, testability, and resilience
version: 1.0
date_created: 2025-11-29
last_updated: 2025-11-29
owner: Platform Engineering
status: 'Phase 1 Complete - Ready for Phase 2'
tags: [refactor, azure-functions, architecture, reliability, testing]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

This plan refactors the Azure Function in `src/API/PoisonQueueRetryProcessor.cs` to enforce SOLID principles, improve error handling, implement exponential backoff with jitter, and maximize testability by abstracting Azure SDK dependencies. The refactor maintains all original functionality and public surface, while reducing complexity and improving maintainability.

## 1. Requirements & Constraints

- **REQ-001**: Decompose monolithic logic into focused components/interfaces (SRP).
- **REQ-002**: All Azure SDK and retry logic must be abstracted behind interfaces.
- **REQ-003**: All configuration and magic numbers must be externalized and validated.
- **REQ-004**: Implement exponential backoff with jitter for retry delays.
- **REQ-005**: Remove pragma suppressions; use proper null checks and error handling.
- **REQ-006**: Maintain current functional behavior (max retries, dead-letter, delete on success).
- **REQ-007**: Structured logging and OpenTelemetry activity tagging must be preserved.
- **SEC-001**: Do not log sensitive message payloads; log only identifiers.
- **CON-001**: All changes must be limited to the API project; no breaking public API changes.
- **GUD-001**: Follow Azure Functions isolated worker and .NET best practices.
- **PAT-001**: Use fail-fast configuration validation and guard clauses.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Establish configuration, constants, and abstractions.

| Task      | Description                                                                                                 | Completed | Date       |
|-----------|-------------------------------------------------------------------------------------------------------------|-----------|------------|
| TASK-001  | Create `src/API/Application/Options/PoisonQueueRetryOptions.cs` with properties: MaxMessagesPerBatch (default: 10), MaxRetryAttempts (default: 3), BaseRetryDelayMinutes (default: 2), ProcessingVisibilityTimeoutMinutes (default: 5), UseJitter (default: true), MaxJitterPercentage (default: 0.3). Include const SectionName = "PoisonQueueRetry". | ✅        | 2025-11-29 |
| TASK-002  | Create `src/API/Infrastructure/Queue/IAzureQueueClient.cs` interface with methods: EnsureQueueExistsAsync, ReceiveMessagesAsync (returns IReadOnlyList<QueueMessageWrapper>), DeleteMessageAsync, UpdateMessageAsync. Define QueueMessageWrapper record with properties: MessageId, PopReceipt, MessageText, DequeueCount. | ✅        | 2025-11-29 |
| TASK-003  | Create `src/API/Application/Retry/IRetryStrategy.cs` interface with methods: CalculateNextRetryDelay (returns TimeSpan), ShouldRetry (returns bool). Define RetryContext record with properties: CorrelationId, CurrentRetryCount, MaxRetryAttempts. Define RetryResult enum with values: Success, Retry, DeadLetter. | ✅        | 2025-11-29 |
| TASK-004  | Create `src/API/Application/Processing/IPoisonQueueMessageProcessor.cs` interface with method: ProcessMessageAsync (returns Task<MessageProcessingResult>). Define MessageProcessingResult record with properties: Success (bool), Result (RetryResult), ErrorMessage (string?, optional). | ✅        | 2025-11-29 |
| TASK-005  | Create `src/API/Application/Processing/IPoisonQueueRetryOrchestrator.cs` interface with method: ProcessPoisonQueueAsync (returns Task, accepts CancellationToken). | ✅        | 2025-11-29 |
| TASK-006  | Add configuration section "PoisonQueueRetry" to `src/API/local.settings.json` with keys matching PoisonQueueRetryOptions properties. Ensure "StorageConnection" and "PoisonQueueName" keys exist in Values section. | ✅        | 2025-11-29 |

### Implementation Phase 2

- GOAL-002: Refactor logic into SRP-compliant classes and remove pragmas.

| Task      | Description                                                                                                 | Completed | Date       |
|-----------|-------------------------------------------------------------------------------------------------------------|-----------|------------|
| TASK-007  | Implement `src/API/Infrastructure/Queue/AzureQueueClient.cs` with constructor accepting QueueClient and ILogger<AzureQueueClient>. Implement EnsureQueueExistsAsync calling queueClient.CreateIfNotExistsAsync with try-catch. Implement ReceiveMessagesAsync calling queueClient.ReceiveMessagesAsync and mapping to QueueMessageWrapper list. Implement DeleteMessageAsync and UpdateMessageAsync with error handling. All catch blocks must log error with queue name and message ID, then throw InvalidOperationException. |           |            |
| TASK-008  | Implement `src/API/Application/Retry/ExponentialBackoffRetryStrategy.cs` with constructor accepting PoisonQueueRetryOptions and ILogger<ExponentialBackoffRetryStrategy>. Implement ShouldRetry returning (context.CurrentRetryCount < context.MaxRetryAttempts). Implement CalculateNextRetryDelay using formula: Math.Pow(options.BaseRetryDelayMinutes, context.CurrentRetryCount + 1), adding jitter if options.UseJitter is true using (1 + random * options.MaxJitterPercentage). Log calculated delay with correlation ID, current retry, and max retries. |           |            |
| TASK-009  | Implement `src/API/Application/Processing/PoisonQueueMessageProcessor.cs` with constructor accepting IMessageQueueService, IExternalEndpointService, IRetryStrategy, PoisonQueueRetryOptions, ActivitySource, ILogger<PoisonQueueMessageProcessor>. Implement ProcessMessageAsync: start activity "ProcessPoisonQueueMessage", deserialize message, check ShouldRetry, call SendToDeadLetterAsync if exceeded, attempt PostHl7MessageAsync, return appropriate MessageProcessingResult. Include private methods: DeserializeMessageAsync, TryPostToExternalEndpointAsync, SendToDeadLetterAsync (creates DeadLetterMessage and calls messageQueueService.SendToDeadLetterQueueAsync). |           |            |
| TASK-010  | Implement `src/API/Application/Processing/PoisonQueueRetryOrchestrator.cs` with constructor accepting IAzureQueueClient, IPoisonQueueMessageProcessor, IMessageQueueService, IRetryStrategy, PoisonQueueRetryOptions, ActivitySource, ILogger<PoisonQueueRetryOrchestrator>. Implement ProcessPoisonQueueAsync: start activity "ProcessPoisonQueue", call queueClient.EnsureQueueExistsAsync, retrieve messages with ReceiveMessagesAsync, create tasks for ProcessSingleMessageAsync for each message, await Task.WhenAll. Include private methods: ProcessSingleMessageAsync, HandleProcessingResultAsync (switch on RetryResult), UpdateMessageForRetryAsync (deserialize, increment retry count, serialize, calculate delay, update message). |           |            |
| TASK-011  | Refactor `src/API/PoisonQueueRetryProcessor.cs` to minimal Azure Function entry point. Replace all logic with constructor accepting IPoisonQueueRetryOrchestrator and ILogger<PoisonQueueRetryProcessor>. Update Run method body to single try-catch block: try { await orchestrator.ProcessPoisonQueueAsync(cancellationToken); } catch (Exception ex) { logger.LogCritical(ex, "Fatal error in poison queue retry processor"); throw; }. Remove all #pragma suppressions and null-forgiving operators. |           |            |
| TASK-012  | Create `src/API/Application/Extensions/PoisonQueueRetryServiceCollectionExtensions.cs` with static method AddPoisonQueueRetryServices(IServiceCollection services, IConfiguration configuration). Bind PoisonQueueRetryOptions from config section. Call ValidateConfiguration to check StorageConnection and PoisonQueueName exist. Register IAzureQueueClient as scoped factory creating QueueServiceClient and QueueClient. Register IRetryStrategy, IPoisonQueueMessageProcessor, IPoisonQueueRetryOrchestrator as scoped. |           |            |

### Implementation Phase 3

- GOAL-003: Integrate services into Program.cs and update configuration.

| Task      | Description                                                                                                 | Completed | Date       |
|-----------|-------------------------------------------------------------------------------------------------------------|-----------|------------|
| TASK-013  | Update `src/API/Program.cs` to call builder.Services.AddPoisonQueueRetryServices(builder.Configuration) after existing service registrations. Ensure this is called before builder.Build(). |           |            |
| TASK-014  | Verify all logging statements in PoisonQueueMessageProcessor include structured properties: CorrelationId, RetryCount, MessageId. Verify AzureQueueClient logs include QueueName. Verify PoisonQueueRetryOrchestrator logs include MessageCount. |           |            |
| TASK-015  | Verify all OpenTelemetry activities set tags: message.id, correlation.id, retry.count, message.count. Verify activities set status (ActivityStatusCode.Ok or ActivityStatusCode.Error) before disposal. |           |            |

### Implementation Phase 4

- GOAL-004: Enhance testability, logging, and observability.

| Task      | Description                                                                                                 | Completed | Date       |
|-----------|-------------------------------------------------------------------------------------------------------------|-----------|------------|
| TASK-016  | Create unit test file `tests/API.Tests/Application/Retry/ExponentialBackoffRetryStrategyTests.cs`. Test ShouldRetry returns true when CurrentRetryCount < MaxRetryAttempts, false otherwise. Test CalculateNextRetryDelay calculates correct exponential backoff (base^(retry+1)). Test jitter is applied when UseJitter is true, producing delay within expected range. Use xUnit, Moq, and FluentAssertions. |           |            |
| TASK-017  | Create unit test file `tests/API.Tests/Application/Processing/PoisonQueueMessageProcessorTests.cs`. Test ProcessMessageAsync returns DeadLetter when ShouldRetry returns false. Test returns Success when PostHl7MessageAsync returns true. Test returns Retry when PostHl7MessageAsync returns false. Test returns DeadLetter when deserialization fails. Mock IMessageQueueService, IExternalEndpointService, IRetryStrategy, ActivitySource. |           |            |
| TASK-018  | Create unit test file `tests/API.Tests/Application/Processing/PoisonQueueRetryOrchestratorTests.cs`. Test ProcessPoisonQueueAsync calls EnsureQueueExistsAsync. Test retrieves messages with correct batch size and visibility timeout. Test processes each message by calling ProcessSingleMessageAsync. Test deletes message when result is Success or DeadLetter. Test updates message when result is Retry. Mock IAzureQueueClient, IPoisonQueueMessageProcessor, IMessageQueueService, IRetryStrategy, ActivitySource. |           |            |
| TASK-019  | Create integration test file `tests/API.IntegrationTests/PoisonQueueRetryProcessorIntegrationTests.cs`. Use Testcontainers.Azurite NuGet package to spin up Azurite container. Test end-to-end flow: seed poison queue with test message, call orchestrator, verify message processed and deleted/updated. Verify dead letter queue receives message after max retries. |           |            |
| TASK-020  | Create unit test file `tests/API.Tests/Application/Extensions/PoisonQueueRetryServiceCollectionExtensionsTests.cs`. Test AddPoisonQueueRetryServices registers all expected services (IAzureQueueClient, IRetryStrategy, IPoisonQueueMessageProcessor, IPoisonQueueRetryOrchestrator). Test ValidateConfiguration throws InvalidOperationException when StorageConnection is missing. Test throws when PoisonQueueName is missing. |           |            |

## 3. Alternatives

- **ALT-001**: Use direct Azure SDK calls in business logic (rejected for testability and SRP).
- **ALT-002**: Implement retry logic as static helpers (rejected for OCP and extensibility).

## 4. Dependencies

- **DEP-001**: Azure.Storage.Queues SDK v12.18.0 or later (already referenced).
- **DEP-002**: Microsoft.Extensions.Options.ConfigurationExtensions v8.0.0 or later (for options binding).
- **DEP-003**: Microsoft.Extensions.DependencyInjection v8.0.0 or later (for service registration).
- **DEP-004**: System.Diagnostics.DiagnosticSource v8.0.0 or later (for ActivitySource).
- **DEP-005**: Existing `IMessageQueueService` interface (src/API/Application/Services/IMessageQueueService.cs).
- **DEP-006**: Existing `IExternalEndpointService` interface (src/API/Infrastructure/ExternalServices/).
- **DEP-007**: Existing `QueueMessageDto` and `DeadLetterMessage` DTOs (src/API/Application/DTOs/).
- **DEP-008**: xUnit v2.6.0 or later (for unit testing).
- **DEP-009**: Moq v4.20.0 or later (for mocking in unit tests).
- **DEP-010**: FluentAssertions v6.12.0 or later (for test assertions).
- **DEP-011**: Testcontainers.Azurite v3.5.0 or later (for integration testing).

## 5. Files

- **FILE-001**: src/API/PoisonQueueRetryProcessor.cs (refactored to minimal Azure Function entry point, ~40 lines)
- **FILE-002**: src/API/Infrastructure/Queue/IAzureQueueClient.cs (new interface, ~20 lines)
- **FILE-003**: src/API/Infrastructure/Queue/AzureQueueClient.cs (new implementation, ~120 lines)
- **FILE-004**: src/API/Infrastructure/Queue/QueueMessageWrapper.cs (new record type, ~10 lines)
- **FILE-005**: src/API/Application/Retry/IRetryStrategy.cs (new interface, ~10 lines)
- **FILE-006**: src/API/Application/Retry/ExponentialBackoffRetryStrategy.cs (new implementation, ~50 lines)
- **FILE-007**: src/API/Application/Retry/RetryContext.cs (new record type, ~8 lines)
- **FILE-008**: src/API/Application/Retry/RetryResult.cs (new enum, ~8 lines)
- **FILE-009**: src/API/Application/Processing/IPoisonQueueMessageProcessor.cs (new interface, ~10 lines)
- **FILE-010**: src/API/Application/Processing/PoisonQueueMessageProcessor.cs (new implementation, ~150 lines)
- **FILE-011**: src/API/Application/Processing/MessageProcessingResult.cs (new record type, ~8 lines)
- **FILE-012**: src/API/Application/Processing/IPoisonQueueRetryOrchestrator.cs (new interface, ~8 lines)
- **FILE-013**: src/API/Application/Processing/PoisonQueueRetryOrchestrator.cs (new implementation, ~180 lines)
- **FILE-014**: src/API/Application/Options/PoisonQueueRetryOptions.cs (new configuration class, ~40 lines)
- **FILE-015**: src/API/Application/Extensions/PoisonQueueRetryServiceCollectionExtensions.cs (new DI registration, ~60 lines)
- **FILE-016**: src/API/local.settings.json (updated with PoisonQueueRetry section)
- **FILE-017**: src/API/Program.cs (updated with AddPoisonQueueRetryServices call)
- **FILE-018**: tests/API.Tests/Application/Retry/ExponentialBackoffRetryStrategyTests.cs (new test file, ~100 lines)
- **FILE-019**: tests/API.Tests/Application/Processing/PoisonQueueMessageProcessorTests.cs (new test file, ~200 lines)
- **FILE-020**: tests/API.Tests/Application/Processing/PoisonQueueRetryOrchestratorTests.cs (new test file, ~250 lines)
- **FILE-021**: tests/API.Tests/Application/Extensions/PoisonQueueRetryServiceCollectionExtensionsTests.cs (new test file, ~80 lines)
- **FILE-022**: tests/API.IntegrationTests/PoisonQueueRetryProcessorIntegrationTests.cs (new test file, ~150 lines)

## 6. Testing

- **TEST-001**: Unit test `ExponentialBackoffRetryStrategy.ShouldRetry` returns true when CurrentRetryCount (0, 1, 2) < MaxRetryAttempts (3), returns false when CurrentRetryCount >= MaxRetryAttempts.
- **TEST-002**: Unit test `ExponentialBackoffRetryStrategy.CalculateNextRetryDelay` with BaseRetryDelayMinutes=2, UseJitter=false: verify delay is 4 minutes (2^1) for retry 0, 8 minutes (2^2) for retry 1, 16 minutes (2^3) for retry 2.
- **TEST-003**: Unit test `ExponentialBackoffRetryStrategy.CalculateNextRetryDelay` with UseJitter=true, MaxJitterPercentage=0.3: verify delay falls within range [baseDelay, baseDelay * 1.3].
- **TEST-004**: Unit test `PoisonQueueMessageProcessor.ProcessMessageAsync` when deserialization returns null: verify returns MessageProcessingResult with Success=false, Result=DeadLetter, ErrorMessage="Failed to deserialize message".
- **TEST-005**: Unit test `PoisonQueueMessageProcessor.ProcessMessageAsync` when ShouldRetry returns false: verify calls SendToDeadLetterAsync, returns MessageProcessingResult with Success=true, Result=DeadLetter.
- **TEST-006**: Unit test `PoisonQueueMessageProcessor.ProcessMessageAsync` when PostHl7MessageAsync returns true: verify returns MessageProcessingResult with Success=true, Result=Success.
- **TEST-007**: Unit test `PoisonQueueMessageProcessor.ProcessMessageAsync` when PostHl7MessageAsync returns false: verify returns MessageProcessingResult with Success=false, Result=Retry.
- **TEST-008**: Unit test `PoisonQueueMessageProcessor.ProcessMessageAsync` when exception thrown: verify catches, logs error, returns MessageProcessingResult with Success=false, Result=Retry, ErrorMessage=exception.Message.
- **TEST-009**: Unit test `PoisonQueueRetryOrchestrator.ProcessPoisonQueueAsync` with 0 messages: verify calls EnsureQueueExistsAsync, ReceiveMessagesAsync, logs "No messages to process", returns without processing.
- **TEST-010**: Unit test `PoisonQueueRetryOrchestrator.ProcessPoisonQueueAsync` with 3 messages: verify calls ProcessSingleMessageAsync 3 times, awaits Task.WhenAll.
- **TEST-011**: Unit test `PoisonQueueRetryOrchestrator.HandleProcessingResultAsync` with Result=Success: verify calls DeleteMessageAsync with correct messageId and popReceipt.
- **TEST-012**: Unit test `PoisonQueueRetryOrchestrator.HandleProcessingResultAsync` with Result=DeadLetter: verify calls DeleteMessageAsync.
- **TEST-013**: Unit test `PoisonQueueRetryOrchestrator.HandleProcessingResultAsync` with Result=Retry: verify calls UpdateMessageForRetryAsync.
- **TEST-014**: Unit test `PoisonQueueRetryOrchestrator.UpdateMessageForRetryAsync`: verify deserializes message, increments RetryCount by 1, serializes updated message, calculates delay via CalculateNextRetryDelay, calls UpdateMessageAsync with new serialized message and calculated delay.
- **TEST-015**: Integration test: create Azurite container, seed poison queue with test message (RetryCount=0), execute orchestrator.ProcessPoisonQueueAsync, verify message updated with RetryCount=1 and visibility timeout set.
- **TEST-016**: Integration test: seed poison queue with message exceeding max retries (RetryCount=3, MaxRetryAttempts=3), execute orchestrator, verify message deleted from poison queue, verify dead letter queue receives DeadLetterMessage.
- **TEST-017**: Unit test `PoisonQueueRetryServiceCollectionExtensions.ValidateConfiguration` with missing StorageConnection: verify throws InvalidOperationException with message "Missing required configuration settings: StorageConnection".
- **TEST-018**: Unit test `PoisonQueueRetryServiceCollectionExtensions.ValidateConfiguration` with missing PoisonQueueName: verify throws InvalidOperationException with message "Missing required configuration settings: PoisonQueueName".
- **TEST-019**: Unit test `PoisonQueueRetryServiceCollectionExtensions.AddPoisonQueueRetryServices`: verify registers IAzureQueueClient as Scoped, IRetryStrategy as Scoped, IPoisonQueueMessageProcessor as Scoped, IPoisonQueueRetryOrchestrator as Scoped, PoisonQueueRetryOptions as Singleton.

## 7. Risks & Assumptions

- **RISK-001**: Refactor may introduce subtle behavior changes; mitigate with comprehensive tests.
- **RISK-002**: Increased abstraction may add complexity; mitigated by clear interfaces and documentation.
- **ASSUMPTION-001**: Existing message contracts and queue setup remain unchanged.
- **ASSUMPTION-002**: Dead-letter queue infrastructure is present and stable.

## 8. Related Specifications / Further Reading

- [Azure Functions isolated worker guidance](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
- [Exponential backoff with jitter](https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/)
- [.NET Dependency Injection Best Practices](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
