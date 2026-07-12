# GMA Access Control And Properties Alignment

Status: complete
Date: 2026-07-10

This task aligns BunkFy with the scope-aware GMA baseline, composes the new persisted AccessControl module, and applies explicit access policies to Properties. It also closes the Properties issues that should be settled before Inventory or Reservations consume its topology.

## Current Findings

The updated GMA source set introduces three distinct layers:

- `Gma.Framework.AccessControl` owns backend-neutral subjects, permissions, scopes, decisions, providers, grant-scope reads, and deny-by-default orchestration.
- `Gma.Modules.AccessControl` owns optional persisted RBAC roles, grants, assignments, PostgreSQL/SQL Server migrations, bootstrap, and admin front doors.
- Product modules own resource meaning, visibility rules, and SQL query predicates. AccessControl must not learn what a BunkFy property, room, bed, department, or reservation means.

The framework also replaces generic tenant-named storage/runtime primitives with neutral scope primitives. Tenancy remains the BunkFy-facing account boundary and provides the active GMA scope through the tenancy/scoping bridge.

The BunkFy solution now builds against the scope-aware GMA source set. Source roots, solution structure, copied examples, Properties, hosts, tests, scripts, and provider migrations have been aligned with the current framework contracts.

## Architecture Decisions

- Auth identifies a subject. AccessControl decides whether that subject has a permission in a scope. Properties decides what property data that grant exposes.
- Code depends on permission codes, never role names. Roles remain operator configuration.
- BunkFy guests never authenticate into the PMS. Normal product requests still use `AccessSubjectKind.User`; admin API/CLI authorization uses `AccessSubjectKind.AdminActor`.
- Keep the subject kinds separate even when they share the same Auth member id. Product access must not accidentally grant admin-surface access.
- Use hierarchical scopes: `tenant:<tenant-id>` and `tenant:<tenant-id>/property:<property-id>`.
- A tenant grant may cover descendant property scopes only when the permission descriptor explicitly enables ancestor matching. A property grant remains limited to that property.
- Do not place permissions in JWTs as the source of truth and do not cache allow/deny decisions until revocation/invalidation semantics exist.
- Do not authorize once per returned row. Read granted scopes once, translate them to a typed Properties visibility scope, and push that scope into the module-owned SQL query.
- Admin API/CLI remain tenant-scoped operational surfaces. The normal management API supports property-scoped staff access.
- Keep Properties domain/application code PostgreSQL-agnostic. Provider-specific migrations remain in the PostgreSQL migrations project.

## Completed GMA Follow-Ups

The GMA access-control work completed the three generic capabilities BunkFy required:

1. Role assignments target an explicit supported subject kind and subject id.
2. Assignments and grants can be listed and removed, with last-owner protection.
3. Permission descriptors drive scope matching consistently for point authorization and grant-scope reads.

These changes belong in GMA because Auth, Files, Inventory, Reservations, Staff, and future projects can reuse them. Preserve the existing efficient query shape: prefilter subject, permission, and plausible scopes in SQL, then apply bounded scope semantics in memory.

Required GMA tests:

- user and admin-actor assignments stay distinct;
- assignment/grant removal takes effect immediately;
- exact, ancestor, global, malformed, and cross-tenant scope cases deny or allow as declared;
- grant-scope reads return the same matching semantics as point authorization;
- EF queries remain translated and bounded;
- concurrent first-owner bootstrap still permits only one winner.

## Phase 1: Restore The BunkFy Baseline (Implemented)

1. Wire `GmaModuleAccessControlRoot` through `Directory.Build.props`, source-root examples/local bootstrap, migration tooling, package checks, and GMA validation scripts.
2. Refresh `BunkFy.slnx`: remove the deleted Authorization project; add Permissions, Scoping, AccessControl, tenancy access-control, production HTTP, and AccessControl module/admin/migration projects in the correct solution folders.
3. Migrate BunkFy-owned code from generic tenant primitives to scope primitives:
   - `ScopedAggregateRoot`, `ScopedEntity`, and `ScopedDomainEvent`;
   - `ScopeAwareDbContext` and scope conventions;
   - `[ScopeAware]`/`ScopedIntegrationEvent` where a contract only needs isolation;
   - `PermissionScopeRequirement.Scoped` metadata;
   - `AuthProfile.ScopeAware()`.
