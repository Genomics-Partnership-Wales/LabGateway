---
goal: Refactor LabResultBlobProcessor for DRY exception handling and improved maintainability
version: 1.0
date_created: 2025-11-30
last_updated: 2025-11-30
owner: Platform Engineering
status: 'Planned'
tags: [refactor, azure-functions, architecture, dry, solid, exceptions]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

This plan refactors the Azure Function in `src/API/LabResultBlobProcessor.cs` to eliminate repetitive exception handling patterns, introduce a unified domain exception base class, centralize constants, and improve code maintainability. The refactor applies DRY (Don't Repeat Yourself) and SRP (Single Responsibility Principle) while maintaining all original functionality.

## 1. Requirements & Constraints

- **REQ-001**: Introduce base `LabProcessingException` class for all domain exceptions (DRY).
- **REQ-002**: Add `IsRetryable` property to exceptions to classify retry eligibility.
- **REQ-003**: Extract magic strings to constants class for maintainability.
- **REQ-004**: Consolidate repetitive catch blocks into unified exception handlers (DRY/SRP).
- **REQ-005**: Extract stream reading and helper methods to improve testability (SRP).
- **REQ-006**: Maintain current functional behavior (failed folder handling, telemetry, logging).
- **REQ-007**: Preserve structured logging with correlation IDs and OpenTelemetry tracing.
- **SEC-001**: Do not log sensitive payload data; log only identifiers and metadata.
- **CON-001**: All changes must be limited to the API project; no breaking public API changes.
- **CON-002**: Existing exception types must remain backward compatible (can still be caught individually).
- **GUD-001**: Follow Azure Functions isolated worker and .NET best practices.
- **GUD-002**: All new code must include XML documentation comments.
- **PAT-001**: Use guard clauses and fail-fast patterns for null checks.

## 2. Implementation Steps

### Implementation Phase 1: Domain Foundation

- GOAL-001: Establish base exception class and constants infrastructure.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `src/API/Domain/Exceptions/LabProcessingException.cs` as abstract base class inheriting from `Exception`. Add properties: `BlobName` (string?, init-only), `IsRetryable` (virtual bool, default false). Include protected constructors accepting (string message) and (string message, Exception innerException). Add XML documentation. | | |
| TASK-002 | Create `src/API/Domain/Constants/BlobConstants.cs` static class with constants: `FailedFolderPrefix = "Failed/"`, `LabResultsContainer = "lab-results-gateway"`. Add XML documentation. | | |

### Implementation Phase 2: Exception Hierarchy Refactoring

- GOAL-002: Update existing domain exceptions to inherit from `LabProcessingException`.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-003 | Refactor `src/API/Domain/Exceptions/LabNumberInvalidException.cs`: Change base class from `ArgumentException` to `LabProcessingException`. Keep all existing constructors but update base calls. Preserve `InvalidLabNumber` property. Override `IsRetryable` to return `false` (invalid input is not retryable). | | |
| TASK-004 | Refactor `src/API/Domain/Exceptions/MetadataNotFoundException.cs`: Change base class from `InvalidOperationException` to `LabProcessingException`. Keep all existing constructors but update base calls. Preserve `LabNumber` property. Override `IsRetryable` to return `true` (metadata service may have transient issues). | | |
| TASK-005 | Refactor `src/API/Domain/Exceptions/Hl7GenerationException.cs`: Change base class from `InvalidOperationException` to `LabProcessingException`. Keep all existing constructors but update base calls. Preserve `LabNumber` property. Override `IsRetryable` to return `false` (generation failures are deterministic). | | |

### Implementation Phase 3: Blob Processor Refactoring

- GOAL-003: Refactor LabResultBlobProcessor to use unified exception handling and helper methods.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-006 | Refactor `src/API/LabResultBlobProcessor.cs`: Add using statement for `LabResultsGateway.API.Domain.Constants`. Update BlobTrigger attribute to use `$"{BlobConstants.LabResultsContainer}/{{name}}"` interpolated string. | | |
| TASK-007 | Add private static helper method `SetActivityTags(Activity? activity, string correlationId, string blobName)` that sets "correlation.id" and "blob.name" tags on the activity. | | |
| TASK-008 | Add private static helper method `ShouldSkipProcessing(string blobName)` returning `blobName.StartsWith(BlobConstants.FailedFolderPrefix, StringComparison.OrdinalIgnoreCase)`. | | |
| TASK-009 | Add private static async method `ReadStreamAsync(Stream stream, CancellationToken cancellationToken)` returning `Task<byte[]>` that copies stream to MemoryStream and returns ToArray(). | | |
| TASK-010 | Add private async method `HandleLabProcessingExceptionAsync(LabProcessingException ex, string blobName, string correlationId, Activity? activity, CancellationToken cancellationToken)` that logs error with ExceptionType, BlobName, CorrelationId, IsRetryable; calls MoveToFailedFolderAsync; sets activity error status. | | |
| TASK-011 | Add private async method `HandleUnexpectedExceptionAsync(Exception ex, string blobName, string correlationId, Activity? activity, CancellationToken cancellationToken)` that logs error with BlobName, CorrelationId; calls MoveToFailedFolderAsync; sets activity error status. | | |
| TASK-012 | Refactor `Run` method: Replace inline activity tag setting with `SetActivityTags()` call. Replace inline skip check with `ShouldSkipProcessing()` call. Replace inline stream reading with `ReadStreamAsync()` call. Replace 4 catch blocks with 2 catch blocks: one for `LabProcessingException` calling `HandleLabProcessingExceptionAsync()`, one for `Exception` calling `HandleUnexpectedExceptionAsync()`. | | |

### Implementation Phase 4: Unit Testing

- GOAL-004: Create comprehensive unit tests for refactored components.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-013 | Create `tests/API.Tests/Domain/Exceptions/LabProcessingExceptionTests.cs` with tests: verify `IsRetryable` default is false; verify `BlobName` can be set via init; verify inheritance chain allows catching as `LabProcessingException`. | | |
| TASK-014 | Update or create `tests/API.Tests/Domain/Exceptions/LabNumberInvalidExceptionTests.cs` with tests: verify inherits from `LabProcessingException`; verify `IsRetryable` returns false; verify `InvalidLabNumber` property is set correctly. | | |
| TASK-015 | Update or create `tests/API.Tests/Domain/Exceptions/MetadataNotFoundExceptionTests.cs` with tests: verify inherits from `LabProcessingException`; verify `IsRetryable` returns true; verify `LabNumber` property is set correctly. | | |
| TASK-016 | Update or create `tests/API.Tests/Domain/Exceptions/Hl7GenerationExceptionTests.cs` with tests: verify inherits from `LabProcessingException`; verify `IsRetryable` returns false; verify `LabNumber` property is set correctly. | | |
| TASK-017 | Create `tests/API.Tests/Domain/Constants/BlobConstantsTests.cs` with tests: verify `FailedFolderPrefix` equals "Failed/"; verify `LabResultsContainer` equals "lab-results-gateway". | | |

## 3. Alternatives

- **ALT-001**: Use a marker interface `ILabProcessingException` instead of base class (rejected because base class allows shared implementation and `IsRetryable` default).
- **ALT-002**: Keep separate catch blocks for logging different error messages (rejected for violating DRY; exception type name is already captured in structured logging).
- **ALT-003**: Move stream reading to `ILabReportProcessor` service (rejected as over-engineering; function already has this responsibility clearly scoped).

## 4. Dependencies

- **DEP-001**: Existing `ILabReportProcessor` interface (src/API/Application/Services/ILabReportProcessor.cs).
- **DEP-002**: Existing `IBlobStorageService` interface (src/API/Application/Services/IBlobStorageService.cs).
- **DEP-003**: Existing domain exceptions in `src/API/Domain/Exceptions/`.
- **DEP-004**: System.Diagnostics.DiagnosticSource (for ActivitySource, already referenced).
- **DEP-005**: xUnit v2.6.0 or later (for unit testing, already referenced).
- **DEP-006**: FluentAssertions v6.12.0 or later (for test assertions, already referenced).

## 5. Files

- **FILE-001**: `src/API/Domain/Exceptions/LabProcessingException.cs` (new abstract base class, ~35 lines)
- **FILE-002**: `src/API/Domain/Constants/BlobConstants.cs` (new constants class, ~20 lines)
- **FILE-003**: `src/API/Domain/Exceptions/LabNumberInvalidException.cs` (refactored, ~50 lines)
- **FILE-004**: `src/API/Domain/Exceptions/MetadataNotFoundException.cs` (refactored, ~40 lines)
- **FILE-005**: `src/API/Domain/Exceptions/Hl7GenerationException.cs` (refactored, ~50 lines)
- **FILE-006**: `src/API/LabResultBlobProcessor.cs` (refactored, ~100 lines)
- **FILE-007**: `tests/API.Tests/Domain/Exceptions/LabProcessingExceptionTests.cs` (new test file, ~60 lines)
- **FILE-008**: `tests/API.Tests/Domain/Exceptions/LabNumberInvalidExceptionTests.cs` (new/updated test file, ~50 lines)
- **FILE-009**: `tests/API.Tests/Domain/Exceptions/MetadataNotFoundExceptionTests.cs` (new/updated test file, ~50 lines)
- **FILE-010**: `tests/API.Tests/Domain/Exceptions/Hl7GenerationExceptionTests.cs` (new/updated test file, ~50 lines)
- **FILE-011**: `tests/API.Tests/Domain/Constants/BlobConstantsTests.cs` (new test file, ~30 lines)

## 6. Testing

- **TEST-001**: Unit test `LabProcessingException` base class `IsRetryable` returns false by default.
- **TEST-002**: Unit test `LabProcessingException.BlobName` can be set via object initializer.
- **TEST-003**: Unit test `LabNumberInvalidException` can be caught as `LabProcessingException`.
- **TEST-004**: Unit test `LabNumberInvalidException.IsRetryable` returns false.
- **TEST-005**: Unit test `LabNumberInvalidException.InvalidLabNumber` property retains provided value.
- **TEST-006**: Unit test `MetadataNotFoundException` can be caught as `LabProcessingException`.
- **TEST-007**: Unit test `MetadataNotFoundException.IsRetryable` returns true.
- **TEST-008**: Unit test `MetadataNotFoundException.LabNumber` property retains provided value.
- **TEST-009**: Unit test `Hl7GenerationException` can be caught as `LabProcessingException`.
- **TEST-010**: Unit test `Hl7GenerationException.IsRetryable` returns false.
- **TEST-011**: Unit test `Hl7GenerationException.LabNumber` property retains provided value.
- **TEST-012**: Unit test `BlobConstants.FailedFolderPrefix` equals "Failed/".
- **TEST-013**: Unit test `BlobConstants.LabResultsContainer` equals "lab-results-gateway".
- **TEST-014**: Verify existing code catching specific exceptions (e.g., `catch (LabNumberInvalidException)`) still compiles and works (backward compatibility).

## 7. Risks & Assumptions

- **RISK-001**: Changing exception base classes may affect existing catch blocks in other parts of the codebase; mitigate by verifying no code catches `ArgumentException` or `InvalidOperationException` expecting these specific types.
- **RISK-002**: BlobTrigger string interpolation with constants may have compilation issues; verify Azure Functions SDK supports this syntax.
- **ASSUMPTION-001**: No other code in the solution catches domain exceptions by their original base types (`ArgumentException`, `InvalidOperationException`).
- **ASSUMPTION-002**: The `IsRetryable` classification (metadata = retryable, others = not) aligns with business requirements.
- **ASSUMPTION-003**: Unit tests can be added without modifying test project structure.

## 8. Related Specifications / Further Reading

- [Refactor PoisonQueueRetryProcessor Plan](./completed/refactor-poison-queue-retry-processor-1.md)
- [.NET Exception Best Practices](https://learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions)
- [Azure Functions Blob Trigger](https://learn.microsoft.com/azure/azure-functions/functions-bindings-storage-blob-trigger)
- [DRY Principle](https://en.wikipedia.org/wiki/Don%27t_repeat_yourself)
- [Single Responsibility Principle](https://en.wikipedia.org/wiki/Single-responsibility_principle)
