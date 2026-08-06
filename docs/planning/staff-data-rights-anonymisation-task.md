# Staff Data Rights Anonymisation Task

Status: operator surface implemented and locally verified
Date: 2026-07-30

## Outcome

Add tenant-scoped, policy-governed anonymisation for one exact Staff profile
without creating a second data-rights control plane or weakening the existing
Guest workflow.

The final workflow must:

- bind the Staff profile to explicit employment-governance evidence;
- block destructive processing while an independently managed hold is active;
- execute only for an exact departed Staff version with no current assignments;
- irreversibly remove Staff-owned profile and search data while preserving the
  minimum non-identifying employment history required by approved policy;
- produce an immutable Staff owner receipt, tombstone, authoritative processing
  ledger entry, and protected external ledger delta;
- deny workspace membership and access synchronously before a restored database
  can expose resurrected Staff data; and
- preserve historical Guest ledger and protected-delta verification.

This is delivered in guarded slices. The prerequisite foundation, scoped case
admission, internal owner mutation, restore safety, and dedicated operator
workflow are complete and locally verified.

## Ownership

Staff owns:

- employment-governance bindings and their immutable change receipts;
- Staff-scoped legal/data holds and their immutable transition receipts;
- Staff mutation eligibility, anonymisation, tombstones, and owner proof;
- the minimum Staff facts retained after anonymisation; and
- serialization of governance, hold, correction, restriction, and
  anonymisation operations for one Staff member.

Data Rights owns:

- case admission, requester verification, approval, expiry, orchestration, and
  central completion;
- scope-aware work items and exactly-one-owner dispatch;
- generic policy-evidence validation;
- the authoritative processing ledger and protected external delta; and
- restore readiness, replay envelopes, and restoration reconciliation.

Workspaces owns organization membership and workspace access. It must implement
a synchronous Staff restore policy that denies the restored subject's BunkFy
workspace access before Staff data is re-scrubbed and before readiness opens.
It does not disable the subject's global Auth account or affect other
workspaces.

The shared BunkFy data-governance library owns country-policy evaluation. GMA
continues to own generic CQRS, tenancy, persistence, outbox, task, security
assurance, and authorization primitives. Employment policy, Staff lifecycle,
BunkFy case types, and cross-module restore coordination are product concepts.
No GMA change is required by the proven design.

## Scope Decision

Staff rights cases are tenant scoped and have no property coordinate. Existing
anonymisation execution, ledger, and restore contracts are property scoped, so
the later execution slice must introduce an explicit scope discriminant rather
than a sentinel property id:

- `GuestRights` remains property scoped;
- `StaffRights` is tenant scoped;
- execution records carry case type and nullable property id;
- persisted ledger snapshots identify their scope kind explicitly; and
- old Guest rows continue to deserialize and verify as property-scoped records.

Data Rights will expose a versioned
`IDataRightsAnonymisationPolicyContributor`. It selects exactly one contributor
by case type, owner, and record type. Staff evaluates its own lifecycle,
governance, retention, and hold state, then returns generic bounded policy
evidence. Data Rights never reads Staff tables or interprets Staff policy
fields.

## Prerequisite Foundation

### Employment Governance

Staff owns one versioned employment-governance binding per Staff member. The
binding records:

- tenant and Staff member coordinates;
- the exact Staff version selected when configured;
- operating country;
- country-policy id, version, and digest;
- data region and transfer profile;
- retention-policy id and version;
- policy effective, expiry, evaluated, and configured timestamps;
- bounded, sorted acknowledgement evidence;
- configured/changed actor provenance; and
- an optimistic governance version.

Configuration is an explicit replace operation, never an implicit inference
from a property assignment. A Staff member may work across properties and
countries, so using a primary or arbitrary property policy would be unsafe.
The model may later move under a legal entity or employment agreement without
changing the Data Rights contributor contract.

Every committed replacement writes an immutable idempotent receipt containing
the exact selected Staff version, previous and resulting governance versions,
policy and acknowledgement digests, actor, timestamp, and a canonical receipt
digest. Equivalent retries return the committed receipt; changed input under
the same idempotency key fails.

