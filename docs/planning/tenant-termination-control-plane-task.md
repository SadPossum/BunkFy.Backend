# Tenant Termination Control Plane Task

Status: complete; production activation intentionally disabled pending private
admission evidence and an external replay provider
Date: 2026-07-31

## Goal

Provide one fail-closed, resumable workflow for ending a BunkFy workspace
without treating organization archive, customer-facing archive, or database
row deletion as proof that processing has stopped.

The workflow must:

- freeze new tenant processing and access before destructive work;
- optionally produce a protected final export before destruction;
- revoke join sources, credentials, memberships, and scoped authorization;
- let every authoritative owner apply its own legal holds, deletion,
  anonymisation, minimum-proof retention, and restore behavior;
- remain blocked until every mandatory owner supplies exact durable proof;
- keep ordinary API requests, adapter ingress, scheduled work, and retries from
  recreating terminated data; and
- expose bounded operational status without personal data.

Repository defaults keep production execution disabled until a deployment
supplies the admitted owner catalogue, protected replay provider, key material,
and private approval, backup, restore-drill, and operator-assurance evidence.

## Ownership

### Data Rights

Data Rights coordinates the termination case and owns:

- controller or tenant-owner request, approval, and executor separation;
- the immutable termination intent and policy-evidence digest;
- phase transitions, owner work ordering, retries, and bounded status;
- the mandatory owner catalogue and exact contributor contract versions;
- PII-minimised owner results and terminal completion proof;
- protected replay deltas and pre-readiness restore reconciliation; and
- the operator-facing view of blocked, overdue, failed, and completed work.

Data Rights never reads another module's tables and never receives removed
values, record identifiers, credentials, property names, or free text.

### Workspaces

Workspaces owns the BunkFy workspace-processing fence and access closure:

- disable or revoke invitations, enrollment links, and pending claims;
- prevent new Staff onboarding, property activation, and adapter connection
  provisioning;
- close ordinary member access and product access-profile assignments;
- retain only the narrowly authorised owner path needed to review, export,
  confirm, or cancel before irreversible execution;
- close that final path when destruction starts; and
- project a durable termination epoch so late messages cannot reopen ordinary
  processing.

GMA Organizations remains authoritative for generic organization and
membership lifecycle. GMA Access Control remains authoritative for scoped
assignments. Workspaces composes those generic capabilities; it does not copy
their persistence.

### Owner modules

Each owner module owns:

- tenant-local discovery and bounded progress;
- exact legal-hold and policy evaluation;
- deletion, anonymisation, redaction, credential revocation, or minimum-proof
  retention for its records;
- idempotency, checkpoints, receipts, tombstones, and local restore replay;
- a local termination fence or epoch check wherever late internal messages
  could recreate owned state; and
- the stable result codes returned to Data Rights.

The first mandatory BunkFy owner set is:

- Workspaces;
- Properties;
- Inventory;
- Reservations;
- Guests;
- Staff;
- Ingestion;
- Retention; and
- Operations Notifications.

Generic stores composed by the product also require explicit ownership:

- Organizations and Access Control for tenant access state;
- Notifications for tenant notification history;
- Task Runtime for tenant run and control history; and
- Files only when the generic Files module is actually admitted. BunkFy does
  not currently compose that module; product-owned objects stored through
  Framework FileManagement remain with their Ingestion or Data Rights owner.

Auth accounts are global subject-owned identities, not tenant records. Tenant
termination revokes only tenant-scoped access and credentials. It must not
delete a subject's account, other workspace memberships, or global sessions
unless a separate account-rights workflow authorises that action.

### GMA and GMA Extensions

GMA already owns the reusable seams needed by the coordinator:

- `ITenantEndpointAccessPolicy` for HTTP tenant admission;
- `ITaskExecutionContextContributor` for tenant task admission;
- generic organization and membership lifecycle;
- scoped Access Control assignment revocation;
- durable tasks, retries, leases, and recovery; and
- module-owned retention and lifecycle services.

No BunkFy case kind, owner key, hospitality policy, or termination phase belongs
in GMA. A missing generic module cleanup facade may be added to its owning GMA
module only when its request and result remain product-neutral. Cross-GMA
composition belongs in GMA Extensions. The BunkFy coordinator remains here.

## Process Model

Use a dedicated `TenantTerminationProcess` tied one-to-one to an approved,
tenant-scoped `DataRightsCase` of kind `TenantTermination`. The case owns
request, review, and approval. The process begins only after approval and must
not overload subject selection, guest anonymisation work items, or ordinary
record retention.

The product workflow advances monotonically:

1. `FreezePending`
2. `Frozen`
3. `ExportPending` or `ExportReady`
4. `DestructionPending`
5. `Destroying`
6. `Blocked`, `Failed`, or `Completed`

The foundation represents those states as monotonic `Freeze`, optional
`Export`, `Destroy`, `Verify`, and `Completed` phases combined with `Pending`,
`Running`, `Blocked`, `Failed`, or `Completed` status. Every running attempt has
a monotonic operation revision, and stale attempt results cannot advance a
newer retry.

Cancellation uses the Workspaces `Restore` contribution and is allowed only
after freeze has completed and before irreversible destruction starts. A
cancelled pre-destruction process removes the workspace fence through an
explicit compensating transition. Data Rights accepts cancellation only after
the exact durable owner proof shows that the fence was released; there is no
shortcut that marks a process cancelled without that proof.

One active termination process is allowed per tenant. Every transition uses an
expected process version, immutable event id, bounded actor id, and monotonic
timestamp.

## Approval And Assurance

- Only a tenant owner or controller-authorised operator may request
  termination.
- Request, approval, freeze, export download, destruction start, and emergency
  recovery use separate permissions where their risk differs.
- Approval and irreversible execution require configured recent privileged
  authentication assurance.
- The approving actor and destructive executor must be different unless a
  separately configured, audited self-hosted exception is explicitly admitted.
- Production requires a non-secret approval reference and exact policy/catalog
  versions. Repository defaults remain unapproved.
- A final export is opt-in. Export completion never starts destruction
  implicitly; the owner must confirm the frozen export revision.

## Freeze And Admission

The workspace freeze is a durable product fact, not only an API flag.

- A BunkFy `ITenantEndpointAccessPolicy` denies ordinary tenant endpoints while
  a freeze is active.
- Only explicitly annotated termination review, export, cancellation, and
  recovery endpoints may pass.
- A BunkFy `ITaskExecutionContextContributor` denies ordinary tenant tasks and
  permits only exact termination, protected replay, and approved cleanup task
  identities.
- Adapter ingress, connection leases, invitation claims, property activation,
  and other non-HTTP entry points consume the same projected fence.
- Owner modules that can receive late integration events persist and compare a
  termination epoch before recreating ordinary state.
- Unknown or unavailable fence state fails closed for Production mutation
  paths.

The freeze precedes export and destructive work. It does not claim that queues
are drained. Every owner must either prove its pre-freeze messages have
converged or make late handling idempotently respect the termination epoch.

### Slice 3 access-closure boundary

Freeze closes effective access and join/access growth without destroying the
state needed for an exact pre-destruction cancellation:

- Workspaces exposes one tenant-authoritative, fail-closed operational
  admission decision for ordinary processing.
- Staff onboarding submission and provisioning, join-source issuance and
  replacement, access-profile mutation, profile assignment, and role
  assignment consume that decision at their application or owner-module
  boundary. A late integration event cannot bypass it.
