# Properties Management Create Idempotency Task

Status: complete
Date: 2026-08-07

## Goal

Make property creation safe to retry after an uncertain response without
creating duplicate topology or acknowledging different property details as the
same operation.

## Audit Finding

Properties already commits each property and its outbox event atomically,
serializes property-code uniqueness, and provisions a durable per-property
mutation lock. Creation still generates the property id on the server and has
no caller-owned operation identity. A response lost after commit therefore
turns an exact retry into a duplicate-code conflict, while concurrent retries
cannot converge on one result.

Workspace termination admission is currently enforced when Properties saves a
mutation. A completed creation replay has no write to save, so replay must
explicitly pass the same admission boundary before its receipt is disclosed.

## Ownership

- Properties owns creation request equivalence, aggregate identity, current
  mutation receipts, code uniqueness, and operation serialization.
- Public API, Admin API, Admin CLI, and web callers supply one non-empty
  operation id for each logical property creation attempt.
- Workspaces remains the authority for operational admission. Properties reads
  its existing termination fence contract and does not query Workspace tables.
- GMA continues to own generic transactions, transaction-key locking,
  optimistic concurrency, and outbox behavior. Property-specific retry
  semantics remain in BunkFy.

## Invariants

1. `OperationId` is the canonical `PropertyId` for a successful creation.
2. Every attempt validates and normalizes property details before acquiring
   persistence locks.
3. Every attempt rechecks workspace operational admission inside the active
   Properties transaction, including completed replays.
4. The tenant-scoped creation-operation coordinate is acquired before reading
   an existing property with the operation id.
5. A new operation then acquires the normalized property-code coordinate before
   checking uniqueness and adding the aggregate.
6. Request equivalence includes normalized name, code, and time-zone id.
7. An exact completed replay returns the current minimal property mutation
   receipt and emits no second event.
8. Reusing an operation id for changed property details returns one stable
   conflict mapped to HTTP 409.
9. Invalid, duplicate-code, admission-denied, or failed attempts do not persist
   operation state and may be retried after the cause is corrected.
10. The property, its operation-lock row, and its single creation outbox event
    commit atomically in the Properties transaction.
11. Creation operation locks are transaction-scoped and retain no request
    payload or additional personal data.

## Surfaces

- Add `OperationId` to `CreatePropertyCommand` and public/Admin create request
  contracts.
- Require `--operation-id` for Admin CLI property creation.
- Keep one browser operation id while normalized creation values are unchanged;
  allocate a new id after cancellation, successful creation, or value changes.
- Add Properties application errors for an invalid creation operation and
  conflicting operation reuse.
- Keep the existing `PropertyMutationReceiptDto` response and refetch workflow.

## Domain And Persistence

Extract normalized property details into a domain value object shared by
creation, update, and replay comparison. A dedicated Properties creation-lock
port will use GMA's existing transaction-key primitive after acquiring the
module's workspace-admission lock. The aggregate's existing operation-lock row
continues to be provisioned with the property. No schema migration or durable
request journal is required because the operation id is the aggregate id.

## Verification

- Focused domain and application tests cover normalization, invalid operation
  ids, lock ordering, exact replay, changed reuse, duplicate-code failure reuse,
  and one aggregate/event creation.
- Public API, Admin API, Admin CLI, generated contract, and web tests cover the
  required operation id and stable browser attempts.
- Persistence tests cover valid and invalid creation-lock coordinates and
  admission behavior.
- One focused PostgreSQL scenario proves concurrent exact retries converge on
  one property, one operation-lock row, and one creation outbox event.
- Use cheap focused checks while editing. Run one consolidated non-Docker slice
  gate and one targeted Docker scenario at completion.

## Not In This Slice

- property update, processing, or retirement replay safety;
- room or bed creation and lifecycle replay safety;
- changing property-code or topology projection contracts;
- generic GMA idempotency, response caching, or a cross-module request journal.

## Completion Evidence

- Properties focused tests passed (`21/21`), including domain normalization,
  handler replay/conflict behavior, and creation-lock admission.
- The focused PostgreSQL concurrency scenario passed (`1/1`) with one
  property, one operation-lock row, and one creation outbox event.
- The consolidated backend verification gate passed, including a clean build,
  migration-drift checks, architecture guards, `122` Properties tests, and all
  non-Docker integration tests.
- The web verification gate passed type checking, linting, `199` tests, and the
  production build.
- The OpenAPI snapshot and generated TypeScript contracts are current.
