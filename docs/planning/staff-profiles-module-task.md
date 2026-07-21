# Staff Profiles Module Task

Status: privacy hardening implemented and locally verified; exact-commit CI pending
Date: 2026-07-21

## Goal

Build `Staff` as BunkFy's tenant-wide owner of operator employment profiles and property work assignments. The module must give later Housekeeping, Maintenance, approvals, scheduling, reporting, and accounting workflows a stable staff identity without taking ownership of credentials, sessions, roles, or effective permissions from GMA Auth and AccessControl.

The first complete slice must support real management work: create and maintain a staff profile, optionally link it to an Auth user subject, assign and unassign it at properties, suspend or mark the staff member as departed, and query current and historical assignments through public management, Admin API, and Admin CLI surfaces.

## Ownership

Staff owns:

- the canonical `StaffMemberId` used by BunkFy product modules;
- employment-facing profile fields and lifecycle;
- an optional correlation to one GMA Auth user subject;
- current and historical property work assignments;
- actor, reason, timestamp, and version facts for sensitive lifecycle changes;
- Staff-owned projections of property availability.

GMA Auth owns member registration, credentials, login, sessions, enable/disable state, and account recovery. GMA AccessControl owns principals, roles, grants, permission evaluation, and access scopes. Properties owns property identity and lifecycle. There are no cross-module foreign keys or cross-schema writes.

An Auth user can exist without a Staff profile for administration, automation, or setup. A Staff profile can exist without a linked Auth user for pre-provisioning and historical employment records. Linking a user does not grant access. Assigning a property does not grant access. Suspending or departing a staff member does not silently disable Auth or revoke AccessControl grants; coordinated offboarding is a later explicit workflow.

## Profile Model

The initial `StaffMember` aggregate is tenant scoped and has:

- stable `StaffMemberId`;
- required display name and optional legal name;
- optional work email and work phone;
- optional employee number, job title, and department label;
- optional Auth user subject id, unique among Staff profiles in the tenant;
- lifecycle status `Active`, `Suspended`, or `Departed`;
- optimistic version;
- created/last-changed UTC timestamps and authenticated actor provenance.

Employee number, work email, and work phone are not global identities. Employee number may be tenant-unique when present, but contact values must not be used for automatic identity matching. A departed profile remains a durable historical record and cannot be reactivated in the first slice. Suspension is reversible through an explicit resume operation.

The Auth user link may be attached, replaced, or removed through an expected-version command. Because the link cannot grant permissions and Auth intentionally has separate ownership, Staff does not reach into Auth persistence or require synchronous Auth availability. The UI/administrative workflow is responsible for selecting a real user; later orchestration may validate or provision both sides without changing Staff's aggregate boundary.

## Property Assignments

A staff member may work at multiple properties. Assignments are explicit entities with:

- property id;
- assignment id;
- optional property-specific job title;
- `IsPrimary` marker, with at most one current primary assignment per staff member;
- effective start date and optional end date;
- current/inactive state;
- assignment and unassignment actor, reason, timestamp, and aggregate version.

Assigning requires an active Staff profile and an active property in Staff's local Properties projection. Retired properties cannot receive new assignments. Existing assignments remain historical when a property retires, but they stop being eligible as current operational assignments. Unassigning is explicit and idempotent; rows are retained rather than deleted.

The first slice applies assignment, unassignment, and departure transitions immediately while retaining their required effective dates as business facts. It does not interpret future dates as scheduled state changes. Planned assignments and departures require explicit scheduled/active/ended states plus a property business-date policy and are deferred rather than being represented ambiguously by `IsCurrent`.

Property-scoped reads return only staff with a current assignment at that property. Tenant-scoped reads may return the canonical profile and full assignment history to callers with tenant-level permission. Query filters must enforce scope in persistence rather than loading tenant-wide data and filtering in memory.

## Surfaces And Permissions

Public management API, Admin API, and Admin CLI use the same application commands and queries. Initial permissions are operation specific:

- `staff.read` for scoped profile and assignment reads;
- `staff.create` for creating a tenant staff profile;
- `staff.manage` for profile edits and Auth-link changes;
- `staff.assign-properties` for property assignment changes;
- `staff.manage-lifecycle` for suspend, resume, and departure operations.

Create, canonical profile updates, Auth-link changes, and employment lifecycle operations require tenant scope. Property list/get and assignment/unassignment operations require the target property scope. Tenant grants may satisfy descendant property scopes according to the module descriptor; a property grant must not satisfy a tenant operation.

Admin departure and Auth-link replacement/removal are confirmation gated. Lifecycle and unassignment commands require a bounded reason. All write commands use expected aggregate versions.

## Events And Consumers

Staff publishes versioned, PII-minimized integration facts for:

- profile creation and non-sensitive profile changes;
- Auth subject link changes, carrying only subject id and link state;
- staff lifecycle changes;
- property assignment and unassignment.

General integration events must not contain names, email, phone, employee number, free-text notes, or unrestricted reasons. Events carry tenant, staff id, status, property/assignment ids where relevant, effective dates, version, and occurred-at time.

The module consumes Properties created, updated, and retired events into a monotonic local projection. A task-driven rebuild from the Properties export source repairs missed facts. Other product modules consume Staff contracts and own their own projections; they do not read Staff tables.

## Persistence And Security

