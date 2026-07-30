# Workspaces Data Rights Correction Task

Status: complete
Date: 2026-07-30

## Goal

Complete one narrow mutation capability for the Workspaces-owned
`staff-onboarding` coordinate. An approved tenant-scoped `StaffRights`
correction may replace the staged applicant profile of one exact onboarding
version while that record is still editable, produce immutable owner proof,
and complete the Data Rights case through the durable Workspaces outbox.

This remains a Workspaces slice. Data Rights may be changed only for the
producer-specific completion binding, and the operator UI may be changed only
to expose this owner workflow. No restriction, anonymisation, Staff profile,
Auth account, or Organizations source behavior is opened here.

## Authority Boundary

Workspaces owns the seven staged applicant fields only while the onboarding
record is `Submitted`:

- display name;
- legal name;
- work email;
- work phone;
- employee number;
- job title; and
- department.

Workspaces does not own or correct:

- verified account email or Auth subject identity;
- invitation, enrollment-link, or claim facts;
- Staff profile values after provisioning starts;
- employment lifecycle or property assignments;
- access-process or access-plan history;
- retention receipts; or
- historical actor, failure, and lifecycle facts.

An authority handoff is never implemented as a silent redirect. If the
selected Workspaces record is no longer editable, the approved coordinate is
stale and the operation fails closed. A Staff, Auth, or Organizations
correction requires a separately reviewed case selecting that authoritative
owner's current coordinate.

GMA remains unchanged. Its tenant scope, CQRS validation, transactions,
optimistic concurrency, outbox, messaging, and authorization primitives are
already sufficient. Workspaces field vocabulary and Data Rights case routing
are BunkFy product concepts.

## Coordinate And Field Policy

The correction accepts exactly:

- case type: `StaffRights`;
- property id: none;
- owner: `workspaces`;
- record type: `staff-onboarding`;
- record id: onboarding application id; and
- record version: current onboarding aggregate version.

The bounded field policy is
`workspaces.staff-onboarding.applicant-correction.v1`.

The owner policy contributor is registered only for this record type.
`staff-access-process`, `staff-access-plan`, and
`staff-retention-correlation-receipt` remain non-correctable. Data Rights
therefore cannot create a correction execution for them.

The request is a full replacement of the seven allowed fields. Display name
is required; empty optional values mean clear. Workspaces normalizes the
request with the same applicant-profile value object used by ordinary
submission. Corrected values never enter Data Rights persistence or
messaging.

## Claim-Bound Read And Mutation

The operator surface must not add a general applicant-data browsing endpoint.
It exposes one exact correction-target read and one correction command under
the Workspaces API. Both require:

- authenticated user identity;
- tenant scope and `data-rights.execute`;
- case id, approval revision, execution id, and executing actor;
- the exact owner coordinate and selected version; and
- an active, unexpired Data Rights correction execution.

The read returns only the seven correctable values, record id, and version.
It is `no-store` and cannot return verified email, subject id, source facts,
claim facts, Staff correlation, failure details, or history.

The mutation revalidates the execution before loading the aggregate. Inside
the transaction, Workspaces serializes the selected onboarding row with a
version-preserving update, rechecks for a receipt committed while waiting, and
then applies the aggregate mutation. The aggregate requires `Submitted`, the
exact selected version, and at least one normalized change. This lock plus the
optimistic concurrency token closes races with claim acceptance, invitation
acceptance, expiry, supersession, another submission update, and concurrent
replay. An authority handoff commits either before or after the correction,
never as a mixed owner state.

## Replay And Owner Proof

Workspaces stores one append-only correction receipt per execution with:

- receipt and execution ids;
- case id and approval revision;
- onboarding application id;
- selected and current record versions;
- changed-field mask;
- a SHA-256 fingerprint of the canonical normalized request;
- applicant and completion event ids; and
- completion time.

The request fingerprint is an internal personal-data derivative. It is used
only to distinguish exact replay from conflicting reuse and is never returned,
exported, logged, emitted, searched, or included in errors.

An exact replay returns the original receipt even if the onboarding record has
since advanced. Reusing the execution with different coordinates or a
different normalized request fails with a stable conflict. The receipt raises
a PII-free domain event, and the Workspaces outbox publishes the existing
tenant-scoped correction-completed contract with only coordinates, versions,
changed field keys, and proof identity.

Data Rights binds a separate handler to the Workspaces producer. It validates
the proof against the persisted execution and completes the case
idempotently. A Workspaces event cannot satisfy the Staff producer binding,
and delayed or duplicate delivery cannot repeat the owner mutation.

## Export And Retention

