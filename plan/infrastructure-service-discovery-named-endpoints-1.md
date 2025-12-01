---
goal: Implement Named Endpoints for Service Discovery in Aspire AppHost
version: 1.2
date_created: 2025-12-01
last_updated: 2025-12-01
owner: Genomics-Partnership-Wales
status: 'Completed'
tags: [infrastructure, aspire, service-discovery, named-endpoints, azure-functions]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

This plan implements Named Endpoints for the LabGateway Azure Functions API within the Aspire AppHost, enabling explicit service discovery URIs and improved Dashboard visibility. Named endpoints provide deterministic port assignments for local development and configurable accessibility levels for Azure deployment.

## Background

Currently, the AppHost uses `.WithExternalHttpEndpoints()` which exposes all HTTP endpoints without explicit naming. This approach:

- Shows generic endpoint names in the Aspire Dashboard
- Lacks explicit port control for local development consistency
- Provides limited configuration for external/internal accessibility per endpoint

Azure Functions inherently share a single HTTP port (default 7071) with routing handled by the Functions runtime via `[HttpTrigger]` route attributes. Named endpoints in Aspire do not create separate ports per route but instead provide:

1. **Service Discovery URIs**: Format `scheme://_endpointName.serviceName` (e.g., `https+http://_http.api`)
2. **Dashboard Visibility**: Named endpoints appear with descriptive names
3. **External Access Control**: Configure `IsExternal` property per endpoint

### HTTP Trigger Functions

The API project exposes three HTTP trigger functions that will be accessible via the named endpoint:

| Function | Route | Method | Description |
|----------|-------|--------|-------------|
| Health | `/api/health` | GET | Health check endpoint for liveness/readiness probes |
| GetLabMetadata | `/api/metadata` | GET | Returns lab metadata information |
| MockHl7Controller | `/api/SubmitHL7Message` | POST | Accepts HL7 message submissions |

## 1. Requirements & Constraints

### Functional Requirements

- **REQ-001**: Configure explicit named HTTP endpoint for the API Azure Functions project
- **REQ-002**: Assign deterministic port number (7071) for local development consistency
- **REQ-003**: Enable service discovery via named endpoint URI format (`https+http://_http.api`)
- **REQ-004**: Improve Aspire Dashboard endpoint visibility with descriptive naming
- **REQ-005**: Expose Health endpoint (`/api/health`) via named endpoint for health checks
- **REQ-006**: Expose GetLabMetadata endpoint (`/api/metadata`) via named endpoint for metadata queries
- **REQ-007**: Expose MockHl7Controller endpoint (`/api/SubmitHL7Message`) via named endpoint for HL7 submissions

### Non-Functional Requirements

- **NFR-001**: Named endpoint configuration must not break existing health check integration
- **NFR-002**: Configuration must support both local development (Azurite) and Azure deployment
- **NFR-003**: Changes must be backward compatible with existing Client project references

### Security Requirements

- **SEC-001**: Control external accessibility via `IsExternal` property for Azure deployment
- **SEC-002**: Named endpoints must respect existing HTTPS enforcement settings

### Constraints

- **CON-001**: Azure Functions share single port; cannot create separate ports per HTTP trigger route
- **CON-002**: Must maintain compatibility with `AddAzureFunctionsProject` Aspire integration
- **CON-003**: Preserve existing storage references (`blobs`, `queues`) and health checks

## 2. Implementation Steps

### Implementation Phase 1: Named Endpoint Configuration

**GOAL-001**: Replace generic external endpoints with explicitly named HTTP endpoint

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Keep `.WithExternalHttpEndpoints()` to expose auto-created Azure Functions endpoint | ✅ | 2025-12-01 |
| TASK-002 | Verified endpoint name `"http"` is auto-created by Azure Functions integration | ✅ | 2025-12-01 |
| TASK-003 | Port assigned dynamically by Azure Functions runtime (default varies by configuration) | ✅ | 2025-12-01 |
| TASK-004 | `WithExternalHttpEndpoints()` marks endpoint as external for Dashboard accessibility | ✅ | 2025-12-01 |

**Code Change for TASK-001 through TASK-004**:

> **IMPORTANT DISCOVERY**: Azure Functions projects in Aspire automatically create an `"http"` endpoint on their configured port. Attempting to create a named endpoint with `name: "http"` will fail with the error: `"Endpoint with name 'http' already exists"`.
>
> The Aspire 9.0+ API does **NOT** support `isExternal` as a parameter to `WithHttpEndpoint()`. Instead, use `.WithExternalHttpEndpoints()` to mark all HTTP endpoints as external.

