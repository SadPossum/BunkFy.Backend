# Ingestion Connection Creation Idempotency Task

Status: completed
Date: 2026-08-09

## Goal

Make adapter-connection creation safe across client retries, lost responses,
concurrent submissions, and process restarts without creating duplicate source
control planes or silently accepting changed connection intent.

## Current Gap

Connection creation generates its identifier on the server and has no
caller-owned operation identity. If the database commit succeeds but the
response is lost, the caller cannot distinguish success from failure and a
retry creates another enabled connection for the same external source.

Ingestion already serializes connection and run mutations with transaction-key
locks, commits through its tenant admission boundary, and returns a bounded
connection mutation receipt. Creation bypasses that connection lock because no
stable connection coordinate exists until the handler generates one.

## Ownership

- Ingestion owns connection request equivalence, connection identity,
  connection-scoped serialization, durable operation receipts, and adapter
  capability admission.
- Properties remains the property and governance-policy authority. Ingestion
  continues to use its local Properties projection and country-policy
  admission boundary.
- Public API, Admin API, Admin CLI, and web callers provide one non-empty
  operation id for each logical creation attempt.
- GMA continues to own generic transactions, transaction-key locking,
  optimistic concurrency, and outbox behavior. Adapter-connection retry
  semantics remain BunkFy-specific.

## Required Semantics

1. `OperationId` is required and becomes the successful `ConnectionId`.
2. Scope, tenant lifecycle, country policy, adapter capability, and normalized
   connection details are validated on every attempt, including replay.
3. The normalized request fingerprint includes property, adapter type,
   execution mode, conflict policy, configuration reference, and optional
   secret reference. The journal stores only the digest, never either raw
   reference.
4. Creation acquires the existing exclusive connection transaction key before
   reading or adding the aggregate.
5. The connection and one append-only connection-management operation receipt
   commit in the same Ingestion transaction.
6. An exact completed replay returns the current bounded mutation receipt and
   emits no second connection or event.
7. Reusing an operation id for changed normalized input, another mutation kind,
   a legacy connection without a matching operation receipt, or another
   resource returns `Ingestion.ConnectionManagementOperationConflict` and maps
   to HTTP 409.
8. Invalid, denied, unsupported, or otherwise failed attempts do not bind the
   operation id and may be retried after the cause is corrected.
9. The journal is tenant-scoped, append-only, exported with the tenant owner
   graph, removed explicitly before its parent connection during tenant
   destruction, and protected by executable database constraints. Portable
   export includes receipt coordinates and outcome metadata but omits the
   request digest because it covers a non-exportable secret reference.
10. The browser retains one operation id while normalized form values are
    unchanged and rotates it after a value change, cancellation, or success.

## Delivery

- [x] Add the Ingestion connection-management operation model, repository,
  transaction-safe persistence, migration, export metadata, and privacy
  classification.
- [x] Require operation ids in connection-create application, public API,
  Admin API, and Admin CLI surfaces with stable 400/409 behavior.
- [x] Add exact replay and changed-request conflict handling under the existing
  connection mutation lock.
- [x] Preserve operation identity across exact web retries and rotate it when
  creation intent changes.
- [x] Add focused handler, persistence, API-contract, web, and one consolidated
  PostgreSQL concurrency scenario.
- [x] Run cheap focused checks while editing, then one canonical non-Docker gate
  and one Docker gate at the completed-slice boundary.

## Verification

- Backend `eng/verify.ps1 -SkipRestore`: passed, including build, migration
  drift, architecture, module, extension, and non-Docker integration tests.
- Web `pnpm contracts:check` and `pnpm verify`: passed with 225 tests and a
  production build.
- Backend `eng/test-docker.ps1 -NoBuild`: passed all 112 Docker integration
  tests.

## Not In This Slice

- retry safety for connection update, enable/disable, polling schedule, or
  checkpoint reset operations;
- one-time ingress credential issuance, credential revocation, legal holds,
  proposal decisions, or TaskRuntime controls;
- automatic duplicate-source detection across distinct operation ids;
- changing the adapter boundary or extracting a generic GMA idempotency
  framework.
