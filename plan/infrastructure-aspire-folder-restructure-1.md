---
goal: Move Aspire AppHost and ServiceDefaults projects from root to src/ folder
version: 1.0
date_created: 2025-11-30
last_updated: 2025-11-30
owner: DevOps/Platform Team
status: 'In progress'
tags: [infrastructure, refactor, aspire, folder-structure]
---

# Introduction

![Status: In progress](https://img.shields.io/badge/status-In%20progress-yellow)

This plan relocates the two Aspire-generated projects (`LabResultsGateway.AppHost` and `LabResultsGateway.ServiceDefaults`) from the repository root to the `src/` folder. This improves project organization by consolidating all source code projects under a single directory, following common .NET solution conventions.

## 1. Requirements & Constraints

- **REQ-001**: Move `LabResultsGateway.AppHost/` folder from root to `src/AppHost/`
- **REQ-002**: Move `LabResultsGateway.ServiceDefaults/` folder from root to `src/ServiceDefaults/`
- **REQ-003**: Update solution file (`LabResultsGateway.sln`) with new project paths
- **REQ-004**: Update all `ProjectReference` paths in affected `.csproj` files
- **REQ-005**: Preserve all project GUIDs to maintain IDE state and configuration
- **REQ-006**: Ensure solution builds successfully after restructure
- **CON-001**: Must not break existing CI/CD pipelines (verify build after changes)
- **CON-002**: Must update relative paths in AppHost.csproj that reference other projects
- **CON-003**: Must update relative paths in API.csproj that references ServiceDefaults
- **GUD-001**: Follow standard .NET solution folder structure conventions
- **PAT-001**: Keep project folder names consistent with existing naming (AppHost, ServiceDefaults)

## 2. Implementation Steps

### Implementation Phase 1: Relocate Project Folders

- GOAL-001: Physically move project folders from root to src/ directory

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Move `LabResultsGateway.AppHost/` to `src/AppHost/` using `Move-Item` or `mv` command | ⏳ | |
| TASK-002 | Move `LabResultsGateway.ServiceDefaults/` to `src/ServiceDefaults/` using `Move-Item` or `mv` command | ⏳ | |
| TASK-003 | Verify folders exist at new locations: `src/AppHost/` and `src/ServiceDefaults/` | | |

### Implementation Phase 2: Update Solution File

- GOAL-002: Update `LabResultsGateway.sln` to reference projects at new locations

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | Update AppHost project path from `LabResultsGateway.AppHost\LabResultsGateway.AppHost.csproj` to `src\AppHost\LabResultsGateway.AppHost.csproj` | ✅ | 2025-11-30 |
| TASK-005 | Update ServiceDefaults project path from `LabResultsGateway.ServiceDefaults\LabResultsGateway.ServiceDefaults.csproj` to `src\ServiceDefaults\LabResultsGateway.ServiceDefaults.csproj` | ✅ | 2025-11-30 |
| TASK-006 | Add both projects to the `src` solution folder (GUID: `{827E0CD3-B72D-47B6-A68D-7590B98EB39B}`) in NestedProjects section | ✅ | 2025-11-30 |

### Implementation Phase 3: Update Project References in AppHost.csproj

- GOAL-003: Fix relative paths in AppHost.csproj after folder relocation

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-007 | Update API project reference from `..\src\API\LabResultsGateway.API.csproj` to `..\API\LabResultsGateway.API.csproj` | ✅ | 2025-11-30 |
| TASK-008 | Update Client project reference from `..\src\Client\LabResultsGateway.Client.csproj` to `..\Client\LabResultsGateway.Client.csproj` | ✅ | 2025-11-30 |

### Implementation Phase 4: Update Project References in API.csproj

- GOAL-004: Fix relative path to ServiceDefaults after folder relocation

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-009 | Update ServiceDefaults reference from `..\..\LabResultsGateway.ServiceDefaults\LabResultsGateway.ServiceDefaults.csproj` to `..\ServiceDefaults\LabResultsGateway.ServiceDefaults.csproj` | ✅ | 2025-11-30 |

### Implementation Phase 5: Validation

- GOAL-005: Verify solution builds and all references resolve correctly

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-010 | Run `dotnet build LabResultsGateway.sln` and verify all projects compile successfully | | |
| TASK-011 | Run `dotnet run --project src/AppHost/LabResultsGateway.AppHost.csproj` to verify AppHost starts correctly | | |
| TASK-012 | Verify Aspire dashboard loads and shows all resources | | |

## 3. Alternatives

- **ALT-001**: Keep projects at root level - Rejected because it creates inconsistent folder structure where some projects are in `src/` and others are at root
- **ALT-002**: Rename folders to remove `LabResultsGateway.` prefix during move - Rejected to maintain naming consistency with project files
- **ALT-003**: Use `dotnet sln` commands to remove and re-add projects - Rejected because it would regenerate project GUIDs and lose IDE state

## 4. Dependencies

- **DEP-001**: Git must be available for tracking file moves (preserves history with `git mv`)
- **DEP-002**: .NET SDK installed for build verification
- **DEP-003**: No active processes locking project files (close Visual Studio/Rider/VS Code)

## 5. Files

- **FILE-001**: `LabResultsGateway.sln` - Solution file requiring path updates for AppHost and ServiceDefaults projects
- **FILE-002**: `src/AppHost/LabResultsGateway.AppHost.csproj` - AppHost project file requiring ProjectReference path updates
- **FILE-003**: `src/ServiceDefaults/LabResultsGateway.ServiceDefaults.csproj` - ServiceDefaults project file (no changes required)
- **FILE-004**: `src/API/LabResultsGateway.API.csproj` - API project requiring ServiceDefaults ProjectReference path update
- **FILE-005**: `LabResultsGateway.AppHost/` - Source folder to be moved
- **FILE-006**: `LabResultsGateway.ServiceDefaults/` - Source folder to be moved

## 6. Testing

- **TEST-001**: `dotnet build LabResultsGateway.sln` completes without errors
- **TEST-002**: `dotnet run --project src/AppHost/LabResultsGateway.AppHost.csproj` starts Aspire orchestrator
- **TEST-003**: All projects appear in Visual Studio/VS Code Solution Explorer under correct folders
- **TEST-004**: Aspire dashboard accessible at configured URL after startup
- **TEST-005**: No broken project references in IDE (no red underlines or missing reference warnings)

## 7. Risks & Assumptions

- **RISK-001**: IDE might cache old project paths - Mitigation: Close IDE before changes, delete `.vs/` and `obj/` folders
- **RISK-002**: CI/CD pipelines may reference old paths - Mitigation: Review `.github/workflows/` files after changes
- **ASSUMPTION-001**: Git will track folder moves correctly preserving file history
- **ASSUMPTION-002**: No other projects in the solution reference AppHost or ServiceDefaults directly (besides API→ServiceDefaults)

## 8. Related Specifications / Further Reading

- [.NET Aspire Documentation - Project Structure](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)
- [.NET Solution File Format](https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file)
- [plan/infrastructure-aspire-orchestration-1.md](infrastructure-aspire-orchestration-1.md) - Related Aspire orchestration plan