- GMA Organizations owns a product-neutral mutation-admission seam for
  organization governance and join-source mutations that do not pass through
  tenant endpoint middleware.
- GMA Access Control owns a product-neutral access-profile-management
  admission seam. Its existing role and profile-assignment policies remain the
  owner seams for assignment growth.
- BunkFy implementations of those generic policies live in the Workspaces
  composition extension and consult only the Workspaces operational-admission
  contract.
- Rejected state is distinct from unavailable state. Unknown tenant identity,
  missing composition, and lookup failure fail closed.

Freeze does not suspend organization memberships or remove Access Control
assignments. The Organizations/Access Control integration intentionally
revokes profile assignments when a membership is suspended, while invitation
and enrollment secrets cannot be restored faithfully. Performing either
mutation before destruction would make the documented cancellation guarantee
false. Physical join-source, membership, and assignment reduction therefore
belongs to the irreversible composed-store stage in delivery slice 6.

The active Workspaces fence is the durable proof of reversible effective
closure. Slice 3 tests must cover generic Organizations routes, generic Access
Control profile and assignment paths, token-scoped onboarding, delayed
onboarding processing, and provider failure. No production termination
endpoint or owner task is activated by this slice.

## Owner Contract

Create a versioned Data Rights Contracts contributor dedicated to tenant
termination. It carries only:

- contract version;
- tenant id;
- process, case, approval, operation, and attempt coordinates;
- termination epoch;
- phase (`Freeze`, `Export`, `Destroy`, `Verify`, or `Restore`);
- immutable policy-evidence digest;
- executing system actor;
- deadline; and
- idempotency key.

An owner descriptor declares:

- stable owner key;
- contract version;
- supported phases;
- ordering dependencies;
- whether it is mandatory for Production; and
- its catalogue version and digest.

An owner result contains only:

- terminal status (`Completed`, `Blocked`, `RetryRequired`, or `Failed`);
- stable result code;
- bounded affected, retained-minimum, and remaining-active counts;
- optional earliest hold-review timestamp;
- selected and resulting owner proof revisions;
- owner catalogue version/digest; and
- completion timestamp.

Data Rights rejects duplicate owners, unknown dependencies, dependency cycles,
unsupported versions, missing mandatory owners, invalid counts, unbounded
codes, stale epochs, and result/catalog mismatches.

## Ordering

The initial dependency graph is:

1. Workspaces freezes processing and join/access growth.
2. Ingestion revokes adapter credentials, leases, and ingress.
3. The optional final export runs against the frozen revision.
4. Reservations, Guests, Staff, Inventory, Properties, Ingestion history, and
   Operations Notifications reduce active owner data.
5. Retention removes or pseudonymises control-plane scope coordinates after
   owner work is terminal.
6. Organizations, Access Control, Notifications, and Task Runtime close
   composed generic tenant state through their owner facades. A Files owner is
   conditional on admitting the generic Files module and is absent from the
   current BunkFy topology.
7. Workspaces closes the final owner path and archives the organization.
8. Data Rights seals protected completion proof and the restore checkpoint.

Independent owners may run concurrently within one stage. The concurrency
limit is bounded. Dependency completion, not registration order, controls
execution.

## Holds And Partial Completion

- An active legal hold blocks only the affected owner's destructive work, but
  the workspace remains frozen.
- A blocked process records no hold reason or record identity, only owner key,
  stable code, and earliest review time.
- An owner may retain an approved non-personal or pseudonymous minimum proof
  and report its count separately from active personal records.
- The process cannot become `Completed` while a mandatory owner is blocked,
  failed, missing, stale, or reports active personal records remaining.
- Operator retry reuses the same process, operation revision, owner work item,
  and idempotency key.

## Restore And Backup Consequences

Database restore safety is part of the public control:

- each irreversible owner result creates a protected replay delta before the
  live transaction may be considered terminal;
- API and Worker readiness remain blocked until every protected termination
  delta newer than the restored checkpoint has been replayed;
- replay re-establishes the workspace fence first, then re-applies owner
  reduction and access closure;
- a backup predating termination converges to the same terminal proof;
- a backup containing the terminal state verifies the existing proof; and
- a restored process can never reopen ordinary tenant access.

Actual backup expiry, object-store version expiry, isolated restore drills, and
RPO/RTO evidence belong to the hosted deployment. Production completion stores
their non-secret evidence references but does not pretend configuration alone
deleted an external backup.

## Efficiency

- Owner work is page-bounded and resumable with owner-local cursors.
- Data Rights stores one small work record per owner and phase, not one record
  per deleted business entity.
- No cross-module record ids enter task payloads, central receipts, logs,
  metrics, or notifications.
- Owner status uses tenant-first indexes and exact process/operation revisions.
- Independent owner phases use bounded parallelism; retries resume only
  incomplete work.
- Completion verification queries owner summaries, never full owner datasets.

## Delivery Slices

1. [x] Add the disabled-by-default Data Rights process aggregate, owner
   descriptors/contracts, persistence, migration, and architecture guards.
2. [x] Add the Workspaces freeze projection plus HTTP/task admission and exact
   termination-route metadata.
3. [x] Add Workspaces access and join-source closure, including GMA
   Organizations/Access Control facades or GMA Extensions composition where a
   generic seam is missing.
4. [x] Add optional tenant export assembly from every mandatory owner against
   one frozen revision.
5. [x] Add owner destruction one domain at a time. Every current product owner
   and composed generic owner now has bounded destruction and terminal proof;
   Workspaces closes the final product path.
6. [x] Add generic composed-store closure for Organizations, Access Control,
   Notifications, and Task Runtime. Files is deliberately not composed; its
   admission boundary is recorded in
   [Files Tenant Termination Boundary](files-tenant-termination-boundary-task.md).
7. [x] Add protected replay, restore-readiness, terminal verification, and
   backup-evidence references.
8. [x] Add API/Admin API/Admin CLI/operator UX with separate permissions,
   assurance, confirmation, status, retry, and rollback boundaries.
9. [x] Add production admission tied to the exact mandatory-owner catalogue and
   keep repository defaults disabled.
10. [x] Run the complete non-Docker gate once and each owner-specific exact
    PostgreSQL/Worker scenario once.
11. [ ] Publish and verify exact release candidates.

## First Implementation Slice

The first slice is intentionally non-destructive:

- persist a termination process and exact owner work descriptors/results;
- define the contributor contract and validate the owner dependency graph;
- reserve process/task identities and stable statuses;
- reject Production enablement because the mandatory owner catalogue is not
  yet complete;
- add no customer-facing endpoint and invoke no owner mutation; and
- prove tenant isolation, optimistic concurrency, retry identity, bounded
  metadata, and contracts-only module references.

This creates a stable coordinator boundary without exposing a partially
implemented "delete workspace" button.

Implemented foundation evidence:

- `TenantTerminationProcess` and one owner work item per
  process/phase/owner/operation revision;
- exact task-run/attempt replay and stale-operation rejection;
- one active process per tenant enforced by a filtered PostgreSQL unique index;
- tenant-safe case and process proof-coordinate foreign keys;
- migration
  `20260731052813_AddTenantTerminationCoordinatorFoundation`;
- contributor dependency/catalog validation and a reserved but unregistered
  `execute-tenant-termination-owner-work` task payload;