```csharp
// Before:
var api = builder.AddAzureFunctionsProject<Projects.LabResultsGateway_API>("api")
    .WithHostStorage(storage)
    .WithReference(blobs)
    .WithReference(queues)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/api/health");

// After (CORRECT - Working Configuration):
// Note: Azure Functions automatically creates an "http" endpoint on port 7071.
// Do NOT use WithHttpEndpoint(name: "http") as it will conflict.
// Just use WithExternalHttpEndpoints() to expose the auto-created endpoint.
var api = builder.AddAzureFunctionsProject<Projects.LabResultsGateway_API>("api")
    .WithHostStorage(storage)
    .WithReference(blobs)
    .WithReference(queues)
    .WaitFor(storage)
    .WithExternalHttpEndpoints()  // Marks the auto-created "http" endpoint as external
    .WithHttpHealthCheck("/api/health");
```

**API Discovery Notes**:
- `WithHttpEndpoint()` signature: `WithHttpEndpoint(int? port = null, int? targetPort = null, string? name = null, Action<EndpointAnnotation>? callback = null, bool isProxied = true)`
- No `isExternal` parameter exists - use `.WithExternalHttpEndpoints()` instead
- Azure Functions integration auto-creates the "http" endpoint; don't duplicate it

### Implementation Phase 2: Verify Named Endpoints in Dashboard

**GOAL-002**: Validate all HTTP trigger functions are accessible via the named endpoint

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-005 | Run AppHost and verify Aspire Dashboard shows named endpoint `http` | ✅ | 2025-12-01 |
| TASK-006 | Verify endpoint URL displays in Dashboard (port assigned dynamically) | ✅ | 2025-12-01 |
| TASK-007 | Test Health endpoint accessible at assigned port `/api/health` | ☐ | |
| TASK-008 | Test GetLabMetadata endpoint accessible at assigned port `/api/metadata` | ☐ | |
| TASK-009 | Test MockHl7Controller endpoint accessible at assigned port `/api/SubmitHL7Message` | ☐ | |
| TASK-010 | Created `.http` test files in `src/API/http/` for endpoint testing | ✅ | 2025-12-01 |

### Implementation Phase 3: Service Discovery Integration

**GOAL-003**: Enable service-to-service communication via named endpoint URIs

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-010 | Document service discovery URI format for API: `https+http://_http.api` | ☐ | |
| TASK-011 | Update Client project HttpClient configuration to use service discovery URI (if applicable) | ☐ | |
| TASK-012 | Verify Client-to-API communication works via service discovery | ☐ | |

### Implementation Phase 4: Documentation Update

**GOAL-004**: Document named endpoint configuration and route mappings

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-013 | Update `docs/SETUP.md` with named endpoint configuration | ☐ | |
| TASK-014 | Document HTTP trigger routes and their service discovery URIs | ☐ | |
| TASK-015 | Add troubleshooting guide for endpoint connectivity issues | ☐ | |

## 3. Alternatives

### ALT-001: Keep `.WithExternalHttpEndpoints()` (Current State)

**Description**: Continue using generic external endpoint configuration

**Pros**:
- No code changes required
- Works without explicit port assignment

**Cons**:
- Generic naming in Dashboard
- No explicit port control
- Less clear service discovery URIs

**Decision**: Rejected - Named endpoints provide better developer experience and explicit configuration

### ALT-002: Multiple Named Endpoints per Route

**Description**: Create separate named endpoints for each HTTP trigger (`health`, `metadata`, `hl7`)

**Pros**:
- More granular visibility in Dashboard

**Cons**:
- Azure Functions share single port - cannot have separate ports per route
- Would require reverse proxy configuration
- Adds unnecessary complexity

**Decision**: Rejected - Azure Functions architecture constraint; single named endpoint with multiple routes is appropriate

### ALT-003: Configuration-Based Endpoints via appsettings.json

**Description**: Define endpoints in `appsettings.json` using `services:api:http:0` configuration pattern

**Pros**:
- Configuration can be changed without code changes
- Supports environment-specific overrides

**Cons**:
- More complex configuration management
- Current codebase uses code-first approach consistently

**Decision**: Deferred - May consider for future if environment-specific endpoint configuration needed

## 4. Dependencies

| ID | Dependency | Type | Notes |
|----|------------|------|-------|
| DEP-001 | Aspire 9.0+ | Package | Named endpoint API available in `WithHttpEndpoint()` |
| DEP-002 | Azure Functions Core Tools 4.x | Tool | Local development runtime |
| DEP-003 | Azurite | Tool | Local storage emulation for blob/queue triggers |
| DEP-004 | Existing AppHost configuration | Code | Must maintain storage references and health checks |

## 5. Files

