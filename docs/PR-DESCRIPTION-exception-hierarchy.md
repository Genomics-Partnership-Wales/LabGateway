# Pull Request: Refactor LabResultBlobProcessor Exception Handling

## Summary

This PR introduces a unified exception hierarchy with retry semantics for the `LabResultBlobProcessor` Azure Function, addressing DRY violations and improving maintainability.

## Changes

### New Files

- **`Domain/Exceptions/LabProcessingException.cs`** - Abstract base class with `IsRetryable` property and `BlobName` context
- **`Domain/Constants/BlobConstants.cs`** - Centralized magic strings (`FailedFolderPrefix`, `LabResultsContainer`)

### Modified Files

- **`Domain/Exceptions/LabNumberInvalidException.cs`** - Now inherits from `LabProcessingException` (IsRetryable: false)
- **`Domain/Exceptions/MetadataNotFoundException.cs`** - Now inherits from `LabProcessingException` (IsRetryable: true)
- **`Domain/Exceptions/Hl7GenerationException.cs`** - Now inherits from `LabProcessingException` (IsRetryable: false)
- **`LabResultBlobProcessor.cs`** - Consolidated 4 catch blocks → 2, extracted helper methods

### Test Coverage

- **42 new unit tests** across 5 test files covering:
  - Exception constructors and serialization
  - `IsRetryable` property behavior
  - `BlobName` property propagation
  - `BlobConstants` values

## Motivation

The original implementation had:

1. **4 nearly identical catch blocks** violating DRY
2. **Magic strings** like `"Failed/"` hardcoded in multiple places
3. **No common base class** for domain exceptions
4. **No programmatic retry semantics** for exception handling

## Solution

### Exception Hierarchy

```text
System.Exception
└── LabProcessingException (abstract)
    ├── LabNumberInvalidException (not retryable)
    ├── MetadataNotFoundException (retryable)
    └── Hl7GenerationException (not retryable)
```

### Before (4 catch blocks)

```csharp
catch (LabNumberInvalidException ex) { /* duplicate logic */ }
catch (MetadataNotFoundException ex) { /* duplicate logic */ }
catch (Hl7GenerationException ex) { /* duplicate logic */ }
catch (Exception ex) { /* different logic */ }
```

### After (2 catch blocks)

```csharp
catch (LabProcessingException ex)
{
    await HandleLabProcessingExceptionAsync(ex, blobClient, blobName, cancellationToken);
}
catch (Exception ex)
{
    await HandleUnexpectedExceptionAsync(ex, blobClient, blobName, cancellationToken);
}
```

## Testing

All 68 tests pass (42 new + 26 existing):

```bash
dotnet test tests/API.Tests/
```

## Documentation

- **ADR-001**: [Exception Hierarchy Decision](architecture/ADR-001-exception-hierarchy.md)
- **Implementation Plan**: [plan/refactor-lab-result-blob-processor-1.md](../plan/refactor-lab-result-blob-processor-1.md)

## Commits

| Hash | Message |
|------|---------|
| `0b2a718` | refactor(exceptions): add LabProcessingException base class and BlobConstants |
| `5bd18de` | test: add unit tests for exception hierarchy and constants (Phase 4) |
| `620dd61` | docs: mark refactor plan as complete |

## Checklist

- [x] Code follows project conventions
- [x] Unit tests added for new functionality
- [x] All tests pass
- [x] Documentation updated (ADR created)
- [x] No breaking changes to public APIs
- [ ] Integration tests pass (requires Docker - see note)

> **Note**: Integration tests in `PoisonQueueRetryProcessorIntegrationTests.cs` require Docker/Azurite and are skipped in environments without container support.

## Related

- Closes: *Link to issue if applicable*
- ADR: [ADR-001-exception-hierarchy.md](architecture/ADR-001-exception-hierarchy.md)
