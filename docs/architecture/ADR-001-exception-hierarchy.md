# ADR-001: Lab Processing Exception Hierarchy

## Status

**Accepted** - November 2025

## Context

The `LabResultBlobProcessor` Azure Function handles blob-triggered processing of lab result JSON files. The original implementation had several maintainability issues:

1. **DRY Violation**: Four nearly identical `catch` blocks with duplicated error handling logic
2. **Magic Strings**: Hardcoded values like `"Failed/"` and `"lab-results-gateway"` scattered throughout
3. **Flat Exception Structure**: Three domain exceptions (`LabNumberInvalidException`, `MetadataNotFoundException`, `Hl7GenerationException`) with no common base class
4. **No Retry Semantics**: No programmatic way to determine if an exception was retryable

The function needed to:
- Move failed blobs to a "Failed" folder
- Log errors with correlation context
- Determine whether failures should trigger retries

## Decision

We will introduce a unified exception hierarchy with retry semantics:

### Exception Hierarchy

```
System.Exception
└── LabProcessingException (abstract base)
    ├── LabNumberInvalidException (IsRetryable: false)
    ├── MetadataNotFoundException (IsRetryable: true)
    └── Hl7GenerationException (IsRetryable: false)
```

### Key Design Elements

1. **Abstract Base Class**: `LabProcessingException` provides:
   - `BlobName` property (string?, init) for context propagation
   - `IsRetryable` virtual property (default: false) for retry decision logic
   - Standard exception constructors for message, inner exception, and serialization

2. **Retry Semantics**:
   - `MetadataNotFoundException` is retryable (external service may recover)
   - `LabNumberInvalidException` is not retryable (data validation failure)
   - `Hl7GenerationException` is not retryable (transformation logic failure)

3. **Constants Class**: `BlobConstants` centralizes magic strings:
   - `FailedFolderPrefix = "Failed/"`
   - `LabResultsContainer = "lab-results-gateway"`

## Consequences

### Positive

- **Single Catch Block**: Handler catches `LabProcessingException` once, reducing code from 4 catch blocks to 2
- **Polymorphic Retry Logic**: `exception.IsRetryable` enables clean retry decisions without type checking
- **Better Traceability**: `BlobName` property ensures context flows through the exception chain
- **Testability**: Each exception can be unit tested for correct retry semantics
- **Extensibility**: New exception types inherit retry semantics automatically

### Negative

- **Breaking Change**: Existing code catching specific exceptions still works, but new patterns are preferred
- **Learning Curve**: Team must understand the hierarchy and when to use `IsRetryable`

### Neutral

- **No Runtime Impact**: Exception hierarchy has negligible performance impact
- **Serialization**: Base class supports binary serialization for distributed scenarios

## Implementation

### Files Changed

| File | Change |
|------|--------|
| `Domain/Exceptions/LabProcessingException.cs` | NEW - Abstract base class |
| `Domain/Constants/BlobConstants.cs` | NEW - Magic string centralization |
| `Domain/Exceptions/LabNumberInvalidException.cs` | MODIFIED - Inherits from base |
| `Domain/Exceptions/MetadataNotFoundException.cs` | MODIFIED - Inherits from base, `IsRetryable = true` |
| `Domain/Exceptions/Hl7GenerationException.cs` | MODIFIED - Inherits from base |
| `LabResultBlobProcessor.cs` | REFACTORED - Consolidated catch blocks |

### Test Coverage

- 42 new unit tests covering:
  - Exception constructors and serialization
  - `IsRetryable` property values
  - `BlobName` propagation
  - `BlobConstants` values

## Alternatives Considered

### 1. Result Pattern (Result<T, TError>)

**Rejected**: Would require significant refactoring of async methods and break existing integration patterns. Exception-based approach aligns with Azure Functions retry semantics.

### 2. Exception Attributes for Retry

**Rejected**: Attributes require reflection at runtime. Virtual property is simpler and more performant.

### 3. Separate IRetryable Interface

**Rejected**: Adds complexity without benefit. The hierarchy already provides polymorphism.

## References

- [Clean Code: Error Handling](https://www.oreilly.com/library/view/clean-code/9780136083238/) - Robert C. Martin
- [Azure Functions Error Handling](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-error-pages)
- [Exception Best Practices in .NET](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)
