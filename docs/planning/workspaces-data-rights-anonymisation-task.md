# Workspaces Data Rights Anonymisation Task

Status: implementation and final local verification complete; publication pending
Date: 2026-07-30

## Goal

Complete the destructive `StaffRights` path without allowing a selected Staff
member to leave required Workspaces-owned person correlation untreated.

The slice has two product-level parts:

1. add a bounded, owner-generic required-companion expansion contract to
   BunkFy Data Rights; and
2. add Workspaces-owned anonymisation, tombstone, receipt, and database-restore
   replay for the exact workspace correlation attached to a departed Staff
   member.

The reusable orchestration belongs in BunkFy Data Rights. Workspaces owns the
meaning and mutation of its records. Staff remains authoritative for the
employment lifecycle and retention policy. GMA remains unchanged.

## Selection And Review Contract

Required companions are expanded when a case leaves discovery for review.
They are never added implicitly after approval or during execution.

The Data Rights contract must:

- identify a contributor by a stable key, case type, operation, and source
  owner/record type;
- pass one exact selected source coordinate and tenant scope;
- return zero or more exact companion coordinates, or a bounded blocked/retry
  outcome;
- validate every returned coordinate through its owning discovery
  contributor;
- expand transitively in deterministic order;
- reject malformed results, duplicate contributor registrations, stale
  coordinates, cycles that exceed the bounded walk, and more than 100 total
  selected subjects;
- add all companions and enter review as one aggregate change; and
- leave correction and restriction single-coordinate behavior unchanged.

Expansion is visible in the selected-subject list before an operator approves
the case. Execution continues to create one durable work item and one owner
proof per selected coordinate.

## Approval Evidence

A Staff Rights case may contain one governing coordinate and its required
companions. The Staff coordinate is the governing policy authority.

The versioned Data Rights policy-contribution contract must allow:

- one authority contribution containing the legal/retention policy evidence
  and its owner state bindings; and
- companion contributions naming the selected authority coordinate and
  supplying only owner-local state bindings.

Data Rights validates that there is exactly one authority, every companion
points to it, every selected coordinate has exactly one contributor, binding
keys are unique, and the combined binding count remains within the contract
limit. It then freezes one canonical approval-evidence document containing
all owner bindings.

Owner execution must validate its own binding subset against current state.
It must not require another owner to understand or re-create that state.

## Workspaces Companion

For a selected `staff/staff-member` coordinate, Workspaces resolves the unique
completed departure access process with:

- the same tenant;
- the same Staff member id and selected Staff version;
- target state `Departed`;
- state `Completed`; and
- the exact pre-anonymisation Auth subject id when one exists.

That `workspaces/staff-access-process` coordinate is the required companion.
No companion is required when Staff has no Auth subject and Workspaces has no
access mapping. Missing, conflicting, active, retained-pseudonym, or
cross-tenant state blocks review.

The completed departure process is the Workspaces anchor because it is the
durable access-closure mapping already required by Staff anonymisation. The
companion does not transfer authority for Staff data to Workspaces.

## Workspaces Owner Mutation

The Workspaces owner contributor accepts only the selected completed departure
access-process coordinate from a tenant-scoped `StaffRights` anonymisation.
Before mutation it must prove:

- the anchor id and version are current;
- the departure mapping and Staff version are exact;
- no person-linked onboarding or access process remains active;
- access denial is complete or safely reasserted;
- the frozen Workspaces state binding still matches; and
- no prior retention or Data Rights proof conflicts with the request.

The mutation pseudonymises only Workspaces-owned subject/actor correlation
equal to the departed subject across:

- terminal onboarding records;
- completed access processes; and
- access plans created by that subject.

Applicant fields remain governed by the existing onboarding lifecycle and
must already be redacted for terminal records. Staff member ids, source and
claim coordinates, target lifecycle state, effective dates, profile snapshots,
and minimum access/audit proof are preserved.

The pseudonym is derived from the owner receipt id and contains no original
subject value. The receipt records exact request coordinates, selected and
resulting anchor versions, bounded mutation counts, approval hashes, and
canonical owner proof. The protected tombstone records only the minimum state
needed to prove and replay anonymisation. Neither stores the original subject
id.

Equivalent retries return the committed proof. Reusing an idempotency key or
ledger entry with different coordinates fails closed.

## Ordering And Restore

