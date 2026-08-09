# Ingestion Connection Control Idempotency Task

Status: completed
Date: 2026-08-09

## Goal

Make connection enable, disable, polling-schedule configuration and clearing,
and checkpoint reset safe across lost responses, concurrent retries, and
process restarts. One logical control action must produce at most one committed
state transition while changed reuse of its operation id fails closed.

## Audit Finding

These controls already execute in an Ingestion transaction, acquire the
connection write coordinate, enforce aggregate versions, and preserve adapter
run consistency. They do not have caller-owned operation identities or tenant
lifecycle admission. A response lost after commit therefore turns an exact
retry into a version or state error. A disable retry can also inspect and mutate
a later linked remote run before discovering that the connection is already
disabled.

The existing connection-management operation journal is scoped, append-only,
exported without request digests, and destroyed before its parent connection.
Its mutation discriminator is the correct owner for these receipts; another
table or a generic framework abstraction would add no useful boundary.

## Ownership

- Ingestion owns control equivalence, operation receipts, connection and run
  serialization, adapter capability admission, country-policy admission, and
  tenant-lifecycle admission at its contract boundary.
- Public API, Admin API, Admin CLI, and web callers provide one non-empty
  operation id for each logical control attempt.
- Properties remains the country-policy and active-property projection
  authority. Enabling continues to require current country-policy admission;
  disabling, schedule maintenance, and checkpoint reset do not start ingestion
  and do not add a country-policy requirement.
- Workspaces remains the tenant lifecycle authority through the existing
  Ingestion lifecycle policy contract.
- GMA continues to own transaction execution, transaction-key locking,
  optimistic concurrency, and outbox behavior. No GMA change belongs in this
  slice.

## Required Semantics

1. The idempotency coordinate is `(ScopeId, ConnectionId, OperationId)`.
2. Enable, disable, polling-schedule configure, polling-schedule clear, and
   checkpoint reset have distinct persisted mutation kinds.
3. Scope, operation id, and tenant lifecycle are validated on every attempt,
   including replay. The existing `ConnectionProvisioning` lifecycle operation
   covers regular connection management; tenant termination owns its separate
   cleanup path.
4. Enabling revalidates country policy on every attempt, including replay.
   Schedule configuration revalidates adapter capability and its provider
   minimum on every attempt, including replay.
5. Exact equivalence includes mutation kind, property and connection
   coordinates, and expected version. Schedule configuration additionally
   includes interval seconds and maximum attempts.
6. Fingerprints are versioned, length-delimited SHA-256 digests. The journal
   stores no raw settings, checkpoint, secret, or other caller payload.
7. Every control acquires the existing exclusive connection transaction key
   before reading the aggregate or operation receipt.
8. An exact completed replay returns the current bounded connection receipt and
   emits no second mutation or event.
9. Reusing an operation id on the same connection for another action, changed
   input, or another expected version returns
   `Ingestion.ConnectionManagementOperationConflict` and maps to HTTP 409.
10. Invalid, denied, unsupported, missing, stale, or otherwise failed attempts
    do not bind the operation id and may be retried after the cause is corrected.
11. Valid schedule configure, schedule clear, or checkpoint reset requests that
    are already satisfied store one successful no-change receipt without
    advancing the connection version or publishing an event.
12. Enable and disable preserve their existing new-attempt state errors. Only an
    exact completed replay bypasses the resulting already-enabled or
    already-disabled state.
13. Disable checks for an existing operation receipt before inspecting or
    cancelling a linked remote run. A new disable still cancels a running remote
    lease and disables the connection atomically under the existing locks.
14. Each state transition and its operation receipt commit in the same
    Ingestion transaction and outbox boundary.
15. Browser callers retain one operation id while target, action, expected
    version, and normalized schedule input are unchanged, and rotate it after an
    intent change, cancellation, target change, or success.

## Delivery

- [x] Add the five control kinds to the existing operation model and widen its
  executable database constraints with one additive schema migration.
- [x] Require operation ids in application, public API, Admin API, and Admin CLI
  surfaces with stable 400/409 behavior.
- [x] Add admission-before-replay, exact replay, changed-request conflict,
  no-change receipt, and disable-before-run-touch ordering.
- [x] Preserve operation identity across exact browser retries and rotate it
  when intent changes.
- [x] Add focused handler, persistence, contract, web, and PostgreSQL
  concurrency coverage.
- [x] Run cheap focused checks while editing, then one consolidated non-Docker
  gate and one Docker gate at the completed-slice boundary.

## Verification

- `pwsh eng/verify.ps1 -SkipRestore`: synchronized solution and source-package
  checks, zero-warning full build, migration drift, all non-Docker tests,
  including 331 Ingestion, 94 architecture, and 54 integration tests.
- `pnpm contracts:check`: OpenAPI snapshot and generated browser contracts are
  current.
- `pnpm verify`: typecheck, lint, 238 tests across 46 files, and production
  build passed.
- `pwsh eng/test-docker.ps1 -NoBuild`: 112 PostgreSQL, MinIO, NATS, and
  end-to-end integration tests passed.

## Not In This Slice

- adapter ingress credential issuance or revocation replay safety;
- run, proposal, legal-hold, reprocessing, or retention control replay safety;
- duplicate-source detection across distinct operation ids;
- a generic GMA idempotency package or response cache.