The development country-policy pack may include clearly marked example
operations and retention rules for Staff. These are executable development
defaults, not production legal approval.

### Legal And Data Holds

Staff owns tenant-scoped holds keyed to one Staff member. Each hold has:

- a stable hold id;
- a stable bounded reason code, not free-text legal advice;
- active or released state;
- placement and release actor/timestamps; and
- an optimistic hold version.

Holds are independently releasable. Placement and release each create an
immutable idempotent receipt bound to the exact Staff and hold versions.
Active holds block both manual anonymisation and later automatic retention.
Hold management remains available through dedicated data-rights paths even
when ordinary Staff processing is restricted.

### Operation Serialization

Staff provisions exactly one per-member operation lock when a Staff member is
created and backfills one for each legacy member. Every mutation of an existing
Staff aggregate, plus governance changes, hold transitions, correction,
restriction, and later anonymisation, acquires the same lock before selecting
mutable state. Ordinary mutations reselect through the operational restriction
gate; safety-reducing transitions use their explicit bypass. This prevents a
new hold, policy replacement, restriction, or lifecycle transition from racing
an approved exact-version execution.

The lock is an application/persistence port, not a process-local mutex. It must
participate in the same database transaction as the selected operation. Its
relational implementation uses an atomic provider-neutral revision update so
the module persistence project does not embed provider-specific locking SQL.

## Foundation Surfaces

The public management API adds tenant-scoped endpoints for:

- reading and replacing employment governance;
- listing and placing Staff holds; and
- releasing one exact hold with explicit confirmation.

Sensitive responses use `Cache-Control: no-store`. Dedicated permissions are:

- `staff.employment-governance.manage`; and
- `staff.data-holds.manage`.

Governance and hold reads also require `staff.sensitive-profile.read`.
Governance replacement and hold release require the host's configurable
privileged authentication assurance. Hold placement is safety increasing and
does not require elevated assurance.

The permissions are delegable but excluded from the company-support ceiling
and all ordinary manager, front-desk, housekeeping, and viewer seeds. The
workspace owner wildcard continues to satisfy them.

Staff data export includes bounded current governance and current/history hold
facts as soon as those records exist. Subject exports omit operator identities
and internal free-text provenance, matching the current Staff export boundary.
Placement enforces the same 1,000-record per-Staff ceiling used by export.
Unexpected legacy data beyond that bound still fails closed instead of
silently truncating.

## Persistence

The Staff schema adds:

- current employment-governance bindings and owned acknowledgement rows;
- append-only governance change receipts;
- Staff hold rows;
- append-only hold transition receipts; and
- operation-lock rows.

All tables are visibly tenant scoped. Indexes lead with tenant and Staff
coordinates; idempotency keys are unique within their operation boundary.
Optimistic concurrency protects current rows. Receipt tables are covered by
the append-only persistence guard.

Only the PostgreSQL migration project changes. Domain and application code
remain provider agnostic.

## Staff Anonymisation

After the foundation is complete, Data Rights may admit `Anonymisation` for
`StaffRights`. Approval and execution must bind:

- tenant-scoped case and exact approval revision;
- exact Staff, governance, restriction, and hold versions;
- country-policy id/version/digest and governance source revision/digest;
- approved retention trigger, deadline, and policy evidence;
- absence of active holds;
- `Departed` lifecycle state; and
- absence of current property assignments.

Staff anonymisation:

- clears display/legal name, work email, work phone, employee number, job title,
  department, Auth-subject correlation, and normalized search copies;
- clears subject-specific assignment free text when approved policy requires
  it, while retaining minimum property/date/state facts;
- transitions the profile to terminal `Anonymised`;
- excludes the record from all ordinary reads, writes, search, reconciliation,
  and operational audiences;
- preserves only a random Staff id and approved non-identifying employment
  history; and
- emits no profile PII, policy notes, hold reasons, or actor identifiers.

