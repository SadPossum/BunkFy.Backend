# Ingestion Credential Mutation Idempotency Task

Status: complete
Date: 2026-08-09

## Goal

Make adapter ingress credential issuance and revocation safe across lost
responses, concurrent retries, and process restarts without persisting
recoverable bearer-token material.

## Audit Finding

Credential creation currently generates a fresh credential id and token on
every call. A response lost after commit therefore leaves an unknown active
credential and an ordinary retry consumes another bounded slot. Revocation is
optimistically versioned but an exact retry becomes an already-revoked or
version error. Neither mutation currently applies the workspace lifecycle
admission used by other connection-management operations.

The one-time token makes ordinary response replay intentionally impossible:
Ingestion stores only its SHA-256 digest. GMA Organizations already establishes
the safe reusable semantic for one-time invitation issuance: a caller-owned
source id prevents duplicate issuance, while an exact replay reports that the
resource already exists without replaying the token. Adapter credentials should
use the same semantic, not add plaintext/encrypted token retention or generic
response caching.

## Ownership

- Ingestion owns credential identity, token generation and digest storage,
  issuance equivalence, revocation receipts, connection serialization, active
  slot limits, descriptor admission, and lifecycle admission.
- Public API, Admin API, Admin CLI, and web callers provide one non-empty
  operation id for each logical issuance or revocation attempt.
- Issuance and revocation record their operations in the existing
  connection-management operation journal because credentials are
  connection-owned children. The issuance operation id is also the credential
  id.
- GMA continues to own transaction execution, transaction-key locking,
  optimistic concurrency, and outbox behavior. Its Organizations one-time
  issuance semantics are reused as a design precedent; no GMA code changes
  belong in this slice.

## Required Semantics

1. Issuance requires a non-empty operation id distinct from the parent
   connection id and uses it as the credential id.
2. Scope, workspace lifecycle, connection ownership, push-capable execution
   mode, and current adapter descriptor are checked on every attempt, including
   replay.
3. The first valid issuance generates one high-entropy token, stores only its
   digest, and returns outcome Issued with the token under no-store response
   policy. The credential and issuance receipt commit atomically.
4. An exact issuance retry matches normalized label, optional/default expiry
   semantics, source system, actor, connection, and current descriptor
   coordinates. It returns outcome AlreadyIssued with current bounded
   credential metadata and no token.
5. Changed reuse of an issuance operation id fails with the stable connection
   management operation conflict and never creates another credential.
6. Failed validation, lifecycle admission, capability checks, or slot
   allocation do not bind a new operation id.
7. If an initial issuance response is uncertain, callers must treat the token
   as unavailable, revoke the visible credential, and issue a replacement with
   a new operation id. No endpoint, log, journal, or retry reconstructs it.
8. Revocation equivalence includes operation kind, property, connection,
   credential, expected credential version, and normalized actor.
9. Revocation acquires the existing exclusive connection coordinate before
   reading the credential or operation receipt.
10. An exact completed revocation retry returns the current bounded credential
    receipt without another state transition. Changed reuse conflicts.
11. A new already-revoked or stale revocation keeps its existing state/version
    failure; only an exact completed replay bypasses it.
12. Credential revocation and its operation receipt commit in the same
    Ingestion transaction.
13. Browser callers retain operation identity while target and normalized
    intent are unchanged, rotate it after intent change, cancellation, or
    success, and render the AlreadyIssued/no-token recovery honestly.

## Delivery

- [x] Add issuance outcome and nullable-token contracts without exposing token
  material through list, detail, or operation-journal surfaces.
- [x] Add operation ids and lifecycle admission to issuance and revocation.
- [x] Make issuance identity-based and revocation journal-backed with one
  additive constraint migration.
- [x] Align public API, Admin API, Admin CLI, browser retry attempts, and
  generated contracts.
- [x] Add focused handler, domain/equivalence, persistence, contract, browser,
  and PostgreSQL concurrency coverage.
- [x] Run cheap focused checks while editing, then one consolidated non-Docker
  gate and one Docker gate at the completed-slice boundary.

## Verification

- `eng/verify.ps1 -SkipRestore` passed, including solution synchronization,
  migration drift, architecture checks, and all non-Docker tests.
- Web contract drift and `pnpm verify` passed with 251 tests and a production
  build.
- `eng/test-docker.ps1 -NoBuild` passed all 112 PostgreSQL, messaging, storage,
  adapter, and end-to-end integration tests after rebuilding the integration
  test output.
- The final descriptor-coordinate fingerprint hardening passed all 336
  Ingestion unit tests and all 94 architecture tests; it does not change the
  persistence or transaction path covered by the Docker gate.

## Not In This Slice

- replaying, encrypting, caching, or otherwise recovering a one-time token;
- credential secret rotation in place;
- generic GMA idempotency middleware or response storage;
- adapter workload identity or federated credentials.