- executable personal-data catalogue v17 with PII-free boundary guards; and
- focused domain, privacy, persistence, migration, and architecture checks.

There is deliberately no endpoint, task registration, owner mutation, or
production enablement in this slice.

## Second Implementation Slice

The second slice establishes the Workspaces-owned admission fact without
making tenant termination reachable:

- persist one `WorkspaceTerminationFence` per process and termination epoch;
- enforce at most one active fence per workspace with a tenant-first filtered
  unique index;
- record every accepted transition in an append-only, PII-free receipt;
- expose a contracts-only, scope-aware current-fence reader for composed
  BunkFy adapters;
- deny ordinary tenant HTTP endpoints through
  `ITenantEndpointAccessPolicy`;
- deny ordinary tenant tasks through a host adapter registered after GMA's
  tenant task-context contributor;
- apply the same fence to independently authenticated adapter ingress and
  organization invitation or enrollment admission;
- reserve exact metadata for Data Rights termination review, export,
  cancellation, and recovery routes; and
- keep every exemption absent until its matching endpoint or task exists and
  proves that its process id matches the active fence.

The current-fence read is an indexed `AsNoTracking` database query. The
permissive state is not cached: a stale "open" answer after freeze would be a
correctness defect. A later optimization may cache only active fences, or may
cache open state only after the workflow models a bounded quiescence period.
Unavailable fence state fails closed for admission paths.

The fence state is monotonic for one process:

1. `Frozen`
2. `DestructionStarted`
3. `Closed`

`Released` is an explicit compensating terminal state and is allowed only from
`Frozen`, before destruction starts. A later process uses a new epoch and a
new fence row; prior rows and receipts remain immutable evidence.

This slice was delivered in two internal increments:

1. persist the fence, receipts, reader, contributor, and fail-closed HTTP,
   task, join, and adapter admission;
2. add coordinator cancellation and Workspaces `Restore` contribution so a
   pre-destruction process can become cancelled only after the `Released`
   receipt is durably proven.

Both increments are implemented. Data Rights still does not invoke the
Workspaces contributor, the reserved owner task remains unregistered, no route
receives termination-access metadata, and Production admission remains
rejected. Admin API and Admin CLI termination controls remain part of delivery
slice 8; they cannot activate or bypass the fence in this slice.

Implemented second-slice evidence:

- migration `20260731063610_AddWorkspaceTerminationFence` persists one
  tenant-scoped fence per process and epoch, enforces one active fence, and
  protects historical fences and receipts from relational deletion or
  mutation;
- migration `20260731083019_AddTenantTerminationCancellation` adds the
  `Restore`/`Cancelled` state combination without renumbering existing states;
- exact idempotency receipts, optimistic-concurrency retry, and stable
  coordinate-conflict handling cover concurrent freeze and release attempts;
- scope-aware HTTP, every task-worker composition, independently authenticated
  adapter ingress, invitation or enrollment admission, connection
  provisioning, adapter-run start, and property activation consume the same
  fence;
- only exact process-matching metadata and the reserved Data Rights owner task
  can bypass admission, and neither is attached or registered yet;
- Workspaces personal-data catalogue v7 classifies the fence, receipts,
  commands, projection, and elevated actor attribution; and
- focused domain, persistence, privacy, admission, contributor, cancellation,
  architecture, and host-composition checks pass.

Second-slice verification on 2026-07-31:

- the coherent non-Docker gate passed solution synchronization,
  source-package ownership, a zero-warning all-up build, and migration drift;
- its first architecture pass identified one missing approved security-signal
  code and four unbounded exception-log call shapes; those bounded catalogue
  and logging corrections passed the exact failed tests, followed by the
  remaining 30 host-default and 44 non-Docker integration tests;
- every other module and architecture assembly in the original gate was
  already green, including 244 Workspaces, 286 Data Rights, 254 Ingestion, and
  66 Properties tests; and
- the single Docker-bound
  `TenantTerminationControlPlanePersistenceIntegrationTests` scenario passed
  against PostgreSQL. It proves both migration upgrades, tenant isolation,
  active-fence uniqueness, epoch non-reuse, relational evidence immutability,
  DB-backed Worker admission, and persisted cancellation constraints.

## Third Implementation Slice

The third slice closes access growth and delayed join/onboarding work while
preserving exact pre-destruction cancellation:

- Workspaces owns one contracts-only `IWorkspaceOperationalAdmissionPolicy`
  backed by the authoritative termination fence. Unknown tenant identity,
  mismatched scope, missing composition, and lookup failure fail closed.
- Organizations owns a reusable mutation-admission seam covering owner
  governance, join-source growth, and non-idempotent trusted membership
  restoration. Access-reducing suspension, removal, revocation, and disablement
  remain available.
- Access Control owns a reusable profile-mutation seam covering create, update,
  archive, and non-idempotent ensure. It evaluates once per profile command;
  exact ensure replays remain available. Assignment growth continues through
  the existing role/profile assignment policy seams.
- BunkFy's Workspaces composition extension maps those generic seams to the
  operational-admission contract without leaking termination vocabulary into
  GMA.
- Workspaces rechecks admission at onboarding submission and before each
  external provisioning side effect, join-source issuance/replacement,
  access-plan preparation/activation, bootstrap, profile management, member
  reconciliation, and access restoration.
- Delayed onboarding and restoration stay retryable while restricted.
  Idempotent replays and access-reducing staff suspension/departure remain
  available.
- Legacy role capture during access reduction is read-only. It records a
  restorable profile snapshot when possible and never creates assignments as a
  hidden side effect.

The ordinary authorization path performs no termination-fence lookup per
permission or per target. Profile mutation evaluates once per infrequent
command, while Workspaces orchestration performs one fresh indexed check before
each independently retryable side effect. Physical membership, join-source,
and assignment destruction remains deferred to delivery slice 6 because it
cannot be faithfully reversed after cancellation.

There is still no production termination endpoint, registered owner task, or
Production enablement. This slice adds no persistence model and therefore
requires no migration or Docker-bound proof.

Third-slice focused verification on 2026-07-31:

- the standalone Organizations non-Docker gate passed a zero-warning build,
  both provider migration-drift checks, all 154 tests, and package audit;
- the standalone Access Control non-Docker gate passed architecture checks, a
  zero-warning build, both provider migration-drift checks, all 132 tests, and
  package audit; and
- all 264 Workspaces tests and all 79 Workspaces composition-extension tests
  passed against the exact published GMA commits.

Third-slice coherent backend verification on 2026-07-31:

- solution synchronization and source-package ownership passed after the new
  Access Control task document was added to its module solution;
- the all-up serial build passed with zero warnings and every PostgreSQL and
  GMA dual-provider migration-drift check passed;
- every fast module suite passed, including 132 Access Control, 154
  Organizations, 264 Workspaces, and 79 Workspaces composition-extension
  tests;
- the first architecture pass rejected raw exception objects in the two new
  GMA policy-failure log delegates. Both now log only stable policy/operation
  names and a bounded exception type, with no organization id;
- the exact privacy guard and both affected policy suites passed after that
  correction, followed by all 30 host-default and 44 non-Docker integration
  tests; and
- no Docker suite was run because this slice adds no persistence state or
  provider-specific behavior.

## Fourth Implementation Slice

The notes below preserve the status at each delivery increment. The current
status and remaining deployment evidence are stated at the top of this task.