Staff and Workspaces work items may complete in either order.

- If Staff runs first, Workspaces uses the still-durable departure mapping to
  scrub its correlation.
- If Workspaces runs first, the Staff access prerequisite accepts only a
  matching Workspaces tombstone proving that access closure was checked before
  correlation was scrubbed.

Database restore replays the anonymised state, not the deleted identity. The
Workspaces restore contributor derives the same pseudonym from the owner
receipt id, reapplies the scrub to the restored pre-anonymisation rows, attaches
the ledger proof to the tombstone, and writes an append-only restore receipt.
It never reopens access or manufactures applicant data.

## Persistence And Efficiency

All target reads are tenant-leading, indexed, projection-only where practical,
and bounded before materialisation. Bulk correlation updates execute in one
owner transaction. The completed departure anchor is locked before state is
revalidated and changed.

The policy snapshot and execution proof use deterministic hashes over stable
coordinates and versions rather than personal values. Oversized or ambiguous
correlation sets fail closed with an operator-visible outcome; they are never
partially anonymised.

New tables are append/proof oriented:

- Workspaces anonymisation receipts;
- Workspaces anonymisation tombstones; and
- Workspaces anonymisation restore receipts.

Database constraints and append-only triggers protect canonical proof and
prevent receipt mutation or deletion.

## Catalogue And Boundaries

Update the executable Workspaces personal-data catalogue and generated
inventory for all new command, receipt, tombstone, restore, contributor, and
proof fields. No subject id, actor id, approval digest, receipt digest, or
tombstone detail may enter logs, metrics, traces, notifications, or support
bundles.

Architecture tests must prove:

- Data Rights Contracts contain no Workspaces or Staff semantics;
- Data Rights Application does not reference Workspaces or Staff;
- Workspaces reaches Data Rights and Staff only through contracts; and
- GMA framework, skeleton, modules, and extensions remain unchanged.

## Implementation Slices

1. [Completed] Freeze this authority, expansion, approval, execution, and
   restore contract.
2. [Completed] Add generic bounded required-companion expansion to Data Rights.
3. [Completed] Add authority/companion approval-evidence aggregation and adapt
   Staff binding validation to the combined evidence.
4. [Completed] Add the Workspaces companion resolver and owner-local policy
   binding.
5. [Completed] Add Workspaces anonymisation, idempotent proof, and commutative
   Staff prerequisite behavior.
6. [Completed] Add database-restore replay, tombstone, and restore receipt.
7. [Completed] Add migrations, catalogue coverage, architecture guards, and
   focused tests.
8. [Complete] Run the coherent local non-Docker gate and the exact relational
   scenario.
9. [Pending publication] Publish the backend and root pointers, then verify
   exact-commit CI.

## Verification

1. Selecting only an eligible Staff coordinate expands the exact Workspaces
   companion before review.
2. No-correlation Staff state adds no companion; missing, stale, conflicting,
   active, cross-tenant, malformed, duplicate, and oversized expansion fails
   closed.
3. Correction and restriction retain their one-coordinate contract.
4. Approval freezes one Staff authority policy plus both Staff and Workspaces
   state bindings.
5. Either owner execution order reaches the same final anonymised state and
   valid per-owner ledger proof.
6. Retries are idempotent and conflicting retries cannot mutate state.
7. Restore from pre-anonymisation rows reproduces the same pseudonymised
   Workspaces state and protected proof without restoring access.
8. PostgreSQL constraints and triggers reject proof mutation/deletion and
   prevent cross-tenant replay.
9. Focused Data Rights, Staff, and Workspaces tests, architecture tests,
   migration drift, the coherent non-Docker gate, one exact Docker scenario,
   and exact-commit GitHub checks pass.

Local verification completed on 2026-07-30:

- `eng/verify.ps1 -SkipRestore` passed with a zero-warning solution build,
  clean migration drift, and all non-Docker test assemblies green.
- the exact PostgreSQL
  `WorkspaceStaffCorrelationAnonymisationPersistenceIntegrationTests`
  scenario passed with one test executed and zero skipped.

## Not In This Slice

- tenant-termination destructive behavior;
- cross-workspace subject search;
- changing Staff, Auth, Organizations, or Access Control ownership;
- undoing anonymisation or retaining recoverable original identity;
- generic framework/GMA changes; and
- unrelated new product domains.