The owner writes an immutable receipt and local tombstone. The receipt may
retain bounded operator attribution required for audit; the tombstone retains
only minimum proof such as receipt digest, authority, completion time, and
ledger/replay coordinates. Audit attribution created by another person is not
rewritten as though it belonged to the data subject.

## Ledger Compatibility

The Data Rights ledger contract will advance only with version-specific
compatibility:

- old ledger versions infer `GuestRights` and property scope;
- the new ledger version stores explicit case/scope coordinates;
- old protected deltas verify against their original serialized MAC shape;
- new deltas use a new canonical payload shape; and
- a full historical-delta restore drill proves both versions before rollout.

The local protected-delta store currently computes its HMAC from serialized
nested delta content. Adding members and reserializing old records would change
those bytes and invalidate valid history. The implementation must therefore
verify the exact stored nested payload or use an explicit legacy serializer for
old contract versions. Permissive fallback verification is prohibited.

## Scope Slice State

The scope slice now implements:

- tenant-scoped Staff anonymisation case admission;
- exactly-one contributor selection by case type, owner, and record type;
- Staff-owned lifecycle, governance, restriction, hold, retention, and
  operation-lock evidence;
- schema-versioned frozen approval evidence with bounded canonical state
  bindings;
- explicit case and scope coordinates on execution batches and work items;
- a version 3 ledger contract for scoped evidence while preserving version 1
  and 2 Guest canonical digests;
- exact-byte verification of historical protected-delta payloads; and
- PostgreSQL promotion and guarded downgrade of existing Guest execution data.

Internal destructive Staff execution is implemented through the version 2
tenant-scoped owner protocol described below. At scope-slice completion, the
public API intentionally had no Staff execution endpoint, so the internal
capability could not be started before its product workflow was approved. The
dedicated operator surface is delivered separately in slice 5.

Version 3 restore is implemented as a separate Staff-scoped protocol. It
resolves the Workspaces prerequisite and Staff owner before replay-envelope
decryption, denies workspace access synchronously, and advances readiness only
after Staff has persisted exact re-scrub proof. Historical Guest restore stays
on its original property-scoped protocol.

## Owner Mutation Slice Contract

The existing Guest owner protocol remains version 1. Its prepared event,
one-shot task payload, owner request, and terminal event keep their original
property-scoped wire shapes so queued Guest work and historical messages do
not change meaning.

Staff uses a parallel version 2 protocol. Every prepared event, task payload,
owner request, and terminal event carries:

- case type and execution-scope kind;
- the tenant id supplied by the scoped message envelope;
- a nullable property coordinate whose shape must agree with the scope; and
- the exact case, work-item, approval, and execution revisions.

Version 2 accepts `StaffRights` only with tenant scope and no property id.
Data Rights persists owner contract version 1 for Guest work items and version
2 for Staff work items. Dispatch resolves exactly one contributor by case
type, owner, record type, and contract version. It never adapts a tenant work
item through the Guest version 1 protocol.

The Staff owner executes as one Staff-module transaction:

1. replay an existing exact idempotency receipt, when present;
2. revalidate the approved operation and full frozen approval evidence;
3. acquire the per-member operation lock, advancing its frozen revision by
   exactly one;
4. reload Staff, governance, restriction, and complete hold state under that
   lock and compare every frozen binding version and digest;
5. require the exact departed Staff version, no current assignment, no active
   hold, and a due retention deadline;
6. scrub profile/search fields, Auth correlation, and assignment free text,
   then transition the aggregate to terminal `Anonymised`;
7. write an immutable owner receipt and local tombstone; and
8. publish a PII-free Staff-anonymised event.

The receipt binds the tenant, idempotency key, case and operation revisions,
selected/resulting Staff versions, pre/post operation-lock revisions, complete
approval digest, frozen-state digest, event id, bounded executor attribution,
completion time, and its own canonical digest. Replay succeeds only when the
receipt, terminal aggregate state, and tombstone all still agree.