The fourth slice adds the optional final tenant export without overloading the
existing Guest or Staff subject-rights export. Those exports start from exact
subject coordinates and intentionally omit unrelated product data. A tenant
termination export is instead a controller-authorised, tenant-wide portability
snapshot assembled from every exact owner in the frozen termination catalogue.

One immutable frozen-revision digest binds:

- tenant, process, case, approval, termination epoch, and policy evidence;
- the completed freeze operation and authoritative Workspace fence revision;
- the freeze completion timestamp; and
- the ordered owner keys, contract versions, catalogue versions, and catalogue
  digests selected for the export phase.

The digest is the shared coordinator revision. It does not pretend that
independent module databases have one global row version. Each owner selects a
module-local consistent proof revision, exports against that revision, and
confirms that its resulting revision still matches. A late write, unsupported
projection, unavailable local fence, or revision change makes that owner retry
or block; it cannot silently produce a mixed snapshot.

Export work follows the existing one-owner-per-work-item model:

1. Data Rights prepares one exact `Export` work item per frozen owner.
2. The owner writes a bounded, schema-declared record stream through a Data
   Rights sink and returns only counts plus selected/resulting proof revisions.
3. Data Rights atomically protects and stores one deterministic fragment for
   that work item. Partial or failed owner output is discarded.
4. After every exact owner fragment is complete, Data Rights produces one
   protected manifest/bundle bound to their hashes and the frozen-revision
   digest.
5. Export availability does not advance to destruction. A separate confirmed
   revision is required before the process may enter `Destroy`.

The subject-rights and tenant-export contracts may share bounded JSON record
and sink primitives, encrypted temporary-file handling, key rotation, object
storage, and expiry cleanup. They do not share selection semantics, artifact
lifecycle state, permissions, task payloads, or download assurance. Owner
records remain inside protected export content; central work state, logs,
metrics, task metadata, and notifications contain no business record ids or
personal fields.

Slice 4 is delivered in owner-safe increments:

1. add the dedicated tenant-export contract, deterministic envelope/fragment
   assembly foundation, frozen-catalogue validation, and focused tests;
2. add Data Rights-owned protected fragment and final-artifact lifecycle state;
3. add BunkFy owner exporters one domain at a time, including explicit
   module-local consistency and late-write behavior;
4. add product-neutral GMA owner facades only where the owning generic module
   lacks a bounded export capability, then compose them in BunkFy; and
5. activate neither the owner task nor a production route until every frozen
   mandatory owner, final confirmation, cleanup, and restore consequence is
   proven.

Fourth-slice foundation checkpoint on 2026-07-31:

- Data Rights owns a dedicated tenant-export contributor contract, bounded JSON
  sink, deterministic frozen-revision digest, protected owner-fragment
  lifecycle, and protected final-bundle lifecycle;
- final bundle preparation validates the persisted completed `Export` work
  items and their exact fragments, rather than the currently deployed owner
  catalogue, so deployment drift cannot rewrite an in-flight frozen snapshot;
- fragment execution still validates the frozen catalogue against the live
  versioned exporter and fails closed when that implementation can no longer
  honor the frozen contract;
- subject exports, tenant fragments, and tenant bundles use distinct envelope
  magic, binding domains, derived keys, storage metadata, and opaque storage
  paths while sharing the bounded protection/storage internals;
- migration `AddTenantTerminationExportArtifacts` adds only the export proof,
  fragment, and artifact state. EF reports no pending model changes and the
  generated idempotent PostgreSQL script preserves the intended tenant-scoped
  keys, checks, and owner-work foreign key;
- the Data Rights personal-data catalogue is version 18 and its deterministic
  inventory covers the new contract, transient application, and persisted
  proof fields; and
- all 317 Data Rights tests plus the 27 relevant module-boundary and closed
  output-sink architecture tests pass with zero build warnings.

The first mandatory owner increment adds Workspaces:

- the versioned Workspaces owner catalogue now declares `Export` alongside
  `Freeze` and `Restore`, with one matching tenant-export schema;
- the exporter streams only Workspaces-owned onboarding, access-history,
  restriction, correction, and retention-correlation records in deterministic
  order, reusing the catalogue-approved subject-export field shapes without
  reusing subject-selection semantics;
- a tenant-scoped GMA transaction key serializes relational Workspaces writes,
  fence persistence, and export selection. Ordinary saves recheck the active
  fence under that key, while direct privacy/retention updates acquire it before
  executing SQL, closing the in-flight write race at the owner boundary;
- the export holds that key through one repeatable-read transaction and returns
  the exact unchanged Workspace fence version as its local proof revision; and
- Workspaces catalogue version 8 explicitly authorizes the selected fields for
  either subject export or controller-authorized tenant portability. Derived
  property projections, journals, rebuild state, termination proofs,
  anonymisation tombstones, and generic GMA-owned state remain excluded.
- all 268 Workspaces tests and all 81 architecture guards pass. The existing
  Workspaces PostgreSQL export scenario now also proves every tenant-export
  query shape, tenant isolation, deterministic replay, and that an operational
  write waits behind export before the frozen-fence recheck rejects it; the
  integration project compiles with zero warnings.

The production owner task, API/download route, and expiry cleanup runner remain
disabled. Execution of the staged Docker proof is deferred until the complete
owner slice, as required by the repository's development test cadence.

The second mandatory owner increment adds Properties:

- the versioned `properties` catalogue supports `Export`, depends only on the
  Workspaces owner, and declares five flat streams for authoritative property,
  governance acknowledgement, room, bed, and governance-revision records;
- topology and governance are exported directly from Properties-owned tables.
  Consumer projections, inbox/outbox state, and the internal consistency row
  remain excluded;
- relational Properties writes and export selection share the BunkFy
  tenant-mutation transaction key. Writes re-read the Workspaces fence under
  that key and fail closed when admission is unavailable, while each successful
  unit of work advances one tenant-local monotonic revision;
- export holds the key in a repeatable-read transaction, validates the exact
  process, epoch, and frozen fence before and after streaming, and returns only
  an unchanged local revision as proof;
- migration `AddPropertiesTenantExportRevision` adds the local revision row and
  a PostgreSQL trigger that independently rejects update or deletion of
  governance-revision history; and
- all 76 Properties tests pass, the shared deterministic child-record helper
  has focused UUIDv8 and validation coverage, and the existing Properties
  PostgreSQL scenario compiles with tenant isolation, deterministic replay,
  shared-lock serialization, frozen-write rejection, and raw-SQL append-only
  assertions staged in its existing container.

The Properties container scenario has not been executed yet. It remains folded
into the single deferred owner-slice Docker proof rather than becoming a new
per-edit validation run. The production owner task, API/download route, and
expiry cleanup runner remain disabled.

The third mandatory owner increment adds Inventory:

- the versioned `inventory` catalogue supports `Export`, depends only on
  Properties, and declares 11 deterministic streams for Inventory-owned unit
  identities, room sales configuration, manual blocks, allocations and their
  units, amendment decisions, anonymisation proof, and bed or room retirement;
- replicated property, room, and bed topology; inbox/outbox state; rebuild
  checkpoints; allocation operation locks; and the internal consistency row
  remain excluded;
- relational Inventory writes and export selection share the BunkFy
  tenant-mutation transaction key. Writes re-read the Workspaces fence under
  that key and fail closed when admission is unavailable, while each successful
  unit of work advances one tenant-local monotonic revision;