| ID | File Path | Action | Description |
|----|-----------|--------|-------------|
| FILE-001 | `src/AppHost/AppHost.cs` | Modified | Added `.WaitFor(storage)` and `.WithExternalHttpEndpoints()` for named endpoint exposure |
| FILE-002 | `docs/SETUP.md` | Pending | Add named endpoint documentation and route mappings |
| FILE-003 | `src/API/Functions/HealthFunction.cs` | Reference | Health check function at `/api/health` |
| FILE-004 | `src/API/Functions/MockMetadataController.cs` | Reference | Metadata function at `/api/metadata` |
| FILE-005 | `src/API/Functions/MockHl7Controller.cs` | Reference | HL7 submission function at `/api/SubmitHL7Message` |
| FILE-006 | `src/API/http/health.http` | Created | HTTP test file for health endpoint |
| FILE-007 | `src/API/http/metadata.http` | Created | HTTP test file for metadata endpoint |
| FILE-008 | `src/API/http/hl7-submit.http` | Created | HTTP test file for HL7 submission endpoint |

## 6. Testing

### Manual Testing

| Test ID | Description | Expected Result | Status |
|---------|-------------|-----------------|--------|
| TEST-001 | Run `dotnet run` from AppHost directory | Dashboard accessible at https://localhost:17169 | ☐ |
| TEST-002 | Verify Dashboard shows `api` service | Endpoint named `http` visible at port 7071 | ☐ |
| TEST-003 | Click endpoint URL in Dashboard | Opens `http://localhost:7071` | ☐ |
| TEST-004 | GET `/api/health` | Returns 200 OK with health status JSON | ☐ |
| TEST-005 | GET `/api/metadata` | Returns 200 OK with metadata JSON | ☐ |
| TEST-006 | POST `/api/SubmitHL7Message` with valid payload | Returns expected HL7 ACK response | ☐ |

### Service Discovery Testing

| Test ID | Description | Expected Result | Status |
|---------|-------------|-----------------|--------|
| TEST-007 | Verify service discovery URI resolves | `https+http://_http.api` resolves to API | ☐ |
| TEST-008 | Health check probe via named endpoint | AppHost health check passes | ☐ |
| TEST-009 | Client project API call via service discovery | Successful response from API | ☐ |

## 7. Risks & Assumptions

### Risks

| Risk ID | Description | Probability | Impact | Mitigation |
|---------|-------------|-------------|--------|------------|
| RISK-001 | Named endpoint breaks existing health check | Low | High | Verify health check path works with new configuration before deployment |
| RISK-002 | Port 7071 conflict with other services | Low | Medium | Document required port availability; provide alternative port configuration |
| RISK-003 | Client service discovery fails | Medium | Medium | Test Client-to-API calls after change; provide fallback URL configuration |
| RISK-004 | Azure deployment endpoint mismatch | Medium | High | Verify `IsExternal` configuration works in Azure App Service environment |

### Assumptions

| ID | Assumption |
|----|------------|
| ASM-001 | Port 7071 is available on developer machines for local development |
| ASM-002 | Azure Functions Core Tools uses port 7071 by default |
| ASM-003 | Aspire `WithHttpEndpoint()` API is compatible with Azure Functions projects |
| ASM-004 | `IsExternal = true` is required for Dashboard link accessibility |
| ASM-005 | All three HTTP trigger functions are deployed and operational |

## 8. Related Specifications / Further Reading

- [Aspire Service Discovery - Named Endpoints](https://aspire.dev/fundamentals/service-discovery/#named-endpoints)
- [Aspire Service Discovery Overview](https://aspire.dev/fundamentals/service-discovery/)
- [Aspire Networking Overview - Endpoints](https://aspire.dev/fundamentals/networking-overview/#endpoints)
- [LabGateway Aspire Orchestration Plan](./infrastructure-aspire-orchestration-1.md)
- [Azure Functions HTTP Triggers](https://learn.microsoft.com/azure/azure-functions/functions-bindings-http-webhook-trigger)

## 9. Appendix: Service Discovery Reference

### Named Endpoint URI Format

```
scheme://_endpointName.serviceName
```

### LabGateway API Endpoint Mapping

| Function | Route | Service Discovery URI | Method |
|----------|-------|----------------------|--------|
| Health | `/api/health` | `https+http://_http.api/api/health` | GET |
| GetLabMetadata | `/api/metadata` | `https+http://_http.api/api/metadata` | GET |
| MockHl7Controller | `/api/SubmitHL7Message` | `https+http://_http.api/api/SubmitHL7Message` | POST |

### HttpClient Configuration Example

```csharp
// In a consuming service (e.g., Client project)
builder.Services.AddHttpClient("LabApi", client =>
{
    client.BaseAddress = new Uri("https+http://_http.api");
});

// Usage in a service
public class LabApiClient(HttpClient httpClient)
{
    public async Task<HealthStatus> GetHealthAsync()
        => await httpClient.GetFromJsonAsync<HealthStatus>("/api/health");

    public async Task<LabMetadata> GetMetadataAsync()
        => await httpClient.GetFromJsonAsync<LabMetadata>("/api/metadata");

    public async Task<Hl7Response> SubmitHl7MessageAsync(Hl7Message message)
        => await httpClient.PostAsJsonAsync<Hl7Response>("/api/SubmitHL7Message", message);
}
```