The owner mutation and version 2 internal dispatch were implemented before a
public Staff execution endpoint was exposed. Restore coordination can
synchronously deny workspace access before restored Staff data is visible.
The later operator slice preserves this ordering and adds its own authorization
and authentication-assurance proof.

The PostgreSQL owner schema enforces the terminal Staff lifecycle, exact
receipt/tombstone coordinates, tenant-first uniqueness, append-only receipts,
and guarded downgrade. Data Rights separately rejects downgrade once a Staff
work item or any non-version-1 owner work exists.

### Deployment Contract

The scoped-execution migration is intentionally fail closed rather than
rolling-write compatible with an older Data Rights binary. Before applying it:

1. stop Data Rights mutation traffic and pause its worker/restore processing;
2. drain active Data Rights transactions and already-dispatched work;
3. apply the migration;
4. deploy the matching API and worker version together; and
5. complete ledger/readiness checks before resuming traffic.

Do not add compatibility defaults or triggers that fabricate scope or policy
evidence for an old writer. Downgrade is supported only while no schema 2
approval, tenant-scoped execution, or version 3 ledger entry exists; the
migration rejects an unsafe downgrade.

## Restore Safety

A database restored from before anonymisation may resurrect both Staff PII and
workspace access. Readiness remains closed until:

1. Data Rights verifies authoritative ledger and protected-delta history.
2. Workspaces synchronously resolves the trusted Staff-to-subject mapping and
   denies/removes that subject's membership and access in the affected
   workspace.
3. Staff re-applies the tombstone/anonymisation result idempotently.
4. Data Rights records reconciliation proof and opens readiness.

If no trustworthy Workspaces mapping exists, readiness stays closed. An async
event alone is insufficient because it creates an authorization window.

### Version 3 Restore Slice Contract

The existing Guest restore contributor, request, result, and protected replay
envelope remain version 2 and property scoped. Historical version 1 and 2
ledger entries continue through that exact path. They are not adapted into the
new contract.

Version 3 restore is a parallel scoped protocol. Its request carries the
ledger case kind, scope kind, nullable property coordinate, exact owner and
record coordinate, owner receipt, resulting record version, and original
completion time. The coordinator validates the complete ledger shape and
resolves every required contributor before decrypting the replay envelope.

`StaffRights` version 3 replay requires exactly one matching restore
prerequisite and one matching owner contributor:

1. Workspaces resolves the latest completed Staff access process and the
   current Staff-owned restore-state snapshot.
2. A linked subject is accepted only when both owners agree on the exact
   subject and the latest completed access process ended in `Departed`.
3. An unlinked pre-anonymisation Staff record may prove that no subject access
   exists. An already-anonymised record without a durable Workspaces mapping
   is not sufficient, because a partially restored Organizations database
   could still contain the old membership.
4. Workspaces synchronously forces the organization membership to `Removed`
   and clears workspace role/profile assignments. `Changed`,
   `AlreadyInDesiredState`, and `NotFound` are safe outcomes. Owner protection,
   an invalid transition, unavailable state, or conflicting identity keeps
   execution or readiness closed.
5. Only after that denial succeeds may Staff acquire its operation lock,
   re-apply the exact anonymised state, attach the ledger proof to its
   tombstone, and append an immutable restore receipt.
6. Data Rights advances and confirms the restore checkpoint only after both
   the Workspaces prerequisite and Staff owner proof succeed.

The same Workspaces prerequisite runs before a new Staff owner mutation. This
ensures every new Staff ledger entry is restore-capable and reasserts the
already-required departure access denial before irreversible profile
mutation. A missing prerequisite is a composition failure; a missing or
conflicting mapping is a policy blocker; transient owner failures remain
retryable.

The Staff restore-state reader is a narrow Staff Contracts surface. It exposes
only the record id, exact version, lifecycle state, and optional Auth subject
needed for this coordination. Workspaces does not read Staff persistence and
Staff does not reference Workspaces or Organizations.

Staff restore receipts are append-only and keyed by ledger entry. Replay must
match the same Staff id, owner receipt, resulting version, tombstone revision,
and canonical digest. The relational schema rejects receipt update/delete and
unsafe downgrade once restore proof exists.

