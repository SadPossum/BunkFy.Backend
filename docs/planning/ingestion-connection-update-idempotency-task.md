# Ingestion Connection Update Idempotency Task

Status: completed
Date: 2026-08-09

## Goal

Make adapter-connection settings updates safe across lost responses,
concurrent retries, and process restarts. One logical update must produce at
most one connection version and event, while changed reuse of its operation id
fails closed.

## Audit Finding

Connection updates already run in an Ingestion transaction, serialize on the
connection write coordinate, enforce optimistic versions, validate adapter
capabilities, and avoid a version change when normalized settings are already
current. They have no caller-owned operation identity. If an update commits but
its response is lost, an exact retry reaches optimistic concurrency and fails.

The connection-management operation journal introduced for creation is scoped,
append-only, exported without request digests, and destroyed before its parent
connection. It intentionally has a mutation kind discriminator and can own this
workflow without another table or a generic framework abstraction.

## Ownership

- Ingestion owns update equivalence, secret-reference update intent, operation
  receipts, connection serialization, adapter capability admission, and
  country-policy admission.
- Public API, Admin API, Admin CLI, and web callers provide one non-empty
  operation id for each logical settings update.
- Properties remains the property and country-policy authority through the
  existing Ingestion projection and admission boundary.
- Workspaces remains the tenant lifecycle authority through the existing
  Ingestion lifecycle policy contract.
- GMA continues to own transactions, transaction-key locking, optimistic
  concurrency, and outbox behavior. No GMA change belongs in this slice.

## Required Semantics

1. The idempotency coordinate is `(ScopeId, ConnectionId, OperationId)`.
2. Scope, operation id, tenant lifecycle, country policy, property ownership,
   adapter capability, conflict policy, and secret update shape are validated
   on every attempt, including replay.
3. Exact equivalence includes mutation kind, property and connection
   coordinates, expected version, execution mode, conflict policy, normalized
   configuration reference, secret update mode, and the normalized replacement
   reference only when replacement is requested.
4. `Keep` is fingerprinted as caller intent and never resolved against the
   connection's mutable current secret reference. `Clear` contains no reference.
5. The journal stores only the canonical SHA-256 digest, never a raw
   configuration or secret reference. Portable export continues to omit the
   digest because a secret reference is not exportable.
6. Update acquires the existing exclusive connection transaction key before
   reading the aggregate or operation receipt.
7. An exact completed replay returns the current bounded connection receipt and
   emits no second mutation or event.
8. Reusing an operation id on the same connection for changed normalized input,
   another expected version, or another mutation kind returns
   `Ingestion.ConnectionManagementOperationConflict` and maps to HTTP 409.
9. Invalid, denied, unsupported, missing, stale, or otherwise failed attempts do
   not bind the operation id and may be retried after the cause is corrected.
10. A valid request whose normalized settings already match current state stores
    one successful no-change receipt without advancing the connection version
    or publishing an event.
11. A material update and its append-only operation receipt commit in the same
    Ingestion transaction and outbox boundary.
12. The browser retains one operation id while the target, original expected
    version, and normalized edit intent are unchanged, and rotates it after an
    intent change, cancellation, target change, or success.

## Delivery

- [x] Add `ConnectionUpdate` to the existing operation model and widen its
  executable database constraints with an additive schema migration.
- [x] Require operation ids in update application, public API, Admin API, and
  Admin CLI surfaces with stable 400/409 behavior.
- [x] Add admission-before-replay, exact replay, changed-request conflict, and
  no-change handling under the existing connection lock.
- [x] Preserve operation identity across exact web retries and rotate it when
  update intent changes.
- [x] Add focused handler, persistence, contract, web, and PostgreSQL
  concurrency coverage.
- [x] Run cheap focused checks while editing, then one consolidated non-Docker
  gate and one Docker gate at the completed-slice boundary.

## Verification

- `eng/verify.ps1 -SkipRestore` passed the synchronized solution and
  source-package guards, a zero-warning full build, every configured migration
  drift check, and all non-Docker backend suites. The focused Ingestion suite
  passed 325 tests; architecture passed 94; Operations Notifications passed 99.
- `pnpm contracts:check` and `pnpm verify` passed. The web suite passed 235 tests
  across 45 files, and the production build completed.
- `eng/test-docker.ps1 -NoBuild` passed all 112 container-backed integration
  tests, including legacy-schema migration and concurrent exact update replay.

## Not In This Slice

- enable/disable, polling-schedule, or checkpoint-reset replay safety;
- ingress credential issuance or revocation replay safety;
- run, proposal, legal-hold, reprocessing, or retention control replay safety;
- duplicate-source detection across distinct operation ids;
- a generic GMA idempotency package or response cache.
