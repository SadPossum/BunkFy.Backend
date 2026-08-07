# Properties Details Update Idempotency Task

Status: complete
Date: 2026-08-07

## Goal

Make property name, code, and time-zone updates safe to retry after an
uncertain response. One logical update must produce at most one property
version and event, while changed reuse of its operation id fails closed.

## Audit Finding

Properties already serializes each aggregate mutation, enforces optimistic
versions, commits outbox events atomically, and locks normalized property-code
coordinates. An update still has no caller-owned operation identity. If its
transaction commits and the response is lost, an exact retry reports a version
conflict. Submitting details that already match the property also advances its
version and publishes a redundant update event.

The existing aggregate lock does not explicitly acquire workspace operational
admission before a no-write replay. That admission must move into the lock path
before any future operation receipt can be disclosed.

## Ownership

- Properties owns update equivalence, immutable operation receipts, topology
  lifecycle, tenant export/destruction, and replay results.
- Public API, Admin API, Admin CLI, and web callers supply one non-empty
  operation id for each logical details update.
- Workspaces remains the authority for operational admission through the
  existing termination-fence contract.
- GMA continues to own transactional dispatch, transaction-key locking,
  optimistic concurrency, persistence exception mapping, and outbox behavior.
  Property request fingerprints, receipts, and topology lifecycle do not
  belong in GMA.

## Invariants

1. The idempotency coordinate is `(ScopeId, PropertyId, OperationId)`.
2. Operation id and property details are validated and normalized before the
   aggregate lock or operation lookup.
3. The aggregate lock acquires workspace operational admission inside the
   active Properties transaction before reading the property or a receipt.
4. Exact equivalence includes mutation kind, property id, expected version,
   and normalized name, code, and time-zone id.
5. An exact completed replay returns the immutable minimal mutation receipt
   without another uniqueness check, property mutation, or domain event.
6. Reusing an operation id on the same property for another expected version
   or normalized details returns one stable conflict mapped to HTTP 409.
7. Invalid, unauthorized, unavailable, missing, retired, stale, or
   duplicate-code attempts do not bind the operation id.
8. A valid request whose normalized details already equal current state stores
   one successful no-change receipt without advancing the property version or
   publishing an event.
9. A material update acquires the normalized destination-code coordinate,
   checks uniqueness, mutates the aggregate, and stores its operation receipt
   in the same Properties transaction and outbox boundary.
10. The journal stores only operation id, tenant/property coordinates, kind,
    expected version, a canonical SHA-256 fingerprint, result status,
    processing status, result version, and completion time. It stores no raw
    property details or actor attribution.
11. Operation records are append-only, tenant-filtered, cascade with their
    property, participate in tenant export, and are removed by bounded tenant
    destruction.

## Surfaces

- Add `OperationId` to `UpdatePropertyCommand` and public/Admin update request
  contracts.
- Require `--operation-id` for Admin CLI property update.
- Keep one browser operation id and the attempt's original expected version
  while the same normalized edit is retried. Clear it after success, cancel,
  target change, or normalized value change.
- Keep `PropertyMutationReceiptDto`; clients refetch current property state
  after receiving it.

## Domain And Persistence

Add an explicit property-details update outcome so the application can
distinguish a material update from a successful no-op. Add a Properties-owned
append-only mutation-operation table with a tenant/property/operation composite
key and property foreign key. The initial mutation kind is details update; later
Properties slices may widen the kind constraint for processing and retirement
without changing this slice's semantics.

The operation fingerprint is a versioned, length-delimited SHA-256 digest of
normalized non-personal topology values. The reusable hash mechanics are too
small to justify a new cross-repository abstraction, and the operation journal
is not generic because its receipt, export, retention, and replay policy belong
to Properties.

## Verification

- Focused domain and application tests cover material update, no-op success,
  normalized replay, changed reuse, failed-attempt reuse, stale versions,
  duplicate codes, admission-before-replay, and one event/receipt.
- Persistence and lifecycle tests cover model constraints, append-only guards,
  tenant filtering, export, destruction, and cascade behavior.
- API, Admin API, Admin CLI, generated contracts, and web tests cover the
  required operation id and stable browser attempts.
- One focused PostgreSQL scenario proves concurrent exact retries commit one
  operation row and one update event, and that failed commands persist no
  receipt.
- Use cheap focused checks while editing. Run one consolidated non-Docker slice
  gate and one targeted Docker scenario at completion.

## Not In This Slice

- property processing activation or suspension replay safety;
- property retirement replay safety;
- room or bed creation, update, or retirement replay safety;
- generic GMA idempotency, response caching, or an operation-journal package.

## Completion Evidence

- All `133` Properties tests passed, covering normalized material/no-op
  updates, immutable replay, conflict and failed-attempt reuse, persistence
  constraints, append-only enforcement, and tenant lifecycle behavior.
- The focused PostgreSQL scenario passed (`1/1`) with concurrent exact retries,
  three expected operation receipts, two material update events, failed-command
  reuse, no-op handling, and closed-workspace admission precedence.
- The consolidated backend gate passed with a zero-warning build, synchronized
  solution, clean migration drift, architecture checks, all non-Docker suites,
  `99` Operations Notifications tests, and `54` integration tests.
- The web gate passed type checking, linting, `201` tests, and the production
  build. The OpenAPI snapshot and generated TypeScript contracts are current.
