# Staff Identity Bootstrap Replay Envelope Task

Status: complete
Date: 2026-08-11

## Goal

Keep workspace-owner Staff bootstrap replay-safe when a delayed duplicate carries
stale or malformed profile data. A recognized bootstrap operation or existing
Auth identity must remain a successful no-op without allowing invalid payloads to
create a new Staff member.

## Confirmed Gap

The command validator and handler currently validate display name, email, and
actor before Staff can inspect the source operation or existing Auth identity.
Those fields are mutation data, not replay-routing data. A duplicate event can
therefore be rejected even though bootstrap owns no update semantics and should
acknowledge the existing identity without reading that stale payload.

## Ownership

- Staff owns the bootstrap replay envelope, creation validation, source-operation
  serialization, identity lookup, and no-op semantics.
- GMA requires no change. Validation-before-dispatch and transactional retry are
  correct generic behavior; payload relevance during replay is domain-specific.

## Invariants

- An enabled tenant, non-empty source operation id, and normalized non-empty Auth
  subject are required before any replay lookup.
- The source-operation lock is acquired before checking operation ownership and
  Auth-subject existence.
- Replaying an operation for the same normalized Auth subject succeeds even when
  display name, email, or actor data is stale or malformed.
- Reusing an operation for a different Auth subject remains a conflict.
- Any existing Staff identity for the Auth subject remains a successful no-op.
- A new identity still requires a valid Staff profile and actor and fails without
  persistence when either is invalid.

## Delivery

1. Restrict command-pipeline validation to the bootstrap routing envelope.
2. Normalize and validate the required Auth subject before the creation lock.
3. Resolve operation and identity no-ops before constructing mutation-only value
   objects, then retain full domain validation for new creation.
4. Prove validator, exact replay, existing identity, conflicting operation, and
   fresh invalid-creation behavior with focused tests.

## Verification Cadence

- Run focused Staff tests while editing.
- Run the complete affected suites, architecture guards, solution synchronization,
  and one solution build at the slice boundary.
- No migration or Docker matrix is required because persistence shape and SQL do
  not change.

## Outcome

- Command-pipeline validation now admits only a valid source operation id and
  required bounded Auth subject. Mutation-only profile and actor fields no longer
  poison a replay before Staff can resolve it.
- The handler normalizes the Auth subject before acquiring the creation lock,
  resolves exact-operation and existing-identity no-ops, and only then constructs
  the Staff profile and actor required for new creation.
- Exact replay uses the normalized Auth subject, while changed-subject operation
  reuse remains a conflict and new identities retain the complete domain guards.
- The possible Workspaces verified-email/display-name mismatch was rechecked and
  requires no code change: current GMA Auth usernames and Staff display names are
  both bounded to 256 characters.
- GMA remained unchanged because this ordering is Staff bootstrap semantics, not
  generic command-dispatch behavior.

## Evidence

- Focused Staff identity-bootstrap tests: 9 passed.
- Complete Staff module suite: 253 passed.
- Architecture suite: 102 passed.
- `pwsh -NoProfile -File eng/update-solutions.ps1 -Check`: backend solution and
  workspace graph are synchronized.
- `dotnet build BunkFy.slnx --no-restore -m:1 --verbosity minimal`: succeeded
  with 0 warnings and 0 errors.
- `git diff --check`: passed.
- No migration or Docker matrix was run because persistence shape and SQL were
  unchanged.
