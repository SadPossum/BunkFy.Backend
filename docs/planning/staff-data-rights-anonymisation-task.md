# Staff Data Rights Anonymisation Task

Status: in progress; foundation and scope slices implemented and verified
Date: 2026-07-29

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

This is delivered in guarded slices. Staff anonymisation case admission and
destructive mutation remain disabled until the prerequisite foundation is
complete and verified.

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
created and backfills one for each legacy member. Governance changes,
hold transitions, correction, restriction, and later anonymisation acquire the
same lock before selecting mutable state. This prevents a new hold or policy
replacement from racing an approved destructive execution.

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

Destructive Staff execution remains intentionally denied. Existing owner
requests, terminal events, and restore contributors are property-scoped
version 1 contracts. Data Rights must not dispatch Staff work until the owner
mutation slice supplies versioned tenant-capable contracts and Staff registers
the corresponding owner.

Version 3 restore also remains intentionally closed before replay-envelope
decryption. This prevents a scoped ledger entry from being replayed through
the Guest property protocol while the synchronous Workspaces access-denial
step is still unavailable.

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

## Delivery Slices

1. Foundation: employment governance, holds, operation lock, permissions,
   persistence, export, catalogue, and focused proof.
2. Scope: case admission, scope-aware contributor contract, work items, and
   versioned ledger/delta compatibility.
3. Owner mutation: Staff eligibility, irreversible anonymisation, receipt,
   tombstone, and PII-free event.
4. Restore: synchronous Workspaces access denial, replay, re-scrub, readiness,
   and historical-delta restoration proof.

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

## Deferred

- versioned tenant-capable approval-gate, owner-request, terminal-event, and
  restore contracts;
- Staff owner mutation under the per-member operation lock, with exact
  re-evaluation of every frozen state binding;
- automatic Staff retention scheduling and worker execution;
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