- export holds the key in a repeatable-read transaction, validates the exact
  process, epoch, and frozen fence before and after streaming, and returns only
  an unchanged local revision as proof;
- migration `AddInventoryTenantExportRevision` adds the local revision row and
  PostgreSQL triggers that independently reject update or deletion of
  anonymisation and restore receipts; and
- all 78 Inventory tests and all 81 architecture guards pass. The integration
  scenario compiles with tenant isolation, deterministic replay, shared-lock
  serialization, frozen-write rejection, and raw-SQL append-only assertions
  staged in PostgreSQL.

The Inventory container scenario has not been executed yet. It remains folded
into the single deferred owner-slice Docker proof. The production owner task,
API/download route, and expiry cleanup runner remain disabled.

The fourth mandatory owner increment adds Reservations:

- the versioned `reservations` catalogue supports `Export`, depends only on
  Inventory, and declares 17 deterministic streams for booking state,
  requested units, pending amendments, Guest links, details history, external
  operations, reminders, data-rights governance, anonymisation, and retention
  proof;
- replicated property, Guest, restriction, and Inventory projections;
  inbox/outbox state; rebuild checkpoints; and the internal consistency row
  remain excluded;
- relational Reservations writes and export selection share the BunkFy
  tenant-mutation transaction key. Writes re-read the Workspaces fence under
  that key and fail closed when admission is unavailable, while each successful
  unit of work advances one tenant-local monotonic revision;
- export holds the key in a repeatable-read transaction, validates the exact
  process, epoch, and frozen fence before and after streaming, and returns only
  an unchanged local revision as proof;
- migration `AddReservationsTenantExportRevision` adds the local revision row
  plus PostgreSQL triggers that independently reject mutation of six receipt
  ledgers and deletion of anonymisation tombstones; and
- all 169 Reservations tests pass. EF reports no pending model changes, and the
  staged PostgreSQL scenario compiles with all 17 streams, tenant isolation,
  deterministic replay, shared-lock serialization, frozen-write rejection,
  and raw-SQL owner-proof protection.

The earlier Reservations export-only container scenario remains staged and was
not rerun during the later destruction slice. The production owner task,
API/download route, and expiry cleanup runner remain disabled.

The Reservations destruction increment is now complete:

- the owner blocks active local legal holds before persisting progress and
  otherwise binds the exact central operation to a local `Open`, `Closing`, or
  `Closed` lifecycle fence;
- each invocation removes at most one non-empty batch of 500 rows, resumes from
  a persisted foreign-key-safe stage, and extends a versioned SHA-256 removal
  chain without exposing row identifiers centrally;
- ordinary writes, projection rebuilds, scoped inbox delivery, scoped message
  creation, and outbox claims cannot repopulate a closing or closed scope;
- completion retains only the closed lifecycle row and one immutable PII-free
  receipt; exact retries replay while changed request coordinates conflict;
- migration `AddReservationsTenantDestructionLifecycle` adds the local state,
  operation, receipt, constraints, and PostgreSQL append-only trigger; and
- the exact PostgreSQL scenario passed with an active-hold blocker, an active
  outbox lease, the 500/1 resume boundary, replay/conflict, raw-SQL receipt
  mutation rejection, post-close admission, and another tenant left intact.

Production execution remains disabled. Cross-owner active-booking preflight,
protected replay, operator controls, and final catalogue-bound production
admission are later control-plane slices.

The fifth mandatory owner increment adds Guests:

- the versioned `guests` catalogue supports `Export`, depends only on
  Reservations, and declares 11 deterministic streams for authoritative Guest
  profiles, correction proof, restriction state and receipts, data holds and
  receipts, anonymisation receipts, tombstones and restore proof, and retention
  execution and anonymisation proof;
- replicated property, stay-history, and effective-restriction projections;
  inbox/outbox state; rebuild and retention sweep checkpoints; operation locks;
  and the internal consistency row remain excluded;
- relational Guests writes and export selection share the BunkFy
  tenant-mutation transaction key. Writes re-read the Workspaces fence under
  that key and fail closed when admission is unavailable, while each successful
  unit of work advances one tenant-local monotonic revision;
- export holds the key in a repeatable-read transaction, validates the exact
  process, epoch, and frozen fence before and after streaming, and returns only
  an unchanged local revision as proof;
- personal-data catalogue version 10 keeps exact subject export and tenant
  portability bindings distinct by their bounded fragment-retention policy;
- migration `AddGuestsTenantExportRevision` adds the local revision row plus
  PostgreSQL triggers that independently reject mutation of six receipt ledgers
  and deletion of anonymisation tombstones; and
- all 134 Guests tests pass. EF reports no pending model changes, and the staged
  PostgreSQL scenario compiles with all 11 streams, tenant isolation,
  deterministic replay, shared-lock serialization, frozen-write rejection,
  and raw-SQL owner-proof protection.

The earlier Guests export-only container scenario remains staged and was not
rerun during the later destruction slice.

The Guests destruction increment is now complete:

- Destroy depends on Reservations, preserving reservation unlinking and
  booking blockers before canonical Guest identity is removed;
- active Guest legal holds block before any local progress, while released
  holds, active/archived/anonymised profiles, stay history, projections,
  message journals, checkpoints, locks, and governance evidence are in scope;
- a persisted `Open`, `Closing`, or `Closed` lifecycle, exact request digest,
  operation row, and SHA-256 removal chain make retries resumable and changed
  coordinates conflicting;
- each call removes at most one non-empty 500-row batch, suppresses scoped
  inbox and projection writes, excludes closing scopes from outbox claims, and
  waits for active outbox leases;
- PostgreSQL receipt and tombstone triggers accept owner deletes only under a
  transaction-local id matching the live operation, scope, request digest, and
  closing state. Updates and ordinary deletes remain prohibited;
- completion retains only the closed lifecycle row and immutable PII-free
  receipt; and
- migration `AddGuestsTenantDestructionLifecycle` is drift-free, all 134
  Guests tests pass, and the exact PostgreSQL 16 scenario proves a dense
  governance graph, active-hold and lease blockers, the 500/1 boundary,
  trigger-authorized deletion, replay/conflict, post-close admission,
  immutable receipt, and cross-tenant isolation.

The production owner task, API/download route, and expiry cleanup runner
remain disabled pending the remaining owner and terminal control-plane slices.

The sixth mandatory owner increment adds Staff:

- the versioned `staff` catalogue supports `Export`, depends only on Guests,
  and declares 14 deterministic streams for authoritative employment profiles,
  property assignment history, correction proof, processing restrictions and
  receipts, employment governance and change proof, data holds and receipts,
  anonymisation receipts, tombstones and restore proof, and retention
  execution and anonymisation proof;
- Auth credentials and sessions, organization membership, roles, grants, and
  effective authorization remain with their GMA owners. Replicated property
  and restriction projections, inbox/outbox state, operation locks, rebuild
  and retention sweep checkpoints, search copies, and the internal consistency
  row remain excluded;
- relational Staff writes and export selection share the BunkFy
  tenant-mutation transaction key. Writes re-read the Workspaces fence under
  that key and fail closed when admission is unavailable, while each successful
  unit of work advances one tenant-local monotonic revision;
- export holds the key in a repeatable-read transaction, validates the exact
  process, epoch, and frozen fence before and after streaming, and returns only
  an unchanged local revision as proof;
- personal-data catalogue version 10 keeps exact Staff subject export and
  tenant portability bindings distinct by their bounded fragment-retention
  policy;