- PostgreSQL is the first provider migration; domain and application remain provider agnostic.
- Every Staff-owned table is in the `staff` schema and visibly tenant scoped.
- Unique constraints cover tenant plus linked Auth user subject and tenant plus employee number when values are present.
- Aggregate and assignment invariants are also enforced with database constraints where practical.
- Sensitive profile fields never appear in logs, operation names, event subjects, general event payloads, or error messages.
- Staff lifecycle and Auth account lifecycle deliberately remain independent; APIs and docs must not imply otherwise.
- Actor ids, reasons, identifiers, labels, paging, and search input are bounded.
- Historical assignments and departed profiles are not erasure. Retention/anonymization is a later compliance workflow.

## Production Privacy Audit

The production privacy hardening slice is implemented:

- keep `staff.read` as an operational directory permission returning display name, job title, department, status, and only the current assignment facts needed for navigation and notification audiences;
- add a separate `staff.sensitive-profile.read` permission for legal name, work contact details, employee number, Auth subject correlation, lifecycle audit timestamps, and historical assignments;
- keep the current user's full profile available through the identity-bound self-service endpoint without granting tenant-wide sensitive access;
- separate directory and sensitive DTO/query shapes so omitted fields cannot leak through serialization or future mapping changes;
- limit ordinary directory search to display name; a future sensitive search requires the sensitive permission and an explicit management surface;
- add no-store response handling where Staff profile detail contains sensitive data;
- add an executable Staff personal-data catalogue and reflection guards covering persistence, search copies, commands, queries, public/admin requests and responses, cross-module onboarding contracts, domain events, and integration events;
- preserve PII-free integration events and add negative guards for logs, notifications, metrics, traces, and support bundles;
- align Admin API, Admin CLI, typed OpenAPI responses, frontend visibility, and tests with the same boundary.

Directory queries project minimized DTOs directly in persistence and include current assignments only. Sensitive responses use `Cache-Control: no-store`; public management updates require both management and sensitive-profile permissions because they return the full profile. The frontend requests `/profile` only for authorized readers and no longer correlates workspace membership to Staff email or Auth-subject data through the broad directory.

The versioned Staff catalogue currently contains 44 field definitions and 366 concrete bindings. Tests fail when a mapped Staff column, search copy, selected public/admin contract, cross-module request, domain event, or integration event gains an undocumented member. Direct identifiers, contact data, free text, search input, and structured payloads are prohibited from events, notifications, logs, metrics, traces, and support bundles.

Retention, anonymization, staff-data export, and legal-hold policy remain explicit later controls; a narrower read model is not an erasure workflow.

## Verification

- domain tests cover validation, optimistic versions, profile changes, Auth-link uniqueness semantics, lifecycle transitions, primary assignment behavior, and assignment history;
- application tests cover tenant/property scope, unavailable properties, active-status requirements, idempotency, and actor provenance;
- contract tests cover stable statuses, permissions, PII-free events, and subject names;
- persistence tests cover tenant keys, partial unique indexes, constraints, scoped SQL queries, and monotonic property projection updates;
- API authorization tests prove tenant grants, descendant property grants, property-only denial for tenant writes, cross-property denial, and no access escalation from Staff assignment;
- a real PostgreSQL/NATS scenario proves property projection, profile creation, assignment, scoped discovery, suspension/resume, unassignment history, duplicate delivery, and cross-tenant isolation;
- migration upgrade, migration drift, build, non-Docker tests, Docker tests, package audits, and GMA submodule checks pass.

## Deferred

- versioned access-profile seeds, invitation property/profile plans, and Staff-driven offboarding orchestration;
- payroll, compensation, benefits, expenses, and staff accounting;
- shifts, availability, time-off, attendance, and scheduling;
- certifications, training, performance, and disciplinary records;
- emergency contacts, home address, tax identifiers, bank details, and identity documents;
- staff files, contracts, signatures, and retention workflows;
- structured departments, positions, teams, reporting lines, and organization charts;
- approval chains, delegation, temporary elevation, and break-glass workflows;
- bulk imports, HR integrations, merge/split, and employee-number reuse policy;
- automated Auth disablement or AccessControl revocation on suspension/departure;
- future-dated assignment and departure scheduling with property-local business dates.

## Completion Criterion

The slice is complete when Staff is host-composed as an optional BunkFy module, all three management surfaces exercise the same scoped application behavior, PostgreSQL persistence and projection repair are operational, assignments cannot escalate access, events expose no profile PII, and focused plus repository-wide verification is green.

## Implementation Checkpoint

The first slice is implemented in `src/Modules/Staff`. It owns tenant-wide employment profiles, optional unique Auth user-subject correlation, active/suspended/departed lifecycle, retained multi-property assignment history, one-current-primary enforcement, optimistic versions, and actor/reason provenance. Public management API, Admin API, Admin CLI, API/Admin hosts, worker consumers, and projection rebuild tasks use the same application behavior.

PostgreSQL persistence uses visibly tenant-scoped aggregate and assignment keys, PII-minimized outbox events, inbox consumption, and a monotonic local Properties projection. A real PostgreSQL/JetStream authorization scenario proves property-event convergence, tenant versus property scope direction, cross-property denial, lifecycle updates, assignment history, PII-free event payloads, and that linking/assigning a user grants no access. No GMA source changes were required.
