# Reservations Stay Lifecycle Task

Status: implemented and verified
Date: 2026-07-12

## Goal

Complete the first operational stay state machine inside Reservations: staff can check in a confirmed reservation, mark an unarrived reservation as a no-show, and check out an active stay. Every transition is tenant/property scoped, optimistic-concurrency controlled, attributed to an authenticated actor, tied to an explicit business date, and reflected in durable integration facts.

This slice stays in Reservations. Inventory owns whether an allocation can be released and the unit-availability consequence; Reservations owns why the booking is ending and which terminal booking state follows. Guest profiles, billing, housekeeping, and a future business-day module consume these facts rather than owning the reservation state machine.

## State Machine

Keep existing wire values stable and append:

- `Confirmed -> CheckedIn` synchronously;
- `Confirmed -> NoShowPending -> NoShow`, with Inventory release between the pending and terminal states;
- `CheckedIn -> CheckoutPending -> CheckedOut`, with Inventory release between the pending and terminal states;
- release rejection restores `NoShowPending -> Confirmed` or `CheckoutPending -> CheckedIn` and retains a factual rejection code;
- cancellation remains valid before arrival from `PendingAllocation`, `AllocationRejected`, or `Confirmed`, but never aliases no-show or check-out;
- allocation amendments remain limited to `Confirmed`, so a checked-in or terminal stay cannot silently move dates/units through the pre-stay amendment path.

No-show and check-out retain the allocation until Inventory confirms release. A stale or mismatched release outcome cannot change the Reservation aggregate or its local Inventory projection. Exact duplicate release outcomes are idempotent and may repair the projection without republishing terminal events.

## Business Date And Provenance

Do not derive operational state from UTC calendar dates. Every stay command carries an explicit `BusinessDate`; the server supplies the immutable UTC timestamp and the front door supplies the authenticated actor identity.

- check-in requires `Arrival <= BusinessDate < Departure`;
- no-show requires `BusinessDate >= Arrival`;
- check-out requires `BusinessDate >= CheckedInBusinessDate`;
- actor ids are normalized, bounded, persisted, and included in lifecycle integration facts;
- Admin API and Admin CLI require explicit confirmation for all three operations;
- public staff API operations use distinct property-scoped permissions.

A later business-day module may provide the default date and close/reopen policy. Early check-in, pre-arrival no-show, backdating restrictions, and other override workflows require explicit product policy rather than hidden wall-clock assumptions.

## Contracts And Surfaces

Add stable status values and lifecycle fields to `ReservationDto`, plus tenant-scoped versioned events for checked-in, no-show, and checked-out facts. Add operation-specific permissions and front doors:

- `POST /api/reservations/properties/{propertyId}/reservations/{reservationId}/check-in`;
- `POST /api/reservations/properties/{propertyId}/reservations/{reservationId}/no-show`;
- `POST /api/reservations/properties/{propertyId}/reservations/{reservationId}/check-out`;
- equivalent Admin API and `reservations check-in|no-show|check-out` CLI operations.

Existing route structure may keep the current shorter reservation base path; the semantic operation names and contracts are authoritative. Invalid status filters must fail validation rather than silently becoming an unfiltered read.

## Persistence

Persist checked-in, no-show, checked-out, and pending-release business-date/actor/timestamp facts on the Reservation aggregate. PostgreSQL constraints enforce complete lifecycle shapes for checked-in, pending, and terminal states. Existing rows backfill without semantic change because the appended states and new fields are nullable.

The domain/application projects remain database-provider agnostic. No cross-module foreign keys are added. Reservations continues to own its Inventory projection through integration events.

## Verification

- domain tests cover every allowed edge, date boundary, actor/version validation, release rejection restoration, duplicate/mismatched release outcomes, and forbidden terminal transitions;
- application tests cover actor propagation and event/projector behavior;
- contract/profile tests cover stable status values, permissions, published events, and subject names;
- persistence tests cover lifecycle columns, constraints, indexes, and migration drift;
- a real PostgreSQL/NATS worker scenario proves check-in, no-show release, checked-out release, freed inventory reuse, cross-property denial, and stale-version conflict;
- build, non-Docker tests, Docker tests, package/security audits, diff checks, and GMA submodule checks pass.

## Deferred

- canonical Guest Records and reservation-to-guest linking;
- room/bed assignment changes after check-in;
- housekeeping dirty/clean transitions;
- folios, balances, deposits, refunds, and financial close rules;
- property business-day configuration, close/reopen, and override approvals;
- automatic arrival/departure jobs and notifications;
- reversing a no-show or checked-out stay;
- group reservations and partial per-guest check-in/out.

## Implementation Note

The slice is implemented in `src/Modules/Reservations` with appended wire states, actor/business-date provenance, correlated Inventory release completion, distinct permissions, public/Admin API and Admin CLI surfaces, PostgreSQL constraints and migration, focused unit coverage, and a passing real PostgreSQL/JetStream stay saga. Canonical property business-day policy and all other items above remain intentionally deferred.

Verification passed with a zero-warning all-up build, all migration drift checks, 1,540 non-Docker tests, 31 Docker tests, the full transitive vulnerability audit, source-package ownership checks, `git diff --check`, and clean GMA submodules whose recorded commits match `origin/dev`.