4. Keep BunkFy contract language tenant-shaped where it is a product fact. The active `ScopeId` is the tenant id in Properties, and contract/event fields may continue to say `TenantId` deliberately.
5. The copied Catalog, Ordering, and TaskSamples examples were removed once the BunkFy product modules provided equivalent composition and testing patterns.
6. Update architecture guards for the new source projects, paths, profile names, and module metadata APIs.
7. Restore, build, run focused package tests, Properties tests, and architecture tests before continuing.

## Phase 2: Align Useful Skeleton Hardening (Baseline Implemented)

The API hosts now compose production HTTP hardening, concrete host/options defaults, dependency-backed readiness, and the notification realtime adapter. Deployment workflows, production file inspection, Linux CI, dependency automation, and source-set release mechanics remain operational follow-ups rather than Properties blockers.

Adopt the application-facing practices added after the BunkFy fork, adjusted to BunkFy's PostgreSQL/MinIO composition:

- compose `AddGmaProductionHttp()`/`UseGmaProductionHttp()` in API hosts;
- configure concrete allowed hosts, forwarded-header trust, HTTPS/HSTS, security headers, CORS, request timeouts, rate limits, and private-network enforcement for Admin API;
- split `/alive` from dependency-backed `/health` and register readiness for every composed DbContext, including AccessControl and Properties;
- apply Auth hardening migrations and keep key rings/pepper rotation, password blocklist, login throttling, refresh reuse revocation, and concurrency behavior explicit;
- set file content inspection off only in local development, require a real fail-closed inspector before production file uploads;
- choose notification preference and retention defaults explicitly and verify the retention service only runs where intended;
- preserve outbox backlog metrics/readiness and bounded worker behavior;
- validate on Windows and Linux, pin GitHub Actions by commit, run Docker tests on relevant changes and a schedule, and add dependency update automation;
- add a source-set manifest/release workflow before the first deployable release, not as a blocker for Properties.

Do not blindly copy Skeleton defaults: BunkFy remains PostgreSQL-first, uses MinIO, has a real Properties module, and should not reintroduce sample product modules.

## Phase 3: Compose AccessControl (Implemented)

Public API host:

- explicitly select `AccessControlProfiles.Default`;
- register AccessControl application and persistence;
- register GMA ASP.NET access control plus the tenant scope resolver;
- reference the PostgreSQL AccessControl migration assembly;
- add AccessControl and Properties database readiness checks.

Admin API/CLI:

- compose `AccessControlAdminApiModule` and `AccessControlAdminCliModule` alongside Administration audit storage;
- let their explicit GMA bridge replace the deny-all admin authorization service;
- keep bootstrap CLI-only;
- apply both the Administration no-op RBAC ownership migration and the AccessControl schema/bootstrap migrations;
- if a database contains old `admin` RBAC rows, migrate or deliberately discard them before switching. The GMA migration preserves legacy tables but does not copy their data into the `access` schema.

Bootstrap sequence for BunkFy development:

1. Bootstrap one global admin-actor owner.
2. Create product roles and grant Properties permission codes.
3. Assign product roles to `User` subjects at tenant or property scope.
4. Assign admin-surface roles to `AdminActor` subjects separately.

## Properties Permission Matrix

| Operation | Permission | Requested scope |
| --- | --- | --- |
| List visible properties | `properties.read` | grant scopes translated into a tenant/all-or-property-id query scope |
| Get a property | `properties.read` | tenant + property |
| Create a property | `properties.properties.manage` | tenant |
| Update/retire a property | `properties.properties.manage` | tenant + property |
| List/get rooms and beds | `properties.read` | tenant + owning property |
| Create/update/retire a room | `properties.rooms.manage` | tenant + owning property |
| Add/update/retire a bed | `properties.beds.manage` | tenant + owning property |

Keep the existing codes unless a product distinction proves necessary. In particular, the same manage permission can be assigned at tenant scope for portfolio administrators or property scope for local managers.

## Properties Policy Implementation (Implemented)