- migration `AddStaffTenantExportRevision` adds the local revision row, extends
  the existing receipt trigger to both later proof ledgers, and prevents
  anonymisation tombstone deletion; and
- all 167 Staff tests pass. EF reports no pending model changes, and the staged
  PostgreSQL scenario compiles with all 14 streams, tenant isolation,
  deterministic replay, shared-lock serialization, frozen-write rejection,
  seven raw-SQL receipt guards, and tombstone deletion protection.

The earlier Staff export-only container scenario was not rerun during the
later destruction slice.

The Staff destruction increment is now complete:

- Destroy depends on Guests, keeping canonical Guest removal ahead of
  employment identity while Auth, Organizations, and Access Control remain
  separate generic owners;
- active Staff legal holds block before progress, while active, suspended,
  departed, and anonymised profiles plus assignments, employment governance,
  projections, journals, checkpoints, locks, and local proof are in scope;
- a persisted `Open`, `Closing`, or `Closed` lifecycle, exact request digest,
  operation row, and SHA-256 removal chain make retries resumable and changed
  coordinates conflicting;
- each call removes at most one non-empty 500-row batch, suppresses scoped
  inbox and projection writes, excludes closing scopes from outbox claims, and
  waits for active outbox leases;
- PostgreSQL receipt and tombstone guards authorize owner deletion only under
  a transaction-local id matching the live operation, scope, digest, and
  closing state. Updates and ordinary deletes remain prohibited;
- completion retains only the closed lifecycle row and immutable PII-free
  receipt; and
- migration `AddStaffTenantDestructionLifecycle` is drift-free, all 167 Staff
  tests pass, and the exact PostgreSQL 16 scenario proves a dense governance
  graph, active-hold and lease blockers, the 500/1 boundary, trigger-authorized
  deletion, replay/conflict, post-close admission, immutable receipt, and
  cross-tenant isolation.

The production owner task, API/download route, and expiry cleanup runner
remain disabled pending the remaining owner and terminal control-plane slices.

The seventh mandatory owner increment adds Ingestion:

- the versioned `ingestion` catalogue supports `Export`, depends only on
  Staff, and declares 15 deterministic streams for adapter connections,
  non-secret ingress credential metadata, tenant ingress control, runs,
  observation evidence metadata, reprocessing attempts and outputs, change
  proposals, reservation source links and dispatches, legal holds, retention
  executions, anonymisation receipts and tombstones, and bounded large-text
  chunks;
- secret references, credential hashes and hash algorithms, raw payload
  bytes, inbox/outbox state, property projections and rebuild checkpoints,
  source-operation locks, global ingress control, anonymisation fingerprints
  and plans, and the internal consistency row remain excluded. Retained raw
  evidence stays available through the separate policy-gated subject-rights
  export rather than being copied into ordinary tenant portability output;
- proposal diffs, source baselines, and dispatch snapshots are represented by
  digest-bound metadata and deterministic 12,000-byte UTF-8 chunks. Subject
  export schema version 2 uses the same bounded representation, fixing the
  previous maximum-value path that could exceed the shared 16 KiB field
  limit;
- relational Ingestion writes and coordinated direct credential mutations
  share the BunkFy tenant-mutation transaction key. Direct mutations require a
  clean EF tracker, writes re-read the Workspaces fence under the key and fail
  closed when admission is unavailable, and every successful operational unit
  advances one tenant-local monotonic revision. Revision rows implement GMA's
  scope entity contract, preventing another tenant from selecting or advancing
  the proof revision;
- export holds the key in a repeatable-read transaction, rejects an active raw
  payload purge, validates the exact process, epoch, and frozen fence before
  and after streaming, and returns only an unchanged local revision as proof;
- personal-data catalogue version 9 keeps subject export and tenant
  portability bindings distinct by their bounded fragment-retention policy;
  and
- migration `AddIngestionTenantExportRevision` adds the local revision row and
  PostgreSQL triggers that independently reject update or deletion of
  anonymisation receipts and deletion of anonymisation tombstones. Legitimate
  tombstone replay updates remain permitted through the domain lifecycle; and
- all 266 Ingestion tests pass, and EF reports no pending model changes.

The Ingestion owner slice completed its staged PostgreSQL proof on 2026-08-02.
It proves all 15 streams, deterministic replay, tenant isolation including
independent revision rows, shared-lock serialization, frozen-write rejection,
coordinated direct credential expiry, legitimate tombstone updates, and
raw-SQL owner-proof protection. The pre-existing protected restore scenario
also passes with an explicit no-fence admission reader.

The runtime gate also exposed and closed a shared composition defect: the
product owner contributors now use distinguishable typed enumerable
registrations, and a non-Docker guard composes all eight current phase and
export owners in one host. Focused model guards prove that every product owner
revision row receives the GMA scope query filter.

Production activation has an additional explicit volume gate. Large fields
are individually bounded, but Data Rights currently protects one fragment per
owner; sufficiently large retained Ingestion history can exceed that fragment
ceiling. The production owner task and route remain disabled until bounded
multi-fragment owner output or an approved hard volume policy is implemented
and tested. Raising the shared limit does not satisfy this gate.

The eighth mandatory owner increment adds Retention:

- the versioned `retention` catalogue supports `Export`, depends only on
  Ingestion, and declares two deterministic streams for authoritative
  retention executions and schedule state;
- rebuildable tenant and property projections, inbox state, and the internal
  consistency row remain excluded. Retention does not copy the business data
  governed by another owner;
- personal-data catalogue version 2 gives the tenant and property coordinates
  exact export bindings and adds the previously missing persistence bindings
  for `RetentionExecution`;
- relational Retention writes and export selection share the BunkFy
  tenant-mutation transaction key. Writes re-read the Workspaces fence under
  that key and fail closed when admission is unavailable, while each successful
  operational unit advances one scope-filtered monotonic revision;
- export holds the key in a repeatable-read transaction, validates the exact
  process, epoch, and frozen fence before and after streaming, and returns only
  an unchanged local revision as proof;
- migration `AddRetentionTenantExportRevision` adds the local revision row.
  No owner-proof trigger is added because Retention currently owns mutable
  control-plane state rather than an append-only proof ledger; and
- all 25 Retention tests pass, the eight-owner composition guard passes, and EF
  reports no pending model changes.

The Retention PostgreSQL scenario passed on 2026-08-02. It proves migration,
two-stream deterministic replay, tenant and revision isolation, shared-lock
serialization, and frozen-write rejection. The production owner task and route
remain disabled; destruction is still a later policy slice.

The ninth mandatory BunkFy owner increment adds Operations Notifications:

- the versioned `operations-notifications` catalogue v3 supports `Export` and
  `Destroy`, depends on Ingestion, and declares 12 deterministic streams for
  the complete tenant-owned GMA Notifications scope;
- the extension owns no database. GMA Notifications remains the persistence and
  product-neutral lifecycle owner, while BunkFy owns the frozen Workspace fence,
  portability schema, personal-data classification, owner dependency, and
  termination result semantics;
- GMA's scope facade selects one monotonic revision and pages user
  notifications, preferences, delivery routes, tag definitions, deliveries,
  attempts, tenant broadcasts and reads, plus exact-reference state and proof.
  Transport inbox rows and platform-global broadcasts are intentionally absent;