## Public Operator Surface Slice Contract

Approved Staff anonymisation is exposed only through the existing tenant-scoped
Data Rights case workflow:

- `GET /api/data-rights/tenant/cases/{caseId}/execution` reads the bounded,
  PII-free execution state with tenant `data-rights.read`;
- `POST /api/data-rights/tenant/cases/{caseId}/execution` starts the exact
  approved execution with tenant `data-rights.erase` and the host's configured
  destructive-operation authentication assurance;
- both routes use `DataRightsCaseScope.Staff`, return no-store responses, and
  preserve the property-scoped Guest routes and contracts unchanged;
- the command handler must still re-evaluate frozen approval evidence, enforce
  a distinct executor when policy requires it, and create only tenant-scoped
  version 2 work;
- the Worker must still complete the Workspaces access-denial prerequisite
  before Staff mutation and retain the version 3 restore proof described above;
  and
- no ordinary Staff management endpoint, self-service endpoint, Admin shortcut,
  or direct database path may anonymise a profile.

The operator web application may offer `Staff data removal` only as a Data
Rights request type. It must preserve the explicit decision and `REMOVE`
confirmation, poll the tenant execution route only while work is non-terminal,
and invalidate Staff directory/detail projections once terminal owner proof is
observed. An absent execution remains a normal not-found result rather than a
synthetic pending state.

Staff correction editing is outside this slice. A correct UI needs a
correction-claim-gated sensitive snapshot from Staff; it must not reuse an
ordinary Staff profile read or silently require unrelated profile permissions.

## Delivery Slices

1. Foundation: employment governance, holds, operation lock, permissions,
   persistence, export, catalogue, and focused proof.
2. Scope: case admission, scope-aware contributor contract, work items, and
   versioned ledger/delta compatibility.
3. Owner mutation: Staff eligibility, irreversible anonymisation, receipt,
   tombstone, and PII-free event.
4. Restore: synchronous Workspaces access denial, replay, re-scrub, readiness,
   and historical-delta restoration proof.
5. Operator surface: tenant-scoped Data Rights execution routes, destructive
   assurance, generated web contracts, and the guarded Staff removal workflow.

Each slice is independently reviewable and leaves destructive behavior disabled
until all preceding invariants are enforceable.

## Verification

Focused proof must cover:

- governance policy selection, acknowledgements, version conflicts, replay,
  expiry, and denied country-policy outcomes;
- hold placement/release, independent holds, replay conflicts, and exact
  Staff-version binding;
- operation-lock ordering under concurrent hold/governance/anonymisation
  attempts;
- privileged-assurance and tenant authorization boundaries;
- workspace seed and company-support ceiling behavior;
- export bounds and omission of operator identities;
- personal-data catalogue and generated inventory completeness;
- model/migration parity, append-only receipts, and tenant-first indexes;
- old and new ledger/delta integrity with tamper rejection;
- Staff anonymisation eligibility, mutation, proof, and ordinary-surface
  exclusion; and
- pre-ready restore access denial and idempotent re-scrub.

Development uses focused non-Docker checks while a slice is changing. Each
coherent slice gets one full non-Docker gate, one exact relevant Docker
scenario, and exact-candidate GitHub verification after publication.

### Foundation Verification

The prerequisite foundation passed the complete non-Docker repository gate on
2026-07-29 with `eng/verify.ps1 -SkipRestore`, including source-package,
architecture, migration-drift, build, and test checks. The exact PostgreSQL
scenario
`StaffDataRightsExportIntegrationTests.Staff_discovery_and_export_use_authoritative_postgresql_records`
also passed against the migrated schema, including legacy operation-lock
backfill, authoritative export, concurrent serialization, and downgrade
protection. No GMA framework change was required for this product-specific
foundation.

### Scope Verification

The scope slice passed the complete non-Docker repository gate on 2026-07-29
with `eng/verify.ps1 -SkipRestore`, covering solution/source-package guards,
the build, migration drift, architecture boundaries, and all 36 non-Docker test
projects. The final integration tail was also rechecked directly: 39 tests
passed with no failures or skips.

