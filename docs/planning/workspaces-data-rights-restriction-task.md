# Workspaces Data Rights Restriction Task

Status: complete
Date: 2026-07-30

## Goal

Add reversible tenant-scoped processing restriction for one exact
Workspaces-owned `staff-onboarding` record. An approved `StaffRights` case may
apply or release an independent owner restriction while Workspaces still owns
the staged applicant copy, produce durable replay-safe proof, and suppress
ordinary onboarding processing without deleting the record or weakening
source expiry, access safety, retention, or other approved Data Rights work.

This remains a Workspaces slice. Data Rights already owns tenant-scoped
restriction admission, approval, execution routing, and central proof. No GMA
change is required. Staff, Auth, Organizations, and Access Control behavior
must not be changed by a Workspaces restriction.

## Authority Boundary

Workspaces may accept a restriction only for:

- owner `workspaces`;
- record type `staff-onboarding`;
- the current onboarding record id and version;
- a tenant-scoped `StaffRights` case with no property id; and
- a record that still contains the Workspaces-owned applicant copy and has not
  handed authority to a durable Staff profile.

Apply is eligible only while `StaffMemberId` is absent and the onboarding
state is one of:

- `Submitted`;
- `PendingApproval`;
- `Provisioning`; or
- `Failed`.

`Provisioning` and `Failed` are eligible only while Staff provisioning has not
created a Staff member. Once `StaffMemberId` exists, the current profile
authority belongs to Staff and a new Staff-owned case is required. Completed,
rejected, superseded, and expired onboarding records have already redacted the
applicant copy and reject a new Workspaces restriction.

Release may target the current version of the same onboarding record after its
lifecycle has advanced. A release removes only the selected active
restriction; it does not recreate redacted values, reopen onboarding, or
redirect authority.

Workspaces does not register restriction for:

- `staff-access-process`;
- `staff-access-plan`; or
- `staff-retention-correlation-receipt`.

Access processes and plans coordinate access safety and retain provenance.
Blocking their required transitions could strand a suspension, departure, or
source-expiry workflow, while restricting a plan based on its creating actor
could affect unrelated applicants. They remain discoverable and exportable
through explicit rights paths, but are not restriction authorities in this
slice.

## Owner Model

Each approved apply case creates an independent onboarding restriction with:

- tenant and onboarding coordinates;
- apply case id and approval revision;
- selected onboarding version;
- active or released status and optimistic version;
- apply actor and timestamp; and
- release case, approval revision, selected current onboarding version,
  actor, and timestamp after release.

The effective projection is keyed by tenant and onboarding id and contains:

- the supported Workspaces onboarding-restriction contract version;
- active restriction count;
- effective restricted state;
- monotonic projection revision; and
- last transition time.

Multiple apply cases compose by reference count. Releasing one restriction
cannot clear another. A release is executable only when exactly one active
restriction exists for the selected onboarding record; ambiguous release
fails closed for explicit operator resolution.

Every transition writes an append-only receipt containing:

- idempotency key and receipt id;
- restriction, onboarding, case, and approval coordinates;
- selected onboarding version;
- action and resulting restriction/projection revisions;
- resulting effective state;
- executing actor;
- PII-free transition event id; and
- completion time.

Equivalent retries return the committed receipt. Reusing an idempotency key
with different inputs fails. The canonical receipt digest is returned only as
central owner proof; it is not logged or used as a search key.

## Operational Enforcement Matrix

Restriction is enforced in database predicates and command boundaries, before
materialization or cross-module calls.

