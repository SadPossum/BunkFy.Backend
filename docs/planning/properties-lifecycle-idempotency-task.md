# Properties Lifecycle Idempotency Task

Status: complete
Date: 2026-08-07

## Goal

Make property processing activation, processing suspension, and retirement safe
to retry after an uncertain response. One logical transition must commit at
most one property version, governance revision, and event, while changed reuse
of its operation id fails closed.

## Audit Finding

Properties now has a scoped append-only mutation journal for details updates,
but lifecycle commands do not use it. An exact retry currently reaches stale
optimistic concurrency. Activation also re-evaluates mutable workspace and
country-policy state, so a committed activation can appear to fail when its
response is lost and the policy later expires or is removed. Suspension and
retirement return no receipt, and their confirmation is enforced only by some
transport surfaces. Retirement checks active rooms before rejecting a stale
property version.

## Ownership

- Properties owns lifecycle request equivalence, immutable receipts,
  governance revisions, topology preconditions, and lifecycle events.
- Public API, Admin API, Admin CLI, and web callers supply operation ids on the
  lifecycle surfaces they expose.
- Workspaces remains the authority for operational admission through the
  existing Properties transaction lock and lifecycle policy contract.
- The country-policy registry remains authoritative for each new activation.
  It is not consulted again to acknowledge an already completed exact replay.
- GMA continues to own transactional dispatch, transaction-key locking,
  optimistic concurrency, and outbox behavior. Lifecycle fingerprints and
  receipts remain Properties-specific.

## Invariants

1. Lifecycle operations reuse the coordinate
   `(ScopeId, PropertyId, OperationId)` and add distinct activation,
   suspension, and retirement mutation kinds.
2. A non-empty operation id, explicit confirmation, expected version, and
   syntactically valid actor reference are required before acquiring the
   aggregate lock. Actor identity is excluded from equivalence and the journal.
3. Every attempt acquires workspace operational admission and the property
   aggregate lock before reading a receipt.
4. Exact completed replay returns its immutable minimal receipt without
   re-evaluating country policy, lifecycle policy, active-room topology, or
   emitting another revision or event. Clients still refetch authoritative
   current state.
5. Reusing an operation id on the same property for another lifecycle kind,
   expected version, or activation policy coordinates returns the existing
   management-operation conflict mapped to HTTP 409.
6. Invalid, unconfirmed, unauthorized, unavailable, missing, retired, stale,
   policy-denied, lifecycle-restricted, or active-room-blocked attempts do not
   bind the operation id.
7. Activation equivalence includes all policy coordinates and a canonical,
   order-independent acknowledgement set. A new activation evaluates current
   lifecycle and country-policy admission before mutating.
8. A successful activation or suspension stores its governance revision,
   domain/outbox event, property version, and operation receipt in one
   Properties transaction.
9. A new retirement validates property state and expected version before its
   active-room query. It then commits the terminal property state, event, and
   receipt atomically while the property lock prevents room creation races.
10. The existing operation journal retains no raw lifecycle payload or actor
    attribution. Its tenant export and bounded destruction behavior continues
    to cover every widened mutation kind.
11. A workspace that has entered closing or destruction denies even an exact
    completed replay before any receipt is disclosed.

## Surfaces

- Add `OperationId` and command-level confirmation to activation, suspension,
  and retirement commands and request contracts.
- Return `PropertyMutationReceiptDto` from suspension and retirement across
  public/Admin HTTP and Admin CLI instead of transport-only empty success.
- Require `--operation-id` for Admin CLI retirement.
- Keep one browser operation id while the same activation, suspension, or
  property-retirement attempt is retried. Clear it after success, cancellation,
  target change, version change, or activation-value change.
- Require a resolved actor for public property retirement, matching processing
  lifecycle audit behavior. Admin surfaces may retain their system-operator
  attribution boundary.

## Domain And Persistence

Add explicit domain preflight checks for processing activation and property
retirement so stale requests fail before policy or topology work. Extract a
small Properties-local mutation-journal coordinator to share replay matching
and receipt recording across details and lifecycle handlers. Widen the existing
PostgreSQL mutation-kind constraint; do not add another operation table or a
generic GMA package. Bump the Properties tenant export schema because the
exported operation-kind vocabulary widens.

## Verification

- Focused domain and application tests cover confirmation and actor guards,
  exact immutable replay, changed/kind reuse, policy-denied and active-room
  failed-attempt reuse, precondition ordering, and one receipt/revision/event.
- API, Admin API, Admin CLI, generated contracts, and web tests cover required
  operation ids, typed receipts, and stable browser attempts.
- Persistence and lifecycle tests cover the widened kind constraint and export
  vocabulary without changing append-only or destruction guarantees.
- One focused PostgreSQL scenario covers concurrent activation replay,
  activation/suspension/retirement event and revision cardinality, immutable
  retired replay, failed-attempt reuse, and closing-workspace admission.
- Use cheap focused checks while editing. Run one consolidated non-Docker gate
  and one targeted Docker scenario at slice completion.

## Not In This Slice

- room or bed creation, update, or retirement replay safety;
- changing country-policy semantics or workspace lifecycle authority;
- retaining raw lifecycle requests or actor identity in the operation journal;
- generic GMA idempotency, response caching, or an operation-journal package.

## Completion Evidence

- Properties unit tests pass: 145 tests.
- The focused PostgreSQL lifecycle-operation scenario passes: 1 test.
- The consolidated backend gate passes with a zero-warning build, synchronized
  solutions, clean migration drift, architecture checks, and all non-Docker
  tests, including 54 integration tests.
- Web verification passes: 38 test files and 204 tests, production bundle,
  type checking, linting, and generated-contract drift checks.