- personal-data catalogue version 7 authorizes 37 bounded field identifiers
  across the 12 typed records. Preference data is allowed only on the protected
  tenant-portability surface, while the live notification surface retains its
  stricter no-preference and no-direct-identity policy;
- destruction is bounded to one non-empty stage batch per call. The first
  accepted call closes scope admission, progress is durable and restart-safe,
  active delivery leases or exact-reference close work return `Busy`, and an
  immutable payload-free receipt makes exact replay terminal;
- the BunkFy adapter validates every page, record/store pairing, cursor,
  revision, progress, receipt, owner idempotency key, and matching frozen
  Workspace fence. It reports completion only for the whole generic scope; and
- all 95 Operations Notifications tests, the two-owner composition checks, and
  the closed-output catalogue guard pass. GMA Notifications has 114 passing
  unit tests and drift-free PostgreSQL and SQL Server models. The exact
  resumable scope-destruction PostgreSQL scenario passed against the final
  migration and foreign-key shape on 2026-08-04.

Pre-reference BunkFy history remains covered by the fail-closed retention
production-admission choice, but it no longer limits tenant termination: the
whole-scope lifecycle owns every current notification copy regardless of exact
BunkFy reference. The overall owner task and production route remain disabled
until the remaining generic owner facades and control plane are complete.

The tenth mandatory BunkFy owner increment adds Organizations:

- the versioned `organizations` catalogue supports `Export` and `Destroy`,
  depends on both Operations Notifications and Retention, and declares five
  deterministic streams for the organization, memberships, invitations,
  enrollment links, and enrollment claims;
- the extension owns no database. GMA Organizations remains authoritative for
  the generic scope lifecycle, typed records, revision, closure, bounded
  destruction progress, transport suppression, and immutable receipt. BunkFy
  owns the frozen Workspace fence, portability schema, personal-data
  classification, owner dependency, and termination result semantics;
- the BunkFy tenant coordinate must be the exact lower-case `D` form of the
  GMA organization id and equal the ambient scope. Invitation and enrollment
  token digests are absent from the generic export facade and cannot enter the
  protected fragment;
- export selects one generic revision, validates all five typed stores and
  cursors, and rechecks both revision and frozen fence after streaming.
  Missing scopes and legacy open scopes use exact revision `0`; nullable proof,
  rather than zero, represents absence of terminal proof;
- destruction performs one bounded generic batch per call, preserves durable
  operation-bound progress, retries active transport work and stale revisions,
  and accepts only the exact whole-scope removal receipt; and
- all 16 BunkFy Organizations adapter tests, three owner-composition checks,
  the closed-output catalogue guard, all 320 Data Rights tests, and the 13-test
  Operations Notifications owner regression pass. EF reports no pending Data
  Rights model changes, and the exact PostgreSQL upgrade scenario proves the
  owner and fragment constraints change from revision `>= 1` to `>= 0`.

GMA Organizations' reusable lifecycle was proved separately by its 161-test
suite and exact PostgreSQL scope-destruction scenario. The overall owner task
and production route remain disabled until the remaining generic owner facades
and terminal orchestration are complete.

The eleventh mandatory BunkFy owner increment adds Access Control:

- the versioned `access-control` catalogue supports `Export` and `Destroy`,
  depends on Organizations, and declares four deterministic streams for scoped
  role assignments, profiles, profile assignments, and profile change history;
- the extension owns no database and references GMA Access Control Contracts
  only. GMA owns the product-neutral scope lifecycle, typed records, admission
  closure, bounded destruction, transport suppression, globally orphaned
  principals, and immutable proof. BunkFy owns the Workspace coordinate and
  fence, portability schema, personal-data classification, dependency, and
  termination result semantics;
- export selects one durable scope revision, validates every typed record,
  cursor, store, scope subtree, and profile ownership boundary, then rechecks
  both revision and frozen fence after streaming;
- destruction performs one bounded generic batch per call and accepts only the
  exact scope, operation key, selected revision, resulting revision, progress,
  receipt, proof shape, and matching frozen fence;
- a closed generic snapshot returns its durable selected revision separately
  from its resulting global revision. This preserves exact replay for a missing
  scope selected at revision zero even after unrelated Access Control changes;
  and
- all 140 fast GMA Access Control tests, 16 BunkFy adapter tests, four
  complete-topology composition tests, and 50 focused architecture guards pass.
  Both provider models are drift-free, and the exact PostgreSQL scenario proves
  bounded destruction, principal safety, admission closure, late-message
  suppression, and exact replay.

The twelfth mandatory BunkFy owner increment adds Task Runtime:

- the versioned `task-runtime` catalogue supports `Destroy` only, depends on
  Access Control, and classifies the scoped run and control-message operational
  copies without exposing them through tenant portability;
- the extension owns no database and references GMA Task Runtime Contracts
  only. GMA owns generic `ScopeId` admission, drain, bounded deletion, and
  immutable payload-free proof. Task-owning domains remain authoritative for
  payload meaning and BunkFy owns the Workspace mapping, frozen fence,
  dependency order, and result semantics;
- enqueue, retry, and control-message admission use a shared transaction-scoped
  key lock while lifecycle mutation takes its exclusive counterpart. Closure
  cancels queued work, reports active
  leases as retryable busy work, excludes closing scopes from new claims, then
  removes control messages and terminal runs one non-empty bounded batch per
  call;
- exact replay validates the operation id, selected/resulting revision, batch
  size, counts, proof, timestamps, and frozen fence. Conflicting operation or
  scope reuse fails closed;
- the final Task Runtime owner call must execute outside the terminating
  workspace scope so it cannot delete the run that still needs to record task
  completion; and
- all 27 GMA Task Runtime fast tests, all 1,096 GMA Framework tests, 15 BunkFy
  adapter tests, five owner composition tests, and 28 focused architecture
  guards pass. Both provider
  models are drift-free, and the exact PostgreSQL upgrade scenario proves
  backfill, scoped deduplication, admission and claim fencing, active-lease
  drain, bounded deletion, cross-scope preservation, and replay/conflict.

The overall owner task and production route remain disabled until terminal
orchestration and the remaining product-owner destruction policy are complete.

The Files boundary audit closes the generic-owner catalogue for the current
BunkFy composition:

- `Gma.Modules.Files.Api` remains absent from every BunkFy host, so it owns no
  current BunkFy tenant object and contributes no mandatory owner;
- Framework FileManagement is infrastructure rather than an owner. Ingestion
  and Data Rights retain authority over every current MinIO object and its
  retention or deletion decision;
- a second Files contributor would duplicate lifecycle authority and could
  erase held source evidence or protected termination artifacts prematurely;
- the metadata-free Files front door cannot gain an honest whole-scope
  lifecycle through prefix deletion because it lacks durable object ownership,
  upload fencing, partial-failure recovery, and exact legacy-object proof; and
- architecture guards keep the generic front door absent and make any new
  production `IFileStorage` consumer an explicit review event.

No GMA change is warranted until an actual reusable Files workflow is admitted.
That future slice must add an exact catalogue/admission/lifecycle design inside
GMA Files and provider scenarios, while product retention and legal-hold policy
remain outside the framework.

Before product destruction begins, owner ordering now uses an explicit plan per
phase. The former global dependency list could preserve deterministic export
order only by imposing that same parent-to-child order on destruction. Data
Rights now validates and topologically sorts the selected phase graph, rejects
cross-phase dependencies, and rejects a cycle in any declared phase. Existing
export, freeze, restore, and generic destroy behavior is unchanged; each
product owner will add its destruction plan together with its actual policy and
proof rather than inheriting export dependencies accidentally. All 323 Data
Rights fast tests and the six affected contributor suites pass, and the Worker
composition builds with zero warnings.

