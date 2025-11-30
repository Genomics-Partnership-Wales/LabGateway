---
goal: Enable NuGet Central Package Management (CPM) for LabGateway solution
version: 1.0
date_created: 2025-11-28
last_updated: 2025-11-28
owner: Genomics-Partnership-Wales
status: 'Completed'
tags: [feature, upgrade, nuget, cpm, dotnet, architecture]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

This plan details the steps to enable and migrate the LabGateway solution to use NuGet Central Package Management (CPM), ensuring all package versions are managed centrally for consistency, maintainability, and compliance with modern .NET best practices.

## 1. Requirements & Constraints

- **REQ-001**: All NuGet package versions must be managed centrally using Directory.Packages.props.
- **REQ-002**: CPM must be enabled for all projects in the solution.
- **SEC-001**: No package version drift is allowed between projects.
- **CON-001**: Must not break existing build or restore processes.
- **CON-002**: Directory.Packages.props must be placed at the solution root.
- **GUD-001**: Follow official Microsoft documentation for CPM.
- **PAT-001**: Remove all version attributes from <PackageReference> in project files.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Enable CPM and create central package version file

| Task      | Description                                                                                  | Completed | Date       |
|-----------|----------------------------------------------------------------------------------------------|-----------|------------|
| TASK-001  | Add `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` to Directory.Build.props | ✅ | 2025-11-28 |
| TASK-002  | Create Directory.Packages.props at the solution root                                         | ✅ | 2025-11-28 |
| TASK-003  | Add all current package versions to Directory.Packages.props                                 | ✅ | 2025-11-28 |

### Implementation Phase 2

- GOAL-002: Migrate all project files and validate build

| Task      | Description                                                                                  | Completed | Date       |
|-----------|----------------------------------------------------------------------------------------------|-----------|------------|
| TASK-004  | Remove version attributes from all <PackageReference> in .csproj files                       | ✅ | 2025-11-28 |
| TASK-005  | Run `dotnet restore` and `dotnet build` to validate migration                                | ✅ | 2025-11-28 |
| TASK-006  | Update documentation and onboarding guides                                                   | ✅ | 2025-11-28 |

## 3. Alternatives

- **ALT-001**: Continue managing package versions in each project file (not chosen due to risk of version drift and higher maintenance).
- **ALT-002**: Use Directory.Build.props for package versions (not recommended; Directory.Packages.props is the standard for CPM).

## 4. Dependencies

- **DEP-001**: .NET SDK 6.0+ (CPM requires .NET SDK 6.0 or later)
- **DEP-002**: All developers and CI agents must use compatible SDK versions

## 5. Files

- **FILE-001**: Directory.Build.props (enable CPM)
- **FILE-002**: Directory.Packages.props (central package versions)
- **FILE-003**: All .csproj files in the solution (remove version attributes)

## 6. Testing

- **TEST-001**: Run `dotnet restore` and ensure no errors
- **TEST-002**: Run `dotnet build` for all projects and ensure success
- **TEST-003**: Validate that all projects use the centrally defined package versions

## 7. Risks & Assumptions

- **RISK-001**: Missed version attributes in .csproj files could cause build errors
- **RISK-002**: Incompatible SDK versions on developer/CI machines
- **ASSUMPTION-001**: All projects use PackageReference and are compatible with CPM

## 8. Related Specifications / Further Reading

- [Microsoft Docs: Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- [NuGet Blog: Announcing Central Package Management](https://devblogs.microsoft.com/nuget/announcing-central-package-management/)/n ,.m