| Workspaces surface | Restricted behavior |
| --- | --- |
| Applicant self-service read | Return not found |
| Ordinary submission update | Reject without changing staged values |
| Join invitation acceptance admission | Deny |
| Enrollment claim/approval admission | Deny |
| Actionable onboarding list | Exclude before paging |
| Staff provisioning and manual retry | Reject before Staff, assignment, or Access Control calls |
| Duplicate/source safety checks | Continue to consider the record |
| Invitation/claim rejection, revocation, expiry, or supersession | Allow and redact according to the existing lifecycle |
| In-flight Staff access-safety process | Continue |
| Data Rights discovery/export/correction | Allow through their dedicated paths |
| Restriction apply/release | Allow only through the owner contributor |
| Retention and approved destructive prerequisites | Continue |

Correction remains available while restricted because it is an explicit
rights operation. Restriction never changes Organizations membership, Auth
credentials, Access Control grants, Staff lifecycle, or an already-created
Staff record.

## Concurrency And Recovery

The correction-specific onboarding row lock becomes a purpose-neutral
Workspaces onboarding operation lock. Correction, restriction apply/release,
ordinary resubmission of an existing record, and the provisioning processor
use the same lock.

The provisioning processor acquires the lock before consulting the effective
restriction projection or calling Staff, assignment, or Access Control ports.
This closes the important race:

- if provisioning locks first, a later apply reloads the advanced onboarding
  state and rejects after authority handoff;
- if restriction locks first, provisioning observes the supported restricted
  projection and performs no cross-module call.

Organizations admission policy performs an indexed unrestricted read so the
normal invitation and enrollment paths are denied before source acceptance.
The processor remains the race-closing backstop because Organizations and
Workspaces commit independently.

If an acceptance event wins the admission race but the processor observes a
committed restriction, the onboarding record may remain `Provisioning` with no
Staff member. Releasing the final active restriction emits a durable,
PII-free transition event. A Workspaces inbox handler reloads the record and
retries only this recoverable `Provisioning` state after confirming the
supported projection is unrestricted. A newer active restriction ends that
recovery attempt without an external call; any genuine processor failure is
surfaced to the inbox so delivery remains retryable. Delivery is idempotent
and a later manual retry remains safe.

Missing or unsupported effective projection state fails closed for every
ordinary path. Safety-reducing lifecycle transitions and explicit rights work
use purpose-named repository paths that do not depend on optional-processing
visibility.

## Persistence And Efficiency

Workspaces adds:

- onboarding processing restrictions;
- one effective projection per onboarding record; and
- immutable transition receipts.

The migration backfills a supported zero-restriction projection for every
existing onboarding record. New onboarding creation writes the aggregate and
baseline projection in the same Workspaces unit of work.

Operational lookups use an indexed supported/unrestricted projection predicate
before ordering, skip, and take. Restriction and receipt lookups use bounded
tenant/onboarding, approval, status, and idempotency indexes. Release reads at
most two active rows. No request scans all tenants, onboarding records,
restrictions, cases, or receipts.

Projection and receipt constraints enforce:

- supported identity and contract versions;
- active-count/effective-state consistency;
- apply/release lifecycle consistency;
- positive revisions;
- append-only receipts; and
- unique idempotency and approval ownership.

## Export And Catalogue

Protected export of a selected onboarding record includes bounded, ordered
restriction lifecycle and transition receipt child records. It exposes the
minimum accountability coordinates, action, state, versions, and timestamps.
It does not expose the receipt digest or executing operator identity in the
Workspaces subject fragment.

The embedded Workspaces personal-data catalogue and generated inventory must
classify:

- restriction commands and owner contribution requests;
- aggregates, effective projections, receipts, and events;
- persistence columns and query surfaces;
- protected-export child records; and
- retention policy for immutable accountability proof.

No applicant values, subject ids, verified emails, case ids, actors, or
reasons enter integration events, logs, metrics, traces, notifications, or
support bundles. The transition event contains only tenant/onboarding
coordinates, contract version, projection revision, and effective state.

## Product And Framework Boundary

Data Rights owns the existing generic restriction contributor contract,
approval gate, central execution proof, and tenant endpoint. Workspaces owns
all eligibility, lifecycle, enforcement, persistence, replay, and recovery
semantics.

