# Staff Onboarding Employment Lifecycle Boundary Task

Status: complete
Date: 2026-08-11

## Goal

Keep accepted workspace onboarding from bypassing Staff employment-lifecycle
authorization. Onboarding may create a missing Staff profile or apply the accepted
profile to an active identity, but it must not resume suspended employment.

## Confirmed Gap

Workspace invitation and enrollment-source management requires `staff.manage`.
The explicit Staff resume route requires the separate
`staff.manage-lifecycle` permission. The Staff onboarding provisioner currently
updates and resumes an existing suspended member under an integration actor, so
an accepted or delayed onboarding flow can restore employment without the
lifecycle permission and undo a later operator suspension.

## Ownership

- Workspaces owns source acceptance, the applicant profile proposal, the access
  plan, and orchestration of Staff, property assignment, and access provisioning.
- Staff owns employment status and is the only module that may decide whether an
  accepted profile can be applied to an existing Staff identity.
- Explicit Staff lifecycle commands and their permissioned API own suspension and
  resumption. Onboarding owns no lifecycle desired state.
- GMA requires no change. Its permission pipeline and transactional command
  primitives already separate the two product capabilities.

## Invariants

- Missing Staff identity may be created by an accepted onboarding operation.
- Existing active Staff identity may receive the accepted profile under the
  existing operation-id, lock, uniqueness, and replay rules.
- Existing suspended Staff identity returns the stable `Staff.StaffSuspended`
  error before any profile mutation, receipt, or integration event.
- Departed, restricted, or anonymised identities retain their existing
  fail-closed behavior.
- An explicit successful Staff resume followed by retry of the same onboarding
  operation may proceed; onboarding itself never performs the resume.
- Workspaces must not assign properties or grant access when Staff provisioning
  fails.
- The public Staff onboarding contract carries no lifecycle reason or desired
  employment state.

## Delivery

1. Remove the lifecycle reason from the Staff onboarding Contracts request and
   internal command.
2. Reject suspended existing identities before applying applicant profile data;
   retain create, active update, exact replay, and conflict semantics.
3. Replace the resume-positive test with denial, immutability, and explicit-resume
   retry proof; keep Workspaces fail-before-access coverage aligned.
4. Refresh the executable Staff personal-data catalog and deterministic inventory.

## Verification Cadence

- Use focused Staff and Workspaces tests while editing.
- Run the complete Staff and Workspaces suites, architecture guards, solution
  graph check, and one zero-warning solution build at the slice boundary.
- No migration or Docker rerun is needed because persistence shape, lock SQL, and
  operation-journal schema do not change.

## Outcome

- Staff onboarding now creates missing identities and updates only active
  identities. Suspended identities fail with `Staff.StaffSuspended` before any
  profile mutation, operation receipt, or integration event.
- Explicit lifecycle resumption remains owned by the permissioned Staff command.
  Retrying the same onboarding operation after an explicit resume then applies
  the accepted profile through the existing lock, replay, and uniqueness rules.
- The Staff Contracts request and internal command no longer carry a lifecycle
  reason, so callers cannot imply lifecycle authority through profile
  provisioning.
- Workspaces preserves the exact Staff failure code and stops before property or
  access assignment when provisioning is denied.
- Staff personal-data metadata and the deterministic inventory now agree on
  catalog version 17; the removed lifecycle reason has no residual binding.
- GMA required no change because the framework permission and transactional
  primitives already support the product-owned boundary.

## Evidence

- Focused Staff onboarding and personal-data tests: 10 passed.
- Focused Workspaces failure and accepted-event ordering tests: 3 passed.
- Staff module suite: 244 passed.
- Workspaces module suite: 353 passed.
- Architecture suite: 102 passed.
- `dotnet build BunkFy.slnx --no-restore -m:1`: succeeded with 0 warnings and
  0 errors.
- `eng/update-solutions.ps1 -Check`: backend solution synchronized.
- `git diff --check`: passed with only existing line-ending normalization
  notices for the two touched Workspaces files.
- Docker and migration checks were intentionally not repeated: persistence
  shape, provider SQL, locking, and operation-journal schema are unchanged.

## Deferred

- Public multi-account invitation, QR/link, provider redirect, broker delivery,
  and process-restart evidence remains the workspace-onboarding deployment gate.
