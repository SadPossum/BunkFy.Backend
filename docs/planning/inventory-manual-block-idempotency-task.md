# Inventory Manual Block Idempotency Task

Status: complete
Date: 2026-08-09

## Goal

Make manual inventory block creation and release safe to retry after a timeout,
lost response, worker restart, or concurrent duplicate delivery without creating
duplicate blocks, repeating events, or losing the original success receipt.

## Current Gap

Block and block-group creation generate their durable identifiers on the server.
An exact retry after a committed but lost response therefore collides with the
newly created block instead of recovering its identity. Single-block release
becomes stale after success, while group release reports that the now-released
group was not found. Concurrent duplicate requests can also race before the
aggregate concurrency fence is reached.

## Ownership

- Inventory owns block invariants, target resolution, overlap checks, mutation
  serialization, request equivalence, immutable receipts, event cardinality,
  and operation-journal lifecycle.
- Public API, Admin API, existing Admin CLI commands, and web callers provide
  one non-empty operation id per logical attempt.
- Properties remains the physical-topology authority. Reservations remains the
  allocation and reservation-workflow authority.
- The implementation remains BunkFy-specific. GMA receives no block concepts
  or generic response-caching middleware.

## Required Semantics

1. Create-block, create-block-group, release-block, and release-block-group
   require a non-empty operation id before mutation work begins.
2. The first successful attempt stores one typed immutable receipt in
   Inventory's management-operation journal in the same transaction as the
   block changes, availability touches, outbox records, and retirement
   advancement.
3. Exact replay returns that receipt before current topology, overlap, block
   status, expected-version, or active-group checks and publishes no new event.
4. Reusing an operation id on the same resource for a different mutation kind,
   target, stay range, normalized reason, expected version, or property returns
   `Inventory.ManagementOperationConflict` and maps to HTTP 409.
5. Failed validation, authorization, missing topology, overlap, allocation
   conflict, stale version, or unavailable target does not bind the operation
   id. The same logical attempt can be retried after the cause is corrected.
6. Creation is keyed by `(tenant, property, operation)` because block and group
   identifiers do not exist before success. Single release is block-scoped and
   group release is group-scoped.
7. Exact concurrent creation is serialized by its operation coordinate.
   Releases in the same group serialize on the durable group coordinate, while
   aggregate and unit concurrency remain the final cross-workflow race guards.
8. Hierarchical targets and reasons are normalized before fingerprinting and
   execution. The journal stores only the SHA-256 fingerprint, never another
   copy of the free-text reason.
9. Group fan-out remains all-or-nothing and records only the generated group id,
   property id, and affected count. It does not duplicate every child id in the
   operation receipt.
10. Journal records remain tenant-scoped, no-store on HTTP surfaces, included
    in tenant export/destruction, and protected by executable database
    constraints.

## Delivery

- [x] Widen the Inventory management-operation model and migration for typed
  block and block-group receipts.
- [x] Add canonical fingerprints, resource locks, replay checks, and receipt
  writes to all four block commands.
- [x] Require operation ids in public API, Admin API, and existing Admin CLI
  block commands with stable 400/409 mappings.
- [x] Preserve create/release operation identity across exact web retries and
  reset it when the property, payload, target, or explicit attempt changes.
- [x] Extend tenant export/destruction metadata and proof for the new receipt
  fields.
- [x] Add focused handler, persistence, API-contract, web, and one consolidated
  PostgreSQL concurrency/migration scenario.
- [x] Run cheap focused tests during implementation, then one Docker gate and
  the canonical verifier at the completed-slice boundary.

## Verification

- 122 focused non-Docker Inventory tests pass.
- The Integration Tests project builds with zero warnings and errors.
- EF Core reports no pending Inventory model changes.
- All 215 web tests, type checking, linting, production build, and generated
  contract verification pass.
- The single consolidated PostgreSQL Docker scenario passes concurrent exact
  create/release replay, changed-request conflict, failed-attempt reuse,
  database receipt constraints, closed-tenant admission, and downgrade refusal.
- The canonical repository verifier passes security and operational policy
  checks, two zero-warning builds, migration drift, all non-Docker backend and
  integration tests, generated contracts, all 215 web tests, lint, typecheck,
  and the production frontend build.

## Not In This Slice

- changing block dates, reasons, or targets after creation;
- scheduled cleanup or retention policy for successful management receipts;
- forced release that bypasses expected versions or active tenant admission;
- redesigning retirement request/retry semantics;
- introducing a generic GMA idempotency abstraction.
