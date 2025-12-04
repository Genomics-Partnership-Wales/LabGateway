# ADR-005: Domain Events Pattern Implementation

## Status
Accepted

## Context
The Lab Results Gateway processes complex business workflows involving lab report ingestion, metadata extraction, HL7 message generation, and external system delivery. These workflows involve multiple steps that need to be decoupled and observable. Traditional imperative approaches lead to tight coupling between components and make it difficult to add new features or monitoring.

## Decision
We will implement the Domain Events pattern to enable loose coupling, better testability, and comprehensive observability. The pattern involves:

1. **Event Definition**: Define domain events as immutable data structures
2. **Event Publishing**: Publish events from domain logic
3. **Event Handling**: Multiple handlers can subscribe to events
4. **In-Process Dispatch**: Use dependency injection for handler resolution

### Implementation Details

#### Event Hierarchy
```csharp
public interface IDomainEvent
{
    string CorrelationId { get; }
    DateTimeOffset Timestamp { get; }
}

public abstract class DomainEventBase : IDomainEvent
{
    public string CorrelationId { get; }
    public DateTimeOffset Timestamp { get; }

    protected DomainEventBase(string correlationId)
    {
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        Timestamp = DateTimeOffset.UtcNow;
    }
}
```

#### Specific Events
- **LabReportReceivedEvent**: Fired when a lab report blob is received
- **LabMetadataRetrievedEvent**: Fired when lab metadata is successfully extracted
- **Hl7MessageGeneratedEvent**: Fired when HL7 message is created
- **MessageQueuedEvent**: Fired when message is queued for processing
- **MessageDeliveryFailedEvent**: Fired when external delivery fails
- **MessageDeliveredEvent**: Fired when message is successfully delivered

#### Event Dispatcher
- **Interface**: `IDomainEventDispatcher` with `DispatchAsync<TEvent>(TEvent @event)`
- **Implementation**: `DomainEventDispatcher` using DI container for handler resolution
- **Handler Resolution**: `IEnumerable<IDomainEventHandler<TEvent>>` for multiple handlers per event
- **Exception Handling**: Isolated handler failures don't stop other handlers

#### Event Handlers
- **Audit Logging**: Logs all events for compliance and debugging
- **Telemetry**: Sends metrics to Application Insights
- **Future Extensions**: Easy to add new handlers (notifications, analytics, etc.)

### Configuration
Events are automatically discovered through DI registration:
```csharp
services.AddTransient<IDomainEventHandler<LabReportReceivedEvent>, AuditLoggingEventHandler>();
services.AddTransient<IDomainEventHandler<LabReportReceivedEvent>, TelemetryEventHandler>();
```

## Alternatives Considered

### Option 1: Direct Method Calls
- **Pros**: Simple, immediate execution
- **Cons**: Tight coupling, hard to test, no observability
- **Rejected**: Doesn't support our decoupling and monitoring requirements

### Option 2: MediatR Library
- **Pros**: Mature library, rich features, community support
- **Cons**: Additional dependency, learning curve
- **Rejected**: Want to keep implementation simple and framework-agnostic

### Option 3: Reactive Extensions (Rx.NET)
- **Pros**: Powerful composition and transformation capabilities
- **Cons**: Complex for simple event handling, steep learning curve
- **Rejected**: Overkill for our use case

### Option 4: Event Sourcing
- **Pros**: Complete audit trail, temporal queries
- **Cons**: Significant complexity and storage requirements
- **Rejected**: Not needed for current requirements

## Consequences

### Positive
- **Loose Coupling**: Components don't need to know about each other
- **Testability**: Easy to test event publishing and handling in isolation
- **Observability**: Comprehensive logging and telemetry of business events
- **Extensibility**: New features can subscribe to existing events
- **Audit Trail**: Complete record of business activities

### Negative
- **Complexity**: Additional abstraction layer
- **Debugging**: Event flow can be harder to trace
- **Performance**: Small overhead for event dispatching
- **Testing**: Need to test event publishing and handling separately

### Mitigation Strategies
- **Clear Naming**: Events named after business actions (past tense)
- **Documentation**: Comprehensive event documentation
- **Logging**: Correlation IDs for tracing event flows
- **Testing**: Unit tests for event creation and handler logic
- **Monitoring**: Telemetry for event processing metrics

## Implementation Timeline
- **Phase 1**: Define event interfaces and base classes
- **Phase 2**: Implement specific domain events
- **Phase 3**: Create event dispatcher
- **Phase 4**: Implement audit logging handler
- **Phase 5**: Implement telemetry handler
- **Phase 6**: Integration testing and documentation

## Related ADRs
- ADR-004: Outbox Pattern
- ADR-001: Exception Hierarchy
- ADR-002: Nullable Test Fields

## References
- [Domain Events Pattern](https://martinfowler.com/eaaDev/DomainEvent.html)
- [Event-driven Architecture](https://microservices.io/patterns/data/event-driven-architecture.html)
- [.NET Dependency Injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
