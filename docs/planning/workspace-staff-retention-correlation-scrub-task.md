# Workspace Staff Retention Correlation Scrub Task

Status: implementation and final local verification complete
Date: 2026-07-30

## Goal

When the existing Staff automatic-retention workflow irreversibly anonymises a
departed Staff profile, remove that person's Auth subject correlation from
Workspaces-owned onboarding and access history without deleting required audit
facts, choosing a new legal retention period, or weakening replay safety.

This is a Workspaces owner-data slice. It extends the already required
Workspaces Staff-retention prerequisite; it does not add another schedule.

## Audit Result

- Terminal onboarding states already redact copied names, verified email,
  work contact details, employee number, job title, and department.
- Abandoned enrollment-link submissions already have a bounded automatic
  staging-reconciliation schedule.
- Completed onboarding, access-process, and access-plan history still stores
  Auth subject ids after the Staff profile is irreversibly retained.
- The access-process row is required to prove workspace access was closed
  before Staff retention. Deleting it independently would break that invariant
  and the existing Data Rights restore boundary.
- The remaining source, claim, Staff, profile, property, process, and lifecycle
  ids are opaque operational or audit coordinates. Their final deletion period
  remains a separate approved-policy decision.

## Ownership

- Staff owns employment governance, the retention deadline, legal holds,
  operation locking, Staff mutation, and the irreversible-retention decision.
- Workspaces owns access closure and every Workspaces subject/actor reference.
  It revalidates its departure process, scrubs its own rows transactionally,
  and records an immutable PII-minimal receipt.
- Retention owns schedule health and the outer execution only. It receives no
  Workspaces record or subject coordinate.
- GMA remains unchanged. Generic task, scope, persistence, Organizations, and
  AccessControl mechanics are already sufficient.

The existing `IStaffRetentionAnonymisationPrerequisite` contract is the correct
boundary: Staff asks whether every owner-side precondition is complete, while
Workspaces performs only the access and correlation work it owns.

## Eligibility And Ordering

Workspaces may scrub only when all of the following are true:

1. the versioned Staff-retention request is valid and tenant scoped;
2. Staff still reports the exact selected profile as departed;
3. the matching completed departure access process exists and still contains
   the same Auth subject;
4. access denial has been idempotently re-applied;
5. no active Workspaces onboarding application remains for that subject;
6. no active access process still names that person as its subject or actor;
   and
7. no valid receipt already proves the same Staff/version scrub.

Staff evaluates employment policy, deadline, restriction, legal hold, and its
operation lock before invoking the prerequisite. A Workspaces conflict returns
a stable blocked or retry-required result and prevents Staff mutation.

Access denial commits before the Workspaces scrub transaction. A failure in
the later transaction therefore leaves authority narrower, never broader, and
is safe to retry.

If Workspaces committed its receipt but the following Staff transaction did
not commit, the next retry uses the valid receipt as the historical mapping
proof and re-denies the still-authoritative Staff subject before returning
complete. It does not mutate the scrubbed history a second time.

## Mutation

One Workspaces transaction derives an opaque value from a new random receipt
id and replaces the original Auth subject wherever Workspaces owns it:

- `WorkspaceStaffOnboarding.SubjectId` on terminal applications;
- `WorkspaceStaffAccessProcess.SubjectId` for the retained Staff member;
- `WorkspaceStaffAccessProcess.RequestedBy` where that person was the actor;
  and
- `WorkspaceStaffAccessPlan.CreatedBySubjectId` where that person issued a
  join source.

Every affected aggregate advances its concurrency version and
`LastChangedAtUtc`. The opaque value is stable within this receipt so retained
audit rows remain internally explainable, but it cannot be used to recover the
Auth subject.

Names, contact fields, token plaintext, source payloads, hold reasons, and
policy documents never enter the receipt, Retention result, logs, metrics,
events, or task payloads.

## Receipt And Replay

Workspaces stores one immutable receipt per tenant, Staff member, and selected
Staff version. It records:

- contract version, receipt id, and outer execution id;
- Staff member id and selected Staff version;
- bounded counts for onboarding, access-process, and access-plan rows;
- completion time; and
- a canonical SHA-256 digest over the complete receipt.

The Staff member id is the existing random owner coordinate, not an Auth
subject or direct identifier. A valid receipt makes later retries complete
without restoring the removed Workspaces subject value. The completion time is
canonicalized to database-safe microsecond precision before mutation and
digest creation. Conflicting, malformed, or digest-invalid proof fails closed.

The scrub is intentionally not used by Data Rights anonymisation. That path
retains its protected restore behavior and continues to use the original
Workspaces access proof until a separately designed multi-owner restore slice
exists.

## Efficiency And Persistence

- Candidate selection remains in Staff; Workspaces does not scan tenants.
- Workspaces performs one exact receipt lookup, one active-onboarding guard,
  one departure-process revalidation, and bounded set-based updates by indexed
  subject/actor fields.
- Add tenant-leading subject/actor indexes needed by those updates.
- Add one tenant-scoped receipt table with uniqueness on
  `(ScopeId, StaffMemberId, SelectedStaffVersion)`.
- Reject tracked receipt updates and deletes in the provider-neutral
  `WorkspacesDbContext`; PostgreSQL also enforces append-only storage with a
  table trigger so direct SQL and bulk operations cannot bypass the invariant.
- PostgreSQL-specific DDL stays in the Workspaces migration project; domain,
  application, contracts, and persistence behavior remain provider agnostic.

## Verification

1. Invalid, cross-tenant, active, stale, owner-protected, and missing departure
   states fail closed.
2. A successful retention prerequisite closes access, scrubs every owned
   subject/actor occurrence, advances affected versions once, and writes one
   valid receipt.
3. Exact and later-execution retries return the existing proof without another
   mutation.
4. Unrelated subjects, actors, tenants, Staff ids, and active onboarding rows
   remain unchanged.
5. Data Rights execution and restore prerequisite behavior remains unchanged.
6. Model, migration, catalogue, architecture, and one real PostgreSQL/Worker
   scenario prove the durable boundary.
7. Run focused checks while editing, then one complete non-Docker gate and one
   exact Docker scenario for the finished slice.

## Verification Evidence

- Workspaces domain, application, persistence, catalogue, and API tests:
  143 passed.
- Complete `eng/verify.ps1 -SkipRestore` gate: synchronized solution and
  source-package guards, zero-warning build, clean migration drift, and 3,385
  non-Docker tests passed.
- Exact
  `RetentionControlPlaneIntegrationTests.Scheduler_isolates_tenants_and_converges_after_legal_hold_release`
  PostgreSQL/Worker scenario: 1 passed. It proves tenant isolation, held-tenant
  convergence, Staff retention, Workspaces subject/actor pseudonymisation,
  canonical receipt reload, and database rejection of receipt update/delete.
- GMA framework, modules, and extensions remain unchanged; the existing scope,
  CQRS transaction, Organizations, AccessControl, and task primitives were
  sufficient.

## Deferred

- Approved deletion periods for Workspaces access/audit history.
- Automatic deletion of terminal onboarding rows and access plans.
- Workspaces-specific legal-hold administration beyond the Staff hold that
  gates this exact retention path.
- Data Rights export, correction, restriction, anonymisation, and restore for
  Workspaces-owned history.
- GMA Organizations invitation/enrollment retention activation and hosted
  backup-expiry evidence.
