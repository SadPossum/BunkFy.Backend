# Staff Onboarding Provisioning Idempotency Task

Status: complete
Date: 2026-08-11

Current lifecycle ownership was tightened by
[Staff Onboarding Employment Lifecycle Boundary](staff-onboarding-employment-lifecycle-boundary-task.md).
Onboarding now rejects an existing suspended Staff identity and requires an
explicit permissioned resume before the same operation can be retried.

## Goal

Make the Staff side of accepted workspace onboarding safe across independent
Staff and Workspaces commits. Retrying an accepted application must finish the
same logical provisioning operation without overwriting newer Staff profile
changes or undoing a later employment-safety transition.

## Confirmed Gap

`WorkspaceStaffOnboardingProcessor` currently retries Staff provisioning only
while its local application has no `StaffMemberId`. If Staff commits and the
Workspaces transaction then fails, the next retry sends the original applicant
profile again. The current Staff handler treats that delivery as a fresh
profile update and resumes a suspended member, so a replay can overwrite newer
operator changes or reverse a later suspension.

## Ownership

- Workspaces owns the onboarding application and supplies its stable
  application id as the provisioning operation id.
- Staff owns profile normalization, subject uniqueness, employment lifecycle,
  operation serialization, durable replay evidence, and replay admission.
- The existing Staff member-mutation journal owns the durable receipt. Its
  existing data-rights export, retention deletion, and tenant-destruction
  paths continue to own the receipt lifecycle.
- GMA remains unchanged. Its transactional command pipeline and EF transaction
  key lock are sufficient generic primitives; the replay policy is BunkFy
  Staff semantics.

## Invariants

- An onboarding provisioning request requires a non-empty operation id.
- The operation id is unique per workspace scope for onboarding mutations and
  is bound to a versioned fingerprint of the normalized Staff profile,
  including the Auth subject.
- The operation lock is acquired before receipt or Staff-member lookup.
- A first operation may create a missing member or update an existing active
  member. A suspended member fails before profile mutation or receipt creation.
- The successful Staff mutation and onboarding receipt commit atomically.
- An exact replay never reapplies profile fields and never increments the Staff
  version.
- An exact replay succeeds only while the recorded member is still
  operational, linked to the same Auth subject, and active. Suspension,
  departure, anonymisation, processing restriction, or account relinking fails
  closed.
- Reusing an operation id with different normalized applicant data returns a
  stable conflict and cannot mutate another member.
- Distinct onboarding operations retain the existing subject-uniqueness and
  member-mutation serialization rules.

## Implementation

1. Add the stable operation id to the Staff onboarding Contracts request and
   internal transactional command; Workspaces passes the application id.
2. Add an `OnboardingProvision` Staff mutation kind, canonical fingerprint,
   operation-level lookup, and a PostgreSQL partial unique index over
   `(ScopeId, Id)` for that kind only.
3. Acquire the existing Staff creation-operation lock by onboarding operation
   id, resolve exact replay before profile mutation, and append the receipt in
   the first successful transaction.
4. Keep all pre-existing mutation-operation semantics unchanged and refresh
   the Staff PostgreSQL migration snapshot plus generated personal-data
   inventories.
5. Prove exact replay, changed-payload conflict, suspended/departed/restricted
   replay denial, first-operation suspended denial, Workspaces operation
   propagation, and PostgreSQL concurrency/migration behavior.

## Verification Cadence

- Use focused Staff and Workspaces tests while editing.
- Generate and inspect the migration once the model is stable.
- Run one targeted PostgreSQL container gate, the complete Staff and Workspaces
  module suites, architecture guards, migration drift, and the solution build
  at the slice boundary.
- Do not repeat the full Docker or hosted CI gates after each code edit.

## Outcome

- Workspaces now uses the stable onboarding application id as the Staff
  provisioning operation id through the Staff Contracts boundary.
- Staff serializes each onboarding operation before lookup and records the
  result in its existing member-mutation journal in the same transaction as the
  member mutation. No second receipt store or data-lifecycle owner was added.
- Exact replay is observational: it returns the current Staff projection without
  restoring the applicant profile or advancing the member version.
- Changed applicant data conflicts, while suspension, departure, processing
  restriction, anonymisation, or Auth-subject relinking makes replay unavailable.
- A first operation against an existing suspended member now fails before
  mutation. Only the explicit Staff lifecycle command may resume employment;
  later employment-safety transitions are never undone by onboarding.
- GMA required no change because the generic transactional command and lock
  primitives already cover the infrastructure concern.

## Evidence

- `dotnet build BunkFy.slnx --no-restore -m:1`: succeeded with 0 warnings and
  0 errors.
- Staff module suite: 243 passed.
- Workspaces module suite: 352 passed.
- Architecture suite: 102 passed.
- Migration drift: all configured PostgreSQL and GMA provider projects passed.
- Targeted PostgreSQL migration, concurrency, replay, isolation, and immutability
  test: 1 passed.

## Deferred

- The deployed multi-account invitation and QR/link lifecycle remains a
  private release gate requiring public HTTPS, real account/provider redirects,
  broker delivery, and process-restart evidence.
