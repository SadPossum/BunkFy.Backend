# Reservations Management Guest Details Idempotency Task

Status: completed

## Goal

Make staff guest-detail edits safe to retry after an uncertain response without
repeating details history, Guest stay projection events, or arrival-reminder
refreshes, and without retaining another fingerprint or copy of guest personal
data.

## Audit Finding

Reservation creation and lifecycle actions now have durable caller operation
identities. The public guest-details edit still relies only on the expected
details revision and generates fresh correlation and event ids. If a committed
response is lost, the same logical retry reports a revision conflict even
though the edit succeeded.

The existing Reservations details history already retains the normalized
post-change snapshot and correlation required to prove an exact material edit.
It should be the replay witness rather than adding another PII-bearing journal.

Guest Record links and Inventory amendments have different target-data and
asynchronous completion semantics. They remain separate follow-up slices.

## Ownership

- Reservations owns edit equivalence, replay behavior, details-history
  retention, and stable conflict errors.
- The public management API and web client supply one non-empty operation id
  for each logical edit attempt.
- GMA continues to own transactional command execution, optimistic-concurrency
  mapping, domain-event dispatch, and generic database error classification.
  No reservation payload or history-replay policy belongs in GMA.

## Invariants

1. The operation coordinate is `(ScopeId, ReservationId, OperationId)`.
2. Every attempt still passes current permission, property scope, country
   policy, and processing-restriction checks before replay is visible.
3. The existing per-reservation mutation lock is acquired before reading the
   aggregate, management journal, and details-history replay witness.
4. The shared management journal reserves the operation coordinate across
   lifecycle and guest-detail actions using only action and revision metadata.
5. A material first edit uses the operation id as its details correlation id;
   the aggregate change, history row, outbox effects, and reminder refresh are
   committed in the same transaction.
6. An exact replay matches the original expected details revision and the
   normalized requested name, contact, guest count, notes, and expected times
   against the already-retained post-change history snapshot.
7. Actor identity is authorized on every request but is not part of request
   equivalence. A replay never rewrites the original audit attribution.
8. An exact replay returns the reservation's current minimal mutation receipt
   and emits no second event or projection effect.
9. Reusing an operation coordinate for a different request returns the stable
   management-operation conflict mapped to HTTP 409.
10. Invalid, denied, stale, and no-change attempts do not reserve an operation
   id because they produce no material effect. A no-change retry remains a
   no-op while its expected revision is current.
11. No new request body, PII hash, response cache, or persistence table is
    introduced. Existing history export, anonymisation, retention, and tenant
    destruction remain authoritative for the replay witness.

## Surfaces

- Add `OperationId` to `UpdateReservationGuestDetailsCommand` and the public
  API request contract.
- Keep one web operation id and original normalized payload across transport or
  server uncertainty; rotate it when the edit intent changes or succeeds.
- Keep Admin API and Admin CLI unchanged because they do not currently expose
  this personal-data edit.

## Persistence

Extend the existing PII-free management journal with a `GuestDetails` kind and
an explicit nullable expected-details-revision coordinate. Lifecycle rows keep
their aggregate expected version; guest-detail rows keep only the details
revision. PostgreSQL constraints require exactly the revision shape appropriate
to each operation kind.

Extend the existing details-history reader with a scoped, reservation-specific
correlation lookup. The current `(ScopeId, ReservationId, ToRevision)` index
already bounds that lookup to one reservation's retained history.

History snapshots may be read transiently inside the application operation to
compare an authorized retry. They are not copied into logs, errors, metrics, or
new records.

## Verification

- Application tests cover exact replay after later lifecycle version changes,
  changed reuse conflict, failed and no-change key reuse, and lock-before-
  history ordering.
- Persistence tests cover scoped correlation lookup and snapshot mapping.
- API, OpenAPI, generated TypeScript, and web tests cover the required operation
  id and stable client attempt behavior.
- Personal-data catalogue and deterministic inventory checks remain clean.
- Use focused backend and web checks while editing. Run one full Reservations
  and web gate plus one targeted PostgreSQL migration proof at slice completion;
  no broad Docker suite is required because broker behavior is unchanged.

## Deferred

- Guest Record link and replacement operation identity;
- Inventory amendment replay after asynchronous confirmation or rejection;
- cross-module create-and-link Guest Record orchestration;
- stay date/unit amendments, room moves, split stays, and other future booking
  amendment policy.

## Outcome

Completed on 2026-08-06. Reservations now journals material guest-detail edit
operations without copying personal data, proves exact retries from retained
details history, and rejects changed operation-id reuse. The public API and web
client carry a stable per-attempt operation id; the client keeps the original
payload only in component memory and rotates it when the normalized intent or
details revision changes.

Verification passed with a warning-free Integration.Tests build, no pending
Reservations PostgreSQL model changes, all 228 Reservations module tests, the
targeted real-PostgreSQL previous-schema upgrade and constraint test, and the
complete web gate of typecheck, lint, 170 tests, and production build. OpenAPI
and generated TypeScript contracts were regenerated from the backend.
