# Tenant Termination Control Plane Task

Status: coordinator foundation implemented; production execution disabled
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

No production endpoint is enabled until the mandatory owner catalogue,
recovery behavior, and production admission contract are complete.

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
- Files for any tenant objects that exist when a file-backed feature is
  admitted.

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
6. Organizations, Access Control, Notifications, Task Runtime, and Files close
   generic tenant state through their owner facades.
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
4. [ ] Add optional tenant export assembly from every mandatory owner against
   one frozen revision.
5. [ ] Add owner destruction one domain at a time: Reservations, Guests,
   Staff, Ingestion, Inventory, Properties, Retention, and Operations
   Notifications.
6. [ ] Add generic composed-store closure for Organizations, Access Control,
   Notifications, Task Runtime, and Files.
7. [ ] Add protected replay, restore-readiness, terminal verification, and
   backup-evidence references.
8. [ ] Add API/Admin API/Admin CLI/operator UX with separate permissions,
   assurance, confirmation, status, retry, and rollback boundaries.
9. [ ] Add production admission tied to the exact mandatory-owner catalogue and
   keep repository defaults disabled.
10. [ ] Run the complete non-Docker gate once, the exact PostgreSQL/Worker
    scenario once, then publish and verify exact candidates.

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

## Deferred

- Counsel-approved production termination policy and statutory exceptions.
- Actual hosted backup/object-version expiry and isolated restore evidence.
- Billing, tax, payment, and accounting retention owned by future modules.
- Provider-side deletion where an OTA or other controller requires a remote
  workflow.
- Generalising the coordinator into GMA before a second product proves the
  same abstraction.