Focused proof passed for Data Rights (243 tests), Staff (94 tests), and
Inventory (67 tests). The exact PostgreSQL migration scenario
`DataRightsPersistenceIntegrationTests.Execution_batch_migration_promotes_only_ledger_proven_legacy_success`
passed against the upgraded schema. It proves ledger-backed legacy promotion,
version 1 and 2 compatibility, fail-closed malformed-row handling, append-only
trigger restoration, and guarded downgrade behavior. The migration model has
no pending changes. All mounted GMA repositories remained clean; this slice
required no GMA change.

### Owner Mutation Verification

Focused Data Rights and Staff proof covers version-specific dispatch,
idempotent receipt replay, frozen-state conflicts, eligibility blockers,
terminal reconciliation, ordinary-surface exclusion, and personal-data
catalogue completeness.

The exact PostgreSQL/NATS/task-runtime scenario
`StaffDataRightsAnonymisationIntegrationTests.Tenant_v2_execution_and_v3_restore_close_access_and_persist_owner_proof`
passed on 2026-07-30. It proves fresh-schema migration, version 2 tenant
dispatch, atomic Staff mutation and owner proof, PII-free task payloads,
ledger/case reconciliation, synchronous workspace access denial, version 3
restore into a separate pre-anonymisation database, idempotent Staff re-scrub,
append-only restore proof, readiness advancement, database-level update and
delete rejection for receipts, and guarded Staff and Data Rights downgrade.
Guest work remains pinned to owner contract version 1. No GMA change was
required.

### Restore Verification

The complete non-Docker repository gate passed after the restore slice,
including solution/source-package guards, build, migration drift, module and
architecture tests. The final visible tail was rechecked directly: 73
architecture tests, 30 host-default tests, and 39 non-Docker integration tests
passed with no failures.

Focused proof passed for Data Rights restore coordination and execution,
Workspaces trusted mapping and access denial, and all 113 Staff tests. The exact
container scenario named above passed against two independently migrated
PostgreSQL databases plus NATS and Task Runtime. It demonstrates that a
pre-anonymisation restore cannot become ready until organization membership and
workspace authorization are closed and the Staff owner has attached the exact
ledger proof to its tombstone and immutable restore receipt.

### Operator Surface Verification

The complete non-Docker repository gate passed on 2026-07-30 with
`eng/verify.ps1 -SkipRestore`, including solution/source-package guards, build,
migration drift, architecture boundaries, and all non-Docker test projects.
The web candidate passed `pnpm verify`: type checking, lint, 133 tests across
20 files, and the production build. Generated contracts also match the current
backend OpenAPI document.

The exact PostgreSQL/NATS authorization scenario
`DataRightsAuthorizationIntegrationTests.Tenant_case_routes_require_a_tenant_grant_and_preserve_property_routes`
passed. It proves tenant readers can inspect Staff execution, property-only
readers cannot cross into tenant scope, readers cannot erase, a normally
authenticated eraser is challenged for stronger assurance, and a fresh
two-step eraser reaches the application while the property-scoped Guest routes
remain available. No GMA change was required.

## Deferred

- automatic Staff retention scheduling and worker execution;
- Staff correction editing until a correction-claim-gated sensitive snapshot
  is available;
- production legal approval of country-policy content;
- payroll, tax, identity-document, contract, signature, and benefits records;
- legal-entity and employment-agreement domain ownership;
- global Auth account disablement or effects on unrelated workspaces;
- self-service public Staff erasure requests; and
- generalized GMA data-rights or employment-policy abstractions.

## Completion Criterion

The task is complete when an approved tenant-scoped Staff anonymisation can
execute against exact Staff-owned policy and hold evidence, irreversibly remove
the approved Staff data, produce restorable authoritative proof without
invalidating historical Guest deltas, synchronously close restored workspace
access, and pass focused, local, Docker, and exact-candidate verification.
