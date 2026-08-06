# Reservations Management Lifecycle Idempotency Task

Status: complete

## Goal

Make staff and administrative cancellation, check-in, no-show, and checkout
safe to retry after an uncertain response without repeating domain events,
Inventory release requests, or lifecycle attribution.

## Audit Finding

Reservation creation now has a caller-owned operation identity, but the four
management lifecycle commands still rely only on optimistic aggregate versions
and generate fresh correlation ids. If the server commits and the response is
lost, the same logical retry can report a version conflict or attempt a second
release workflow even though the first command succeeded.

Guest-detail edits, Guest Record links, and Inventory amendments have different
payload, privacy, and desired-state semantics. They remain separate follow-up
work rather than widening this lifecycle slice.

## Ownership

- Reservations owns lifecycle operation equivalence, replay results, journal
  retention, restriction behavior, and conflict errors.
- Public API, Admin API, Admin CLI, and web callers supply one non-empty
  operation id for each logical lifecycle attempt.
- GMA continues to own transactional command execution, optimistic-concurrency
  mapping, outbox/inbox delivery, and generic database exception
  classification. No Reservation action vocabulary or journal belongs in GMA.

## Invariants

1. The idempotency coordinate is `(ScopeId, ReservationId, OperationId)`.
2. Every request still passes current authorization, property scope, country
   policy, and processing-restriction checks before a journal replay is visible.
3. The existing per-reservation mutation lock is acquired before reading the
   journal or aggregate lifecycle state.
4. A successful first action records its operation in the same transaction as
   the aggregate mutation and outbox effects.
5. An exact replay matches action kind, expected aggregate version, and the
   optional business date, returns the current minimal mutation receipt, and
   emits no second domain event or Inventory release request.
6. Reusing the coordinate for a different lifecycle request returns a stable
   conflict mapped to HTTP 409.
7. Failed validation or a failed domain transition does not bind the operation
   id, because no successful effect exists to replay.
8. Release rejection does not erase the accepted operation. A later replay
   truthfully returns the current restored state without starting a new release.
9. Authentication provenance is evaluated on every request and remains in the
   aggregate's existing lifecycle audit fields. The journal does not duplicate
   actor identifiers or guest personal data.
10. The journal stores only operation id, reservation coordinate, action kind,
    expected version, optional business date, and creation time. It is removed
    with its owning reservation or tenant and is included in module-owned
    export/catalogue coverage as correlated operational metadata.

## Surfaces

- Add `OperationId` to cancellation and stay-lifecycle application commands.
- Require it in public and Admin API request contracts.
- Require `--operation-id` in the Admin CLI lifecycle commands.
- Keep one web operation id across transport/server uncertainty for an
  unchanged action attempt; allocate a new id for a new action submission.
- Extend the deployed Reservations and Inventory verifier to replay lifecycle
  actions without broadening its retained evidence.

## Persistence

Add a bounded Reservations-owned lifecycle operation journal with a unique
tenant/reservation/operation coordinate and a same-module reservation foreign
key. Domain and application projects remain provider agnostic; the concrete
schema and migration stay in the PostgreSQL migrations project.

The journal is not a generic HTTP response cache. Replays return the current
`ReservationMutationReceiptDto`, so later saga convergence is never hidden
behind a stale stored response.

## Verification

- Focused application tests cover exact replay, changed-payload conflict,
  failed-command key reuse, release-rejected replay, and lock-before-journal
  ordering.
- Persistence tests cover uniqueness, cascade/tenant removal, export, and
  migration drift.
- API, Admin API, CLI, generated contracts, and web tests cover the required
  operation id and stable client attempt behavior.
- The focused PostgreSQL/JetStream stay scenario proves one event/release per
  exact retry and current-state replay after convergence.
- Use cheap focused checks while editing. Run the full non-Docker slice gate
  and one targeted Docker scenario only at slice completion.

## Outcome

Cancellation, check-in, no-show, and checkout now use a Reservations-owned
operation journal behind the existing serialized mutation boundary. Public API,
Admin API, Admin CLI, and web callers provide stable operation identities;
exact retries return the current receipt, while changed reuse returns a stable
conflict. The journal is covered by PostgreSQL migration, tenant destruction,
subject and tenant export, and the executable personal-data catalogue. No GMA
change was required.

Verification completed on 2026-08-06:

- all 223 Reservations module tests pass;
- the Reservations PostgreSQL model has no pending migration changes;
- the real PostgreSQL and JetStream stay-lifecycle scenario passes without a
  skip and proves one stored management operation per successful action;
- web typecheck, lint, all 159 tests, and the production build pass; and
- the root operations suite passes, including the Operations Notifications
  fixture and the 11-check deployed Reservations and Inventory verifier.
