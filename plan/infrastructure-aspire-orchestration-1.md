---
goal: Add Aspire orchestration and Azure deployment to LabGateway
version: 1.0
date_created: 2025-11-30
last_updated: 2025-11-30
owner: Genomics-Partnership-Wales
status: 'Planned'
tags: [infrastructure, aspire, orchestration, azure, deployment]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

This plan adds .NET Aspire orchestration to the LabGateway solution and prepares the API (Azure Functions) and Client (Blazor) projects for orchestrated local development and secure Azure deployment. It ensures all orchestration, configuration, and deployment steps are explicit, deterministic, and machine-executable.

## 1. Requirements & Constraints

- **REQ-001**: Use Aspire AppHost to orchestrate all solution projects.
- **REQ-002**: Register API (Azure Functions) and Client (Blazor) in AppHost with explicit health checks and references.
- **REQ-003**: All configuration and secrets must use environment variables or Azure App Settings/Key Vault.
- **SEC-001**: No secrets or connection strings may be committed to source control.
- **SEC-002**: Enforce HTTPS and secure headers for all endpoints.
- **CON-001**: Preserve existing solution structure; add only minimal new projects/files.
- **GUD-001**: Follow Azure Functions and Storage best practices (e.g., `AzureWebJobsStorage`).
- **PAT-001**: Use ServiceDefaults for observability and health endpoints.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Initialize Aspire and create AppHost

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Install Aspire CLI and verify with `aspire --version` |  |  |
| TASK-002 | Run `aspire init` at solution root to create `apphost.cs` and `apphost.run.json` |  |  |
| TASK-003 | Confirm AppHost builds and dashboard launches via `aspire run` |  |  |

### Implementation Phase 2

- GOAL-002: Register projects and relationships in AppHost

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | In `apphost.cs`, add `AddProject<Projects.LabResultsGateway_API>("api")` with `.WithHttpHealthCheck("/health")` |  |  |
| TASK-005 | Add `AddProject<Projects.LabResultsGateway_Client>("client")` with `.WithExternalHttpEndpoints()` and `.WithReference(api)` |  |  |
| TASK-006 | Use `.WaitFor(api)` on client to ensure correct startup order |  |  |

### Implementation Phase 3

- GOAL-003: Add ServiceDefaults for observability

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-007 | Create `LabGateway.ServiceDefaults` via `dotnet new aspire-servicedefaults -n LabGateway.ServiceDefaults` |  |  |
| TASK-008 | Add ServiceDefaults project to solution and reference from API and Client |  |  |
| TASK-009 | Update `Program.cs` in API and Client to call `builder.AddServiceDefaults(); app.MapDefaultEndpoints();` |  |  |
| TASK-010 | Verify Aspire dashboard shows health/traces for all services |  |  |

### Implementation Phase 4

- GOAL-004: Configure Azure Storage and Functions settings

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-011 | Create Azure Resource Group, Storage Account, and Function App |  |  |
| TASK-012 | Set Function App setting `AzureWebJobsStorage` to Storage Account connection string |  |  |
| TASK-013 | Add Blob containers and queues as required; record names in app settings |  |  |
| TASK-014 | Ensure `src/API/local.settings.json` uses `UseDevelopmentStorage=true` for local dev |  |  |

### Implementation Phase 5

- GOAL-005: Deploy API (Azure Functions) and validate

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | Build and publish API project via `dotnet publish src/API/LabResultsGateway.API.csproj -c Release` |  |  |
| TASK-016 | Deploy to Function App using Azure CLI or Functions Core Tools |  |  |
| TASK-017 | Validate Functions logs, triggers, and blob/queue integration |  |  |

## 3. Alternatives

- **ALT-001**: Use Docker Compose for orchestration; rejected for lack of observability and type safety.
- **ALT-002**: Use manual service startup scripts; rejected as brittle and error-prone.

## 4. Dependencies

- **DEP-001**: .NET SDK 10.0+ and Aspire CLI installed.
- **DEP-002**: Azure CLI or Azure Functions Core Tools for deployment.
- **DEP-003**: API and Client projects must build and run cleanly.

## 5. Files

- **FILE-001**: `apphost.cs` — AppHost orchestration code.
- **FILE-002**: `apphost.run.json` — AppHost configuration.
- **FILE-003**: `src/API/Program.cs` — ServiceDefaults and endpoints.
- **FILE-004**: `src/Client/Program.cs` — ServiceDefaults (optional).
- **FILE-005**: `src/API/local.settings.json` — Local dev settings (no secrets).

## 6. Testing

- **TEST-001**: `aspire run` starts AppHost, dashboard accessible, all resources healthy.
- **TEST-002**: API Functions deploy to Azure; triggers execute; blob ingest pipeline processes sample files.

## 7. Risks & Assumptions

- **RISK-001**: Misconfigured connection strings causing trigger failures.
- **RISK-002**: Missing health endpoints if ServiceDefaults not used.
- **ASSUMPTION-001**: Azure Functions project uses standard `AzureWebJobsStorage`.

## 8. Related Specifications / Further Reading

- <https://aspire.dev/get-started/add-aspire-existing-app/?lang=csharp>
- <https://aspire.dev/fundamentals/service-defaults/>
- <https://learn.microsoft.com/azure/azure-functions/functions-overview>
