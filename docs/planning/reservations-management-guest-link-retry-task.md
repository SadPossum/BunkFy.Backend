# Reservations Management Guest Link Retry Task

Status: completed

## Goal

Keep canonical Guest Record link and replacement actions safe when a public web
request has an uncertain response, without adding an operation journal entry or
another retained copy of the guest identifier.

## Audit Outcome

The direct link endpoint is already a state-setting `PUT`. The Reservations
aggregate checks whether the requested guest already occupies the requested
role before checking the caller's expected aggregate version. An exact retry
therefore succeeds as a no-op with the original stale version and emits no
second guest-link or Guest stay event. If another write changes the role after
the first attempt, the original version remains stale and the retry conflicts
instead of overwriting the newer state.

The public API, Admin API, and Admin CLI already require callers to provide an
expected version. Adding a durable operation id would require retaining a guest
identifier or equivalent personal-data fingerprint solely to distinguish
requests whose resulting state is already distinguishable by the aggregate.
That extra persistence is not justified for this idempotent `PUT`.

## Ownership

- Reservations owns role occupancy, explicit replacement, version conflict,
  and no-op replay semantics.
- The web client owns one in-memory snapshot of the original direct-link intent
  while a retry remains uncertain.
- API and CLI callers continue to own their original request payload.
- GMA remains unchanged; there is no generic framework primitive missing here.

## Invariants

1. Every retry still passes current authentication, permission, tenant,
   property, country-policy, and reservation processing checks.
2. Linking a new guest still requires the current Guests projection and
   authoritative processing-restriction decision.
3. An already-current guest requires no new Guests restriction decision because
   the retry produces no new processing or linkage.
4. Exact first-link and replacement retries return the current minimal
   reservation receipt without changing version, audit attribution, or events.
5. Replacing an occupied role always requires `ReplaceExistingRole = true` on
   the original intent.
6. The web preserves the original property, reservation, guest, role,
   replacement flag, and expected version after an uncertain response.
7. A changed target or explicitly restarted interaction creates a new intent;
   a background reservation-version refresh alone does not silently rebase a
   retry onto newer state.
8. The intent snapshot stays only in component memory and contains no new logs,
   storage, metrics, or analytics payload.

## Verification

- Extend the aggregate test to prove exact primary-guest replacement replay is
  a no-op with the original expected version.
- Add focused web tests for stable original-version reuse and intent rotation.
- Run focused Reservations and web tests while editing, followed by one web
  slice gate. Existing real-PostgreSQL saga coverage already proves the direct
  endpoint's exact replay behavior, so no new Docker run is required.

## Deferred

- Creating a Guest Record and then linking it is a cross-module orchestration
  beginning with a non-idempotent Guests `POST`; it requires its own slice.
- Reservation inventory amendments remain a separate asynchronous replay
  problem with confirmation and rejection outcomes.

## Outcome

Completed on 2026-08-07. The existing Reservations state-set semantics remain
the durable idempotency boundary; exact replacement replay is now covered as an
event-free no-op. The web keeps the original direct-link payload in component
memory across an uncertain retry and rotates it only when the interaction or
target changes.

Verification passed with the focused Reservations aggregate test and the full
web gate: typecheck, lint, all 176 tests, and the production build. The existing
real-PostgreSQL Reservations saga remains the deployed-path proof for exact
direct-link retry, so this slice intentionally added no schema or Docker work.
