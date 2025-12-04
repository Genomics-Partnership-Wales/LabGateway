# ADR-004: Outbox Pattern Implementation

## Status
Accepted

## Context
The Lab Results Gateway processes lab reports and needs to reliably deliver HL7 messages to external systems. The system must ensure that messages are not lost during processing failures and that delivery can be retried. Traditional approaches of sending messages directly from business logic can lead to inconsistencies if the database transaction succeeds but the message delivery fails.

## Decision
We will implement the Outbox Pattern to ensure reliable message delivery. The pattern involves:

1. **Transactional Storage**: Store messages in the same database transaction as business data
2. **Separate Dispatcher**: Use a background process to dispatch stored messages
3. **Retry Logic**: Implement exponential backoff for failed deliveries
4. **Cleanup**: Automatically remove successfully dispatched messages

### Implementation Details

#### Storage Layer
- **Technology**: Azure Table Storage for outbox persistence
- **Schema**:
  - PartitionKey: CorrelationId (for efficient querying)
  - RowKey: MessageId (unique identifier)
  - MessageType: Type of message (HL7, etc.)
  - MessageData: Serialized message content
  - Status: Pending/Dispatched/Failed
  - CreatedAt: Timestamp
  - DispatchedAt: Timestamp (when successfully sent)
  - FailedAt: Timestamp (when delivery failed)
  - RetryCount: Number of retry attempts
  - NextRetryAt: When to attempt next retry
  - ErrorMessage: Last error encountered

#### Dispatcher Component
- **Trigger**: Azure Functions timer trigger (runs every 30 seconds)
- **Process**:
  1. Query pending messages (Status = "Pending" AND NextRetryAt <= now)
  2. Attempt delivery to external systems
  3. Update status based on success/failure
  4. Implement exponential backoff for retries

#### Queue Service Decorator
- **Pattern**: Decorator pattern wrapping the existing queue service
- **Behavior**: Store message in outbox first, then attempt immediate dispatch
- **Failure Handling**: If dispatch fails, message remains in outbox for retry

### Configuration
```json
{
  "OutboxOptions": {
    "TableName": "OutboxMessages",
    "MaxRetryCount": 5,
    "InitialRetryDelaySeconds": 30,
    "MaxRetryDelaySeconds": 3600,
    "MessageTimeToLiveHours": 168
  }
}
```

## Alternatives Considered

### Option 1: Direct Message Sending
- **Pros**: Simple implementation, immediate delivery
- **Cons**: Risk of message loss if system fails after DB commit but before message send
- **Rejected**: Unacceptable risk for healthcare data delivery

### Option 2: Database Triggers
- **Pros**: Automatic, no additional infrastructure
- **Cons**: Complex to implement, vendor-specific, performance impact
- **Rejected**: Not portable, adds complexity to database layer

### Option 3: Message Queue with Dead Letter Queue
- **Pros**: Built-in retry mechanisms, monitoring
- **Cons**: Additional infrastructure cost, complexity
- **Rejected**: Overkill for current scale, adds operational complexity

### Option 4: Event Sourcing
- **Pros**: Complete audit trail, replay capabilities
- **Cons**: Significant complexity increase, storage requirements
- **Rejected**: Not needed for current requirements

## Consequences

### Positive
- **Reliability**: Guaranteed message delivery even during system failures
- **Consistency**: Messages are stored transactionally with business data
- **Observability**: Clear visibility into message delivery status
- **Scalability**: Background processing doesn't impact main request flow

### Negative
- **Latency**: Messages may not be delivered immediately
- **Complexity**: Additional infrastructure and monitoring requirements
- **Storage**: Additional storage for message persistence
- **Operational**: Need to monitor outbox table and dispatcher health

### Mitigation Strategies
- **Monitoring**: Implement alerts for outbox table size and dispatcher failures
- **Cleanup**: Automatic TTL-based cleanup of old messages
- **Testing**: Comprehensive integration tests with Azurite
- **Documentation**: Clear operational procedures for troubleshooting

## Implementation Timeline
- **Phase 1**: Infrastructure setup (Table Storage, options configuration)
- **Phase 2**: Outbox service implementation
- **Phase 3**: Queue service decorator
- **Phase 4**: Dispatcher function
- **Phase 5**: Integration and testing
- **Phase 6**: Documentation and monitoring

## Related ADRs
- ADR-005: Domain Events Pattern
- ADR-001: Exception Hierarchy
- ADR-002: Nullable Test Fields

## References
- [Transactional Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [Azure Table Storage Best Practices](https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-overview)
- [Azure Functions Timer Triggers](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer)
