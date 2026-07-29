# Staff Data Rights Correction Task

Status: implementation and final local verification complete; publication pending
Date: 2026-07-28

## Goal

Add the first mutation capability for tenant-scoped `StaffRights` cases. An
approved correction must update one exact Staff-owned profile version,
produce durable owner proof, and complete the Data Rights execution without
granting access, changing employment lifecycle, or reaching into Auth or
AccessControl.

This is one Staff slice. The existing Data Rights module may be changed where
its generic case orchestration is still property-only, but no other data owner
is opened for new behavior.

## Ownership

Staff owns:

- the editable Staff profile fields and their normalization;
- Staff member version revalidation and employee-number uniqueness;
- the correction mutation, changed-field calculation, and immutable receipt;
- exact replay or conflict behavior for one execution id; and
- the PII-free completion fact published from the Staff outbox.

Data Rights owns:

- `StaffRights` case admission and requester verification;
- subject selection, decision approval, execution claim, and expiry;
- contributor routing by owner, record type, and field-policy key; and
- central case completion after validating Staff's receipt coordinates.

GMA continues to own generic CQRS, scope context, transactions, outbox
delivery, and persistence primitives. Staff field vocabulary, employment
history, and Data Rights case kinds are BunkFy product concepts. No GMA change
is required.

## Data Rights Scope Prerequisite

Data Rights already supports tenant-scoped Staff discovery and export, while
its correction execution path currently requires a property id. This slice
must make correction execution scope-aware:

- correction claims and queries accept an explicit case type plus nullable
  property coordinate;
- `GuestRights` remains property scoped;
- `StaffRights` is tenant scoped and rejects a property coordinate;
- the correction gate reconstructs and validates the exact case scope;
- the execution row persists case type and a nullable property id;
- the execution contract and database constraint advance together; and
- tenant correction endpoints expose claim and query operations under
  `/api/data-rights/tenant/cases`.

The existing property-scoped completion event remains compatible. Staff
publishes a separate versioned tenant-correction completion event so deployed
or queued Guest and Reservation events do not silently change shape.

## Case Admission

`StaffRights` accepts exactly one of:

- `AccessExport`; or
- `Correction`.

Combined operations and restriction, erasure, or anonymisation remain closed
until their owner slices exist. Valid requester relationships remain data
subject, authorized representative, or controller initiated. Tenant-owner
requests remain rejected.

## Coordinate And Field Policy

The selected coordinate remains:

- owner: `staff`;
- record type: `staff-member`;
- record id: Staff member id; and
- record version: Staff aggregate version.

The correction field policy is
`staff.staff-member.correction.v1`. It allows full replacement of these
bounded profile fields:

- display name;
- legal name;
- work email;
- work phone;
- employee number;
- job title; and
- department.

Display name remains required. Empty optional values mean clear. The request
uses the complete allowed profile shape rather than patch semantics, and the
owner computes the exact changed field keys after normalization.

The correction cannot change:

- Auth subject linkage;
- active, suspended, or departed state;
- suspension or departure facts;
- property assignments;
- aggregate identity or tenant;
- creation facts; or
- prior actor and reason history.

Correction is allowed for active, suspended, and departed profiles because
retained employment data must remain correct. It must not reactivate a
departed profile or make any assignment current.

## Mutation And Replay

Execution must fail closed unless all of these still match in one transaction:

- tenant and `StaffRights` scope;
- case id, approval revision, and unexpired execution id;
- executing actor;
- owner, record type, and field-policy key;
- Staff member id and selected aggregate version; and
- employee-number uniqueness within the tenant.

The aggregate applies normalized values, rebuilds its normalized search
copies, advances exactly one version, and emits only identifiers, version,
status, time, and changed field keys.

Staff stores one immutable receipt per execution with:

- receipt and execution ids;
- case id and approval revision;
- Staff member id;
- selected and current aggregate versions;
- changed-field mask;
- a SHA-256 fingerprint of the canonical normalized request;
- completion event ids; and
- completion time.

The fingerprint is an internal personal-data derivative. It is never emitted,
logged, returned, or used for search, and must be classified in the Staff
personal-data catalogue. An exact replay returns the original receipt even if
the Staff profile later advances. Reusing the execution id with different
coordinates or a different fingerprint fails with a stable conflict.

## Surfaces And Events

The Staff management API exposes one tenant-scoped correction endpoint. It
requires the Data Rights execute permission and delegates authorization to the
claimed execution gate. Ordinary `staff.manage` permission alone cannot run an
approved correction.

