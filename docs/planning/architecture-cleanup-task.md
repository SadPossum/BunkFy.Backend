# Backend Architecture Cleanup Task

Status: complete
Date: 2026-07-12

## Goal

Bring the BunkFy backend composition and product modules back into a consistent production shape before another domain is opened. This slice repairs solution discoverability, enforces Contracts-only cross-module dependencies, brands BunkFy-owned module assemblies consistently, removes skeleton examples, and reduces oversized files where the current shape obscures ownership or behavior.

## Confirmed Findings

- `BunkFy.slnx` is valid XML and all listed project paths exist, but the root `BunkFy.Workspace.slnx` contains only the old root AppHost and ServiceDefaults projects rather than the real full-stack workspace.
- `BunkFy.Adapters.Configuration` references `BunkFy.Modules.Ingestion.Application` for a runtime material-resolver port and application errors. Adapter code is outside the Ingestion module and must consume an Ingestion contract instead.
- the backend still carries the GMA Skeleton example modules `Catalog`, `Ordering`, and `TaskSamples` in hosts, tests, scripts, docs, and the solution.
- BunkFy product module assemblies formerly used unbranded names such as `Staff.Application`; the target is `BunkFy.Modules.<Module>.<Layer>` for projects, assemblies, and namespaces.
- the cross-module architecture guard has a hard-coded, stale module list and therefore does not protect Guests, Inventory, Reservations, Staff, or Ingestion.
- several front-door files and aggregates have accumulated multiple independent responsibilities. `StaffModule.cs`, grouped command/query files, `Reservation`, and large Ingestion front doors are the clearest current examples.

## Migration Order

1. Move adapter-facing Ingestion ports and stable errors into `BunkFy.Modules.Ingestion.Contracts`; add a generic guard covering every product module and all non-module source projects.
2. Remove Catalog, Ordering, and TaskSamples from hosts, workers, tests, scripts, docs, and disk.
3. Rename retained product module projects, assemblies, namespaces, project references, migration assembly names, tests, and solution entries to `BunkFy.Modules.*`.
4. Rebuild both backend and root workspace `.slnx` files from authoritative project paths and validate with `dotnet sln`, MSBuild, and a Visual Studio load smoke test.
5. Split Staff routes, contracts, commands, queries, validators, and handlers into focused files. Extract value objects/entities or aggregate collaborators where they own real invariants rather than merely moving methods.
6. Apply the same focused-file rule to the largest Reservations and Ingestion hotspots in bounded follow-up passes, with behavior-preserving tests after each pass.
7. Add lasting project-name, namespace, cross-module-reference, solution-integrity, and source-file-shape guards.

## Compatibility Rules

- Domain error codes, HTTP routes, event subjects, database schemas, table names, and migration ids do not change merely because assemblies are branded.
- Existing migration operations are not regenerated. Generated model type names and snapshots are updated to the renamed CLR namespaces, then checked for drift.
- No GMA source repository is modified for BunkFy branding.
- Hosts may compose module front doors and persistence adapters explicitly. Product code outside a module may reference another product module only through its Contracts or Admin.Contracts assembly.
- File-size guards target hand-written orchestration and domain files; generated EF migration files are excluded.

## Verification

- every `.slnx` parses, contains only existing unique paths, lists successfully through `dotnet sln`, and opens in the installed Visual Studio without project-load errors;
- no retained project or source file references Catalog, Ordering, or TaskSamples;
- every retained product module project and namespace starts with `BunkFy.Modules.`;
- architecture tests discover modules from disk rather than from a hand-maintained module-name list;
- migration drift, fast tests, Docker tests, source-package checks, vulnerable-package checks, and GMA submodule checks pass.

## Implementation Checkpoint

- `eng/update-solutions.ps1` now regenerates the backend solution and, with `-IncludeRootWorkspace`, the root workspace solution from projects on disk. Both current files parse, contain unique existing paths, and list through `dotnet sln`.
- external source projects can reference product modules only through Contracts projects; hosts remain explicit composition roots. Adapter configuration material resolution now lives in Ingestion Contracts.
- Catalog, Ordering, and TaskSamples are removed. Product projects, assemblies, namespaces, references, and migration CLR names use `BunkFy.Modules.*`.
- Staff routes, requests, commands, queries, handlers, validators, events, projectors, DTOs, and aggregate behaviors are split into focused files. Staff profile, actor, and change-reason validation are explicit value objects.
- Reservation is split by guest, details, allocation, amendment, and stay capabilities. Room owns an explicit validated `RoomDefinition`; Guest profile changes are an explicit value object; AdapterConnection separates configuration/checkpoint and remote-lease behavior.
- architecture tests ratchet domain files at 400 lines, enforce focused Staff CQRS/event/contract files, and prevent additions to the known grouped-request backlog.
- GMA `ModuleNameResolver` recognizes the generic `<Brand>.Modules.<Module>.*` assembly convention. This is the only framework source change: it is covered for BunkFy and a second arbitrary brand and preserves `Gma.Framework.*` behavior.

The remaining grouped request files in Guests, Reservations, and especially Ingestion are recorded by the architecture-test allowlist. They should be reduced while those existing modules are touched; they do not block this cleanup or justify opening unrelated refactors in GMA.

Final verification passed with deterministic solution generation; XML/path and `dotnet sln` validation; Visual Studio backend design-time loading and root-workspace opening; a zero-warning 204-project build; all 16 migration drift checks; 1,489 non-Docker tests; 27 Docker tests; source-package ownership, transitive vulnerability, diff, and GMA pointer checks. The framework submodule is intentionally dirty only for the resolver change described above and must be committed in the GMA repository before the parent pointer is finalized.
