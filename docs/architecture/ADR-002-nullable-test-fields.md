# ADR-002: Handling Nullable Reference Type Warnings in Integration Test Classes

## Status

Accepted

## Context

In our .NET 10.0 project, we have enabled nullable reference types (NRT) to improve code safety and reduce null reference exceptions. During builds, CS8618 warnings are raised for non-nullable fields in test classes (e.g., `PoisonQueueRetryProcessorIntegrationTests.cs`) that are not initialized in the constructor. These fields, such as `_azuriteContainer`, `_serviceProvider`, `_options`, and `_messageQueueServiceMock`, are typically set up in asynchronous lifecycle methods (e.g., `InitializeAsync` in xUnit's `IAsyncLifetime`) or test methods, which the compiler cannot statically verify as non-null.

This issue arises in integration tests where dependencies like Testcontainers (for `_azuriteContainer`) require async initialization, leading to deferred assignment. Ignoring these warnings risks runtime null dereferences, while over-suppressing them undermines NRT's benefits.

We need a consistent, team-wide approach to address these warnings without compromising test reliability or code maintainability.

## Decision

We will declare the affected fields as nullable (e.g., `private AzuriteContainer? _azuriteContainer;`) and add explicit null checks before usage via a reusable `EnsureInitialized<T>` helper method. This approach balances safety with the flexibility required for test class initialization.

### Implementation Pattern

```csharp
// Nullable field declarations
private AzuriteContainer? _azuriteContainer;
private IServiceProvider? _serviceProvider;

// Reusable helper method with CallerArgumentExpression for meaningful error messages
private static T EnsureInitialized<T>(
    [NotNull] T? field,
    [CallerArgumentExpression(nameof(field))] string? fieldName = null) where T : class
{
    return field ?? throw new InvalidOperationException(
        $"Test setup failed: {fieldName} was not initialized. Ensure InitializeAsync() completed successfully.");
}

// Usage in test methods
[Fact]
public async Task MyTest()
{
    var serviceProvider = EnsureInitialized(_serviceProvider);
    var queueClient = serviceProvider.GetRequiredService<IAzureQueueClient>();
    // ...
}
```

### Rationale

- **Safety First**: Nullable declarations make nullability explicit, preventing accidental null access and aligning with NRT principles.
- **Test-Specific Flexibility**: Integration tests often involve async setup (e.g., starting containers), where immediate non-null initialization isn't feasible. Nullable fields allow this without compiler warnings.
- **Minimal Disruption**: Requires only type changes and null guards, avoiding major refactors like constructor injection.
- **Consistency**: Applies uniformly across test classes, reducing cognitive load for developers.
- **Future-Proofing**: As .NET evolves, this supports better tooling for null analysis.
- **Meaningful Errors**: The `CallerArgumentExpression` attribute provides clear error messages identifying which field failed initialization.

## Consequences

### Positive

- Eliminates CS8618 warnings without suppression.
- Improves code readability by explicitly handling potential nulls.
- Encourages defensive programming in tests, catching setup failures early.
- Aligns with modern C# best practices for NRT.
- Reusable helper method reduces boilerplate across test classes.

### Negative

- Adds minor boilerplate (null checks) in test methods.
- May require updates to existing tests if null guards expose unhandled cases.
- Slight performance overhead from null checks (negligible in tests).

### Risks

- If null checks are omitted, runtime exceptions could occur - mitigate with code reviews.
- Overuse in non-test code could lead to lax null handling; restrict to test classes.

## Alternatives Considered

1. **Use Null-Forgiving Operator (`null!`)**
   - Initialize fields with `null!` in the constructor.
   - **Pros**: Quick, keeps non-nullable types.
   - **Cons**: Suppresses warnings without fixing root cause; risky if initialization fails. Not recommended for long-term maintainability.
   - **Rejected**: Undermines NRT's intent; better for edge cases, not standard.

2. **Add 'required' Modifier**
   - Declare fields as `required` and initialize via object initializers.
   - **Pros**: Enforces non-null at creation.
   - **Cons**: Inflexible for async setup; requires test class refactoring.
   - **Rejected**: Not suitable for dynamic test lifecycles; overkill for internal fields.

3. **Refactor to Constructor Injection**
   - Move initialization to constructor with dependency injection.
   - **Pros**: Ensures non-null, promotes SOLID principles.
   - **Cons**: Significant changes to test structure; async constructors needed for containers.
   - **Rejected**: High effort for minimal gain; tests prioritize simplicity over purity.

4. **Disable NRT for Test Projects**
   - Use `<Nullable>disable</Nullable>` in test project files.
   - **Pros**: Eliminates all warnings.
   - **Cons**: Loses NRT benefits in tests, where null issues are common.
   - **Rejected**: Reduces overall code quality; not aligned with project standards.

## Implementation Notes

- Update field declarations in affected test classes (e.g., `PoisonQueueRetryProcessorIntegrationTests.cs`).
- Add `EnsureInitialized<T>` helper method to each test class (or consider a shared base class).
- Use `[NotNull]` attribute from `System.Diagnostics.CodeAnalysis` for flow analysis.
- Use `[CallerArgumentExpression]` from `System.Runtime.CompilerServices` for meaningful error messages.
- Run full test suite post-change to verify no regressions.
- Document in team wiki; enforce via PR reviews.

## References

- [C# Nullable Reference Types Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references)
- [Testcontainers Documentation](https://dotnet.testcontainers.org/)
- [CallerArgumentExpression Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callerargumentexpressionattribute)
- Project Build Logs (CS8618 warnings)
