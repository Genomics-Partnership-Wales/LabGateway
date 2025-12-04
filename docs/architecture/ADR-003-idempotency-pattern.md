# ADR-003: Idempotency Pattern Implementation

## Status
Accepted

## Context
The Lab Results Gateway processes blob uploads that may be retried due to network issues or client-side failures. Without idempotency, duplicate processing could lead to:
- Duplicate lab results in downstream systems
- Inconsistent data states
- Resource waste from redundant processing

We need a mechanism to ensure that the same blob content is only processed once, even if uploaded multiple times.

## Decision
Implement idempotency using content-based hashing with Azure Table Storage for tracking.

### Key Components:
- **IdempotencyKey**: Value object containing SHA256 hash of blob content and blob name
- **ProcessingRecord**: Entity storing processing state with TTL for automatic cleanup
- **TableStorageIdempotencyService**: Infrastructure service managing idempotency checks
- **Integration**: Check idempotency before processing in LabResultBlobProcessor

### Technical Details:
- Hash algorithm: SHA256 for collision resistance
- Storage: Azure Table Storage with 24-hour TTL
- Key format: {BlobName}_{ContentHash}
- Outcome tracking: Success/Failed states

## Consequences

### Positive:
- Prevents duplicate processing of identical blob content
- Automatic cleanup of old records via TTL
- Minimal performance impact (single table lookup per blob)
- Scalable with Azure Table Storage

### Negative:
- Additional Azure Table Storage costs
- Slight latency increase per blob processing
- Requires content hashing for each blob

### Risks:
- Hash collisions (mitigated by SHA256)
- Table storage outages (fallback to processing without idempotency)
- TTL configuration errors (manual cleanup required)

## Alternatives Considered

### Database-based Idempotency
- Pros: ACID transactions, complex queries
- Cons: Higher complexity, potential bottlenecks
- Rejected: Overkill for simple key-value tracking

### Redis-based Caching
- Pros: Faster lookups, built-in TTL
- Cons: Additional infrastructure dependency
- Rejected: Azure Table Storage sufficient for current scale

### Client-side Deduplication
- Pros: No server-side changes
- Cons: Client responsibility, unreliable
- Rejected: Cannot trust client-side implementation

## Implementation Notes
- Content hash computed in ComputeContentHash method
- Idempotency check occurs before HL7 processing
- Failed processing still marked to prevent retries
- Telemetry added for monitoring hit/miss rates