Correction receipts are Workspaces-owned subject accountability records.
Protected export of a selected onboarding record therefore includes its
bounded, ordered correction receipts as child records. Export includes
coordinates, versions, changed field keys, and completion time, but excludes
the request fingerprint.

Receipt lookup is indexed by tenant and execution id, with an additional
tenant/application ordering index for export. Export reads at most the
configured Workspaces child-record bound plus one and fails before writing if
the selected record would exceed that bound.

The existing onboarding lifecycle remains authoritative:

- completed, rejected, superseded, and expired records redact applicant data;
- Staff retention may later scrub remaining subject correlation;
- correction does not extend applicant-copy retention; and
- the immutable correction receipt follows its separately catalogued
  accountability retention policy.

Approved disposal periods remain an engineering default until product/legal
policy approves them.

## Operator UI

The Data Rights correction owner editor recognizes
`workspaces/staff-onboarding`, loads the exact claim-bound target, and submits
the full replacement to Workspaces. It shows stale, handed-off, expired-claim,
and conflicting-replay outcomes without exposing raw server details.

The editor invalidates only the selected Data Rights case and relevant
Workspaces onboarding queries after success. It does not poll lists or add
background refresh beyond the existing single non-terminal case refresh.

This slice does not add unrelated Staff or Auth correction editors.

## Implementation Slices

1. [Complete] Audit field authority, state handoff, Data Rights contracts,
   persistence concurrency, export obligations, and operator routing.
2. [Complete] Add the applicant-profile value object, correction outcome,
   immutable receipt, PII-free event, and focused domain tests.
3. [Complete] Add claim-bound target read, correction command, policy
   contributor, replay fingerprint, repository ports, and application tests.
4. [Complete] Add persistence mapping, append-only enforcement, export child
   records, migration, and catalogue coverage.
5. [Complete] Add Workspaces API routes, producer metadata, the explicit Data
   Rights completion subscription, and architecture/security tests.
6. [Complete] Add the Workspaces owner editor and generated web contracts.
7. [Complete] Run the coherent non-Docker gate once, one exact PostgreSQL/NATS
   scenario, then publish backend, web, and root candidates in dependency
   order with exact-commit CI.

## Verification

- Domain tests prove shared normalization, exact changed fields, no-op
  rejection, state handoff rejection, and one-version advancement.
- Application tests prove tenant scope, active claim, actor and field-policy
  binding, stale version, exact replay, conflicting replay, and no corrected
  values in owner proof.
- Persistence tests prove append-only receipts, unique execution ownership,
  database constraints, deterministic export, and the owner record bound.
- API tests prove tenant `data-rights.execute`, authenticated-user binding,
  exact target response shape, and `no-store`.
- Data Rights metadata and handler tests prove an explicit Workspaces producer
  subscription without weakening the Staff binding.
- Catalogue guards classify every request, target, command, receipt, event,
  persistence, and export member while prohibiting corrected values from
  integration events and operational sinks.
- One real PostgreSQL/NATS scenario proves claim, exact read, correction,
  atomic receipt/outbox commit, central completion, replay, conflict,
  authority handoff rejection, and no duplicate mutation.
- During implementation only focused checks run. The full non-Docker gate runs
  once at completed-slice state, Docker once for the exact final scenario, and
  GitHub Actions only for the published candidate.

Completed verification on 2026-07-30:

- the backend non-Docker chain passed after splitting the correction behavior
  from the aggregate root to satisfy the reviewable-file-size guard;
- Workspaces passed 165 focused tests, Architecture passed 74,
  ServiceDefaults passed 30, and non-Docker Integration passed 39;
- the web app passed typecheck, lint, 134 tests, and its production build; and
- the exact PostgreSQL/NATS scenario passed after its fixture chronology was
  corrected and the real Workspaces-to-Data Rights actor binding was aligned
  with the canonical `user:{subjectId}` identity.

Docker remained limited to this exact scenario. Failed iterations were fixed
and rerun against the same bounded scenario; no broad Docker suite was used.

## Deferred

- Workspaces processing restriction;
- Workspaces anonymisation, required-companion expansion, and restore proof;
- correction of access history through corrective audit actions;
- Staff, Auth, or Organizations owner editors;
- self-service correction;
- bulk or multi-owner correction;
- automatic case-coordinate rerouting; and
- approved legal retention periods.

## Completion Criterion

The slice is complete when an approved tenant-scoped correction can read and
mutate exactly one still-editable Workspaces onboarding version, replay
safely, export its bounded owner proof, and complete the central Data Rights
case without changing any identity, source, Staff, access, or historical
authority.