The public receipt DTO contains only bounded proof coordinates and changed
field keys. It contains no corrected values or request fingerprint.

Staff publishes a tenant-scoped, PII-free correction-completed event. Data
Rights binds one explicit Staff producer handler, validates the event against
the stored execution, records central receipt digests, and completes the case.
Duplicate delivery is idempotent; mismatched completion coordinates fail
closed.

## Persistence And Privacy

- Add a Staff-owned correction receipt table in the `staff` schema.
- Add the Data Rights execution-scope migration in the `data_rights` schema.
- Keep all keys visibly tenant scoped and all versions optimistic.
- Add database checks for contract version, coordinate validity, version
  progression, field mask, digest length, and completion ids.
- Add indexes for exact execution replay; do not add broad personal-data
  search indexes.
- Extend the Staff executable personal-data catalogue and regenerate its
  deterministic inventory.
- Keep corrected values, fingerprints, actor ids, and Staff identifiers out of
  logs, metrics, traces, notifications, and general integration payloads.

No property country may be borrowed as an employment jurisdiction. Automatic
Staff retention remains deferred until Staff owns an explicit employment
jurisdiction or another approved policy coordinate.

## Implementation Record

- Data Rights correction claims, queries, persistence, and authorization gates
  now carry an explicit case type plus nullable property coordinate.
- The existing property event remains unchanged. Staff publishes a separate
  tenant-scoped completion event through its own outbox, and Data Rights binds
  it to the Staff producer explicitly.
- Staff owns one tenant endpoint, normalized full-profile correction, departed
  profile support, employee-number uniqueness, optimistic versioning, exact
  request replay, and an append-only receipt.
- Staff's executable personal-data catalogue classifies every new command,
  receipt, event, persistence, actor, coordinate, and request-fingerprint
  surface; its deterministic inventory has been regenerated.
- PostgreSQL migrations advance the Data Rights correction contract and add
  the Staff receipt table with tenant keys, constraints, and narrow replay
  indexes.
- Cross-process completion accepts only a bounded one-minute clock skew while
  continuing to reject future-dated owner proof outside that window.
- `TenantTermination` remains tenant scoped but has no correction policy
  contributor, producer, or endpoint; this slice opens correction only for
  `StaffRights`.
- A Docker-bound integration proof covers a departed Staff profile, tenant
  authorization, both module transactions, NATS/outbox delivery, immutable
  replay proof, conflicting replay, and central case completion.

## Verification Cadence

During implementation run only focused Staff, Data Rights correction, contract,
and architecture tests. At the completed-slice gate run:

1. Staff domain tests for normalization, exact changed fields, departed-profile
   correction, and lifecycle/assignment preservation.
2. Staff application tests for approval, stale version, uniqueness, no-op,
   exact replay, and conflicting replay.
3. Data Rights tests for tenant claim/query/gate/completion, invalid scope, and
   property-path compatibility.
4. Contract and catalogue guards proving no corrected value crosses an
   operational boundary.
5. One PostgreSQL migration and transaction path for tenant-scoped correction.
6. The complete non-Docker verifier once.
7. The relevant Docker suite once.
8. Exact-candidate GitHub gates once before publication.

After a final-gate failure, fix the failure batch with focused checks and rerun
only the failed or resumed tail.

## Verification Evidence

- The consolidated non-Docker verifier completed with a zero-warning build,
  migration drift checks, all module fast tests, 73 architecture tests, 30
  Service Defaults tests, and 39 non-Docker integration tests.
- The focused Data Rights model suite passed 12 tests, its PostgreSQL migration
  project built with zero warnings, and EF reported no pending model changes
  after the case-operation constraint correction.
- The single Docker-bound Staff correction scenario passed against PostgreSQL
  and NATS, including departed-profile mutation, tenant authorization, atomic
  receipt and outbox persistence, central completion, exact replay, and
  conflicting-replay rejection.

## Deferred

- Staff processing restriction;
- Staff anonymisation, tombstone, and restore proof;
- employment jurisdiction and automatic retention;
- coordinated Auth correction or disablement;
- AccessControl grant changes;
- assignment-history correction;
- public self-service case creation; and
- correction of payroll, identity documents, or other future Staff domains.

## Completion Criterion

The slice is complete when a tenant-scoped approved `StaffRights` correction
can claim an execution, mutate exactly one selected Staff profile version,
replay safely, publish bounded owner proof, and complete the central case while
all property-scoped correction behavior remains compatible and exact-candidate
verification is green.
