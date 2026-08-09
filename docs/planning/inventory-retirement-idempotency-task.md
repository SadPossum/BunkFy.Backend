# Inventory Retirement Idempotency Task

Status: complete
Date: 2026-08-09

## Goal

Make room and bed retirement requests and rejected-process retries safe across
timeouts, lost responses, concurrent staff actions, and worker restarts without
duplicating processes, finalization requests, or topology events.

## Current Gap

Retirement requests generate their process identifiers on the server and do not
accept an operation id. They return any existing process for the target even when
the new reason differs, so changed intent can appear to succeed while being
ignored. Retry commands have neither an operation id nor an expected process
version; a committed retry followed by a lost response therefore comes back as
an invalid retry. These paths also bypass Inventory's management locks.

## Ownership

- Inventory owns retirement intent, request equivalence, drain state, impact
  projection, retry admission, mutation serialization, journal pointers, and
  finalization coordination.
- Properties remains the physical-topology authority and finalizes only from a
  correlated Inventory request. Reservations remains the reservation and staff
  reassignment authority.
- Public API, Admin API, existing Admin CLI commands, and web callers provide one
  non-empty operation id per logical request or retry attempt.
- The implementation remains BunkFy-specific. GMA receives no retirement,
  response-caching, or generic process-manager concepts.

## Required Semantics

1. Bed request, room request, bed retry, and room retry require a non-empty
   operation id before mutation work begins.
2. A retry also requires the rejected process version observed by the caller.
   A stale version returns the existing stable version-conflict result.
3. Request fingerprints include property, target coordinates, and normalized
   reason. Retry fingerprints include property, topology-change id, and expected
   version. Actor identity remains on the process but is not duplicated or hashed
   in the journal.
4. A successful operation records a compact immutable pointer containing the
   topology-change id and resulting process version in the same transaction as
   the process mutation, availability touches, definitions, and outbox records.
5. Exact replay resolves the stored topology-change id and returns a fresh,
   bounded process view. It publishes no new event. The returned state may be
   newer than the state at the original response because coordinators can advance
   a process asynchronously.
6. Reusing an operation id on the same resource for another kind, target,
   normalized reason, expected version, or property returns
   `Inventory.ManagementOperationConflict` and maps to HTTP 409.
7. Failed validation, authorization, missing topology, active conflicting drain,
   active claims, stale version, or invalid state does not bind an operation id.
8. A new operation with the same normalized request may adopt the existing
   durable process and store a pointer to it. A changed reason returns a stable
   retirement-request conflict instead of silently ignoring the new intent.
9. Requests serialize on the room resource fence. Retries serialize first on the
   process coordinate and then on the room fence; no path acquires those locks in
   the reverse order.
10. PostgreSQL optimistic concurrency remains the final guard against automatic
    coordinator and integration-event races outside the operator lock path.
11. Journal records remain tenant-scoped, no-store on HTTP surfaces, included in
    tenant export/destruction, and protected by executable database constraints.

## Delivery

- [x] Extend Inventory's management-operation model with retirement kinds,
  resource coordinates, and a compact topology-change result pointer.
- [x] Add canonical request fingerprints, request adoption/conflict checks,
  process and room locks, expected-version retries, and exact replay to all four
  command handlers.
- [x] Require operation ids and retry versions in public API, Admin API, and the
  existing Admin CLI with stable 400/409 mappings.
- [x] Preserve operation identity across exact web retries and add an explicit
  rejected-retirement retry action that resumes outcome polling.
- [x] Extend tenant export/destruction metadata and proof for the journal field.
- [x] Add focused handler, persistence, API-contract, web, and one consolidated
  PostgreSQL concurrency/migration scenario.
- [x] Run cheap focused tests during implementation, then one Docker gate and the
  canonical verifier at the completed-slice boundary.

## Verification

- The root canonical non-Docker verifier passed on 2026-08-09, including
  zero-warning workspace builds, migration drift, 130 Inventory tests, 54
  non-Docker integration tests, 94 architecture tests, 218 web tests, lint,
  generated-contract drift, and the production frontend build.
- The single end-of-slice Docker gate passed all 112 container-backed integration
  tests in 12 minutes 54 seconds. The Inventory workflow proves two concurrent
  PostgreSQL callers with the same operation id converge on one retirement
  process and one durable topology-change receipt.

## Not In This Slice

- changing a retirement reason or target after process creation;
- canceling or force-completing an active retirement;
- storing full mutable process DTOs in the operation journal;
- changing automatic drain advancement or Properties finalization contracts;
- introducing a generic GMA idempotency or process-manager abstraction.