1. Room/bed management routes are explicitly property-nested, for example `/api/properties/{propertyId}/rooms/{roomId}/beds/{bedId}`. Commands/queries include `PropertyId` and verify the parent relation so authorization scope and loaded data cannot drift.
2. Add a Properties HTTP scope resolver that combines the active tenant with the route property id. Do not query Properties merely to build this scope.
3. Apply explicit permission metadata to every normal management endpoint. Authentication and tenant resolution alone are not authorization.
4. Keep Admin API/CLI on the shared admin executor using the same typed Properties permissions. Their checks remain tenant-scoped and audited.
5. For property listing, resolve the `User` subject and call `IAccessGrantScopeReader` once for `properties.read`.
6. Translate grants into a module-owned value such as `PropertiesVisibilityScope` with either all properties in the tenant or a bounded set of property ids.
7. Pass that typed scope to `IPropertiesReadRepository` and generate one filtered SQL query. No per-property access service calls and no unfiltered materialization.
8. Deny when no applicable grant exists. Return filtered results when at least one property grant exists. Avoid revealing whether an unauthorized property id exists.
9. Keep command authorization at each front door. Internal/worker callers must choose and document their subject/service authorization path rather than inheriting ambient HTTP state.

## Properties Audit Follow-Ups

The lifecycle, versioning, and immutable-cursor slice specified in [Properties Topology Lifecycle Task](properties-topology-lifecycle-task.md) is implemented and verified.

Completed before Inventory or Reservations consumes topology:

- Added a property retirement lifecycle and `PropertyRetired` integration event. Retired topology remains addressable for history but rejects setup mutations.
- Added explicit confirmed room-to-bed retirement cascading, with bed events emitted before the room event.
- Added monotonic property, room, and bed versions to DTOs, integration events, and topology exports, with optimistic concurrency on owning aggregates.
- Replaced mutable-code rebuild paging with an immutable generated property ordinal and removed provider-specific runtime SQL.
- Added expected-version preconditions across the public API, Admin API, and Admin CLI.

Remaining before operator-heavy workflows need them:

- Decide how concurrent unique-key violations become stable 409 errors. The current pre-checks are useful but cannot close races by themselves.
- Add stable display order for rooms/beds before an operator UI or Inventory depends on ordering. This can still wait if no consumer needs it in the next slice.

Useful later, but not required for the policy slice:

- property address/contact/display metadata;
- richer property settings;
- role templates or Staff profile integration;
- actor-facing business audit projections beyond shared admin audit.

Keep outside Properties:

- availability, holds, allocations, closures, and overbooking rules;
- housekeeping or maintenance workflow;
- guest data;
- provider ids, adapter state, and import checkpoints;
- hard-coded roles or a product relationship graph inside GMA.

## Verification

Completed verification includes the BunkFy solution build, focused GMA/package suites, Properties and architecture tests, migration drift checks, source-package checks, and the complete 22-test Docker suite. Properties has real-PostgreSQL coverage for the data-preserving lifecycle migration, immutable rebuild ordering, stale-write rejection, real Auth tokens, persisted grants, tenant/property isolation, revocation, tenant-scoped outbox envelopes, and composed-host export resume.

Fast gates:

- all package-local GMA solutions build/test at their pinned commits;
- `dotnet build BunkFy.slnx -m:1` passes;
- Properties unit/contract/persistence tests pass;
- architecture and module-boundary tests pass;
- migration drift checks include AccessControl and Properties PostgreSQL migrations.

Completed product-specific Docker coverage:

- Administration, AccessControl, Auth, Notifications, and Properties migrations apply to clean PostgreSQL databases.
- Owner bootstrap and role create/grant/assign/revoke flows run through the real CLI/API with audit verification.
- Real Auth tokens cover unauthenticated, ungranted, wrong-tenant, tenant-granted, and exact-property Properties requests.
- Property lists use the SQL-filtered visibility path, and exact-property grants return only granted rows.
- Cross-tenant and cross-property identifiers cannot read or mutate another scope.
- Property retirement persists a tenant-scoped outbox envelope, and topology export resumes through the composed host with retired descendants intact.
- MinIO/file and worker scenarios pass in the same Docker suite.

## Completion Criteria

This task is complete when BunkFy builds against the updated GMA source set, AccessControl is an explicit persisted dependency, every Properties management operation is fail-closed under the documented permission/scope matrix, property-scoped list reads are efficient, the required topology hardening is complete, and focused plus Docker-backed verification is green.

Inventory may now become the next active product module. Keep later Properties enhancements in the follow-up notes and preserve the one-active-module rule.