The Ingestion product owner now also supports `Destroy` under its phase-specific
Staff dependency. It closes local admission under the exclusive tenant lock,
blocks before progress on active legal holds, drains live outbox leases, and
removes raw payload objects before their database receipts. Object deletion is
outside database transactions and requires an absence read before a separate
SHA-256 object-proof chain advances. The remaining graph is deleted one
foreign-key-safe non-empty batch of at most 500 records per call; derived
reprocessing receipts are removed before their attempts and source receipts.
The deployment-global ingress control is excluded. Completion retains one
closed lifecycle row and one immutable PII-free receipt with independent row
and object proofs. Personal-data catalogue version 10 classifies that retained
minimum under a dedicated termination-proof policy. All 271 fast Ingestion
tests pass, the PostgreSQL model is drift-free, and the exact PostgreSQL 16
scenario proves storage retry, graph ordering, replay/conflict, admission
closure, trigger protection, global-state preservation, and tenant isolation.
Production execution remains disabled pending terminal orchestration,
cross-owner preflight, protected replay, operator controls, and final admission.

The Retention product owner now also supports `Destroy` under its existing
Ingestion dependency. It closes local scheduling, projection, execution, and
inbox admission; excludes closing scopes from global and current schedule
discovery; and removes every Retention-owned control record in deterministic
stages of at most 500 rows per invocation. Completion retains only one closed
lifecycle row and one immutable, PII-free receipt with a versioned SHA-256 row
proof. Personal-data catalogue version 3 binds every retained proof member to a
dedicated tenant-destruction policy, and migration
`AddRetentionTenantDestructionLifecycle` adds the resumable operation and
append-only receipt ledger. All 29 fast Retention tests pass, the model is
drift-free, and the exact PostgreSQL 16 scenario proves freeze drain, bounded
progress, schedule suppression, replay/conflict, trigger protection, closed
admission, and tenant isolation.

The Retention concurrency proof also exposed an efficiency mismatch in the
completed product owners: their ordinary writes still called the legacy
exclusive advisory-lock overload after shared/exclusive framework support had
landed. Reservations, Guests, Staff, Ingestion, and Retention now use shared
tenant admission locks while export and lifecycle transitions remain exclusive.
This preserves the freeze/destruction barrier without serializing unrelated
same-tenant transactions.

The Inventory product owner now also supports `Destroy` under a phase-specific
Reservations dependency while retaining its Properties dependency for export.
It closes operational, projection, checkpoint, inbox, and outbox admission;
waits for active publication leases; and removes all 18 tenant-owned record
families in foreign-key-safe stages. Allocation units are explicitly removed
before allocations so each invocation deletes at most one non-empty batch of
500 physical rows. Completion retains one closed lifecycle row and one
immutable, PII-free receipt with a versioned SHA-256 row-proof chain.
Personal-data catalogue version 3 binds the retained proof to a dedicated
termination policy, and migration `AddInventoryTenantDestructionLifecycle`
adds resumable operation state plus PostgreSQL authorization and immutability
triggers. All 84 fast Inventory tests pass, the model is drift-free, and the
exact PostgreSQL 16 scenario proves lock drain, outbox suppression, bounded
graph removal, replay/conflict, proof protection, closed admission, and tenant
isolation. That provider proof also corrected the copied outbox claim predicate
in Reservations, Guests, Staff, and Ingestion to use mapped lifecycle state.

The Properties product owner now also supports `Destroy` while retaining its
Workspaces export dependency. Its phase-specific destruction plan waits for
Inventory and for the independent Operations Notifications and Retention
branches, transitively covering all current topology consumers without
coupling physical topology to generic Organizations, Access Control, or Task
Runtime owners. Properties closes topology, governance, inbox, and outbox
admission; drains active publication leases; and removes governance revisions,
owned acknowledgements, beds, rooms, and properties in foreign-key-safe stages
of at most 500 physical rows per invocation. Completion retains one closed
lifecycle row and one immutable, PII-free receipt with a chained row proof.
Personal-data catalogue version 3 classifies the retained proof, and migration
`AddPropertiesTenantDestructionLifecycle` authorizes governance-history removal
only for the exact live closing operation. All 82 fast Properties tests pass,
the model is drift-free, and the exact PostgreSQL 16 scenario proves lock
drain, outbox suppression, a `500 + 1` owned-bed split, replay/conflict,
governance and receipt trigger protection, closed admission, and tenant
isolation. That proof also corrected destruction to state explicit tenant
predicates for every source because governance revisions intentionally do not
inherit the ambient scoped-entity filter.

The Workspaces product owner now completes the final product-owned destruction
step under terminal Properties and Task Runtime dependencies. The existing
workspace fence remains the lifecycle authority: the first accepted request
enters `DestructionStarted`, ordinary writes and new message admission stay
closed, and active outbox leases pause progress. Workspaces removes all
remaining operational, projection, governance, anonymisation, transport, and
historical proof rows in foreign-key-safe stages of at most 500 physical rows
per invocation. Completion retains the closed fence, its final close receipt,
and one immutable PII-free destruction receipt with a versioned SHA-256 row
proof. Personal-data catalogue version 9 classifies the retained proof, and
migration `AddWorkspaceTenantDestructionLifecycle` authorizes historical
receipt and fence removal only for the exact live operation. All 272 fast
Workspaces tests pass, the model is drift-free, and the exact PostgreSQL 16
scenario proves shared-lock drain, active-lease pause, outbox suppression,
bounded `500 + 1` projection removal, replay/conflict, trigger protection,
closed admission, and tenant isolation.

## Acceptance

- Two tenants can progress independently with no cross-scope reads or writes.
- Duplicate requests cannot create two active processes for one tenant.
- Freeze is durable and precedes export or destruction.
- Ordinary API, adapter, task, invitation, and property activation paths fail
  closed while frozen.
- Every mandatory owner returns exact durable proof or the process remains
  visibly blocked.
- Active holds pause destruction without leaking hold contents.
- Exact retry and process restart do not duplicate owner work.
- A pre-termination restore replays the fence and every completed owner
  reduction before readiness.
- Final completion proves no mandatory owner reports active personal records,
  access remains closed, and external backup evidence is referenced honestly.
- GMA contains only reusable lifecycle seams; BunkFy policy and owner
  orchestration remain in BunkFy.

Final local verification on 2026-08-05 used
`eng/verify.ps1 -SkipRestore`. Both generated solution graphs and source-package
boundaries passed, the complete solution built with zero warnings and zero
errors, every provider migration reported no pending model changes, and all
4,310 non-Docker tests passed. Exact PostgreSQL, NATS, storage, and Worker
proofs were run once in their owning slices rather than repeated as one costly
catch-all Docker loop. Release-candidate publication and private deployment
admission evidence remain intentionally pending.

## Deferred

- Counsel-approved production termination policy and statutory exceptions.
- Actual hosted backup/object-version expiry and isolated restore evidence.
- Billing, tax, payment, and accounting retention owned by future modules.
- Provider-side deletion where an OTA or other controller requires a remote
  workflow.
- Generalising the coordinator into GMA before a second product proves the
  same abstraction.