The repeated restriction shape across BunkFy owners is intentionally not
moved to GMA. Data-rights case types, owner coordinates, eligibility matrices,
and proof semantics are product concepts. Extracting a generic aggregate now
would couple EF mappings and erase meaningful owner differences without
removing the hard policy decisions.

No owner-specific API or editor is needed. The existing tenant Data Rights
restriction endpoint and generic operator flow execute the selected
Workspaces coordinate.

## Implementation Slices

1. [Complete] Audit onboarding authority, access-safety boundaries, ordinary
   reads and writes, source-event races, restriction contracts, and recovery.
2. [Complete] Add restriction aggregate, projection, receipt, contract DTOs,
   domain event, errors, and focused domain tests.
3. [Complete] Generalize the onboarding operation lock and add apply/release
   commands, contributor routing, proof digest, and application tests.
4. [Complete] Enforce unrestricted operational reads, submission, admission,
   actionable paging, processor calls, and release recovery.
5. [Complete] Add persistence mappings, append-only enforcement, migration
   backfill, export children, and catalogue coverage.
6. [Complete] Add composition, security, integration, and PostgreSQL
   coverage.
7. [Complete] Run one coherent non-Docker gate, one exact final Docker
   scenario, then publish backend and root candidates with exact-commit CI.

## Verification

- Domain tests prove independent restrictions, reference-counted projection
  state, exact release, temporal validation, and immutable proof creation.
- Application tests prove tenant scope, owner/record/case binding, approval,
  deadline, actor, selected version, eligibility, replay, conflicting reuse,
  ambiguous release, and authority handoff.
- Operational tests prove self-read, resubmission, admission, actionable
  paging, processor, retry, lifecycle bypasses, correction, and retention
  follow the matrix.
- Concurrency tests prove apply versus resubmission and apply versus
  provisioning serialize through the same row lock.
- Persistence tests prove baseline projection creation, migration backfill,
  database constraints, append-only receipts, indexed bounded reads, and
  deterministic export.
- Composition tests prove exactly one Workspaces restriction contributor and
  no Data Rights persistence dependency on Workspaces.
- One PostgreSQL scenario proves legacy projection backfill, operational
  suppression, lifecycle visibility, exact release, and database-enforced
  append-only receipts. Focused application and composition tests prove the
  durable final-release recovery route and outbox ownership.
- During implementation only focused checks run. The full non-Docker gate runs
  once at completed-slice state, Docker once for the exact final scenario, and
  GitHub Actions only for the published candidate.

Completed verification on 2026-07-30:

- solution synchronization and source-package guards passed;
- the all-up build passed with zero warnings and every provider migration
  model reported zero drift;
- Workspaces passed 193 tests, Architecture passed 75, ServiceDefaults passed
  30, and non-Docker Integration passed 39;
- the exact PostgreSQL restriction migration, suppression, release, and
  append-only receipt scenario passed on its first container run; and
- the final durability audit corrected inbox retry signaling, after which all
  six focused enforcement tests and the complete Workspaces suite passed.

Docker remained limited to that one exact scenario. GitHub Actions is reserved
for the published backend and root candidates.

## Deferred

- restriction of access-process or access-plan provenance;
- coordinated Staff, Auth, Organizations, or Access Control restriction;
- self-service case creation;
- bulk or multi-coordinate restriction;
- jurisdiction-specific adjudication and approved legal bases;
- Workspaces anonymisation and required-companion selection; and
- approved legal retention periods.

## Completion Criterion

The slice is complete when an approved tenant-scoped `StaffRights`
restriction can apply or release one exact still-authoritative
`workspaces/staff-onboarding` record, ordinary onboarding processing fails
closed according to the matrix, lifecycle and access-safety work continues,
release converges any acceptance race, central Data Rights stores validated
owner proof, and the focused, single final local, Docker, and exact-candidate
gates are green.
