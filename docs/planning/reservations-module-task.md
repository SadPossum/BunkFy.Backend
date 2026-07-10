# Reservations Module Task

Status: first slice implemented
Date: 2026-07-11

Build Reservations as the tenant- and property-scoped owner of booking intent and lifecycle. Inventory remains the sole owner of concrete unit claims and the no-overbooking invariant; Guest Records, Rates, Billing, and provider adapters remain separate future modules.

## Goal

The first complete Reservations slice should support:

- staff-created reservations for one or more explicit room/bed inventory units;
- property-local half-open stay ranges `[arrival, departure)`;
- a primary guest/contact snapshot and guest count without creating guest login identities;
- direct and external-source references suitable for later idempotent provider ingestion;
- asynchronous allocation confirmation or rejection by Inventory;
- cancellation with Inventory-owned allocation release;
- scoped reads and lifecycle commands through public management API, Admin API, and Admin CLI;
- versioned events, local projections, rebuilds, and worker processing.

## Ownership

Reservations owns:

- reservation identity, requested stay, requested units, source reference, contact snapshot, notes, and lifecycle status;
- allocation request correlation and the accepted Inventory allocation reference;
- reservation history and lifecycle events.

Inventory owns:

- whether requested units are sellable and free for the requested dates;
- active manual blocks, holds, allocations, and claim release;
- serialization of concurrent claims and the no-overbooking decision.

Reservations must not write Inventory tables or infer confirmation from its local availability projection. Inventory must not own guest/contact, booking-source, or reservation lifecycle data.

## Confirmation Protocol

Do not create a distributed transaction across module DbContexts.

1. `CreateReservation` persists a `PendingAllocation` reservation and publishes an allocation request through the Reservations outbox.
2. Inventory consumes the request and handles it idempotently by tenant, reservation id, and allocation request id.
3. Inventory atomically either creates the allocation or records a deterministic rejection, then publishes the result through its outbox.
4. Reservations consumes the correlated result and transitions to `Confirmed` or `AllocationRejected`.
5. Duplicate/stale outcomes are harmless. A result for an old request id cannot change the current reservation attempt.

Cancellation follows the same rule: a confirmed reservation enters `CancellationPending`, Inventory releases its allocation, and only the correlated release result moves the reservation to `Cancelled`. A pending reservation records cancellation intent and compensates a late allocation confirmation with an immediate release; an allocation rejection completes that cancellation locally. Already rejected reservations may cancel locally.

The allocation-request and release-request message contracts belong to Inventory because they describe commands accepted by the Inventory boundary, even though Reservations publishes them. Inventory outcome events remain Inventory-published facts. This preserves a one-way project dependency from Reservations to `Inventory.Contracts`.

## Inventory Prerequisite

Extend the existing Inventory module, without opening another new domain, to provide:

- durable allocation records keyed idempotently by reservation and request id;
- explicit multi-unit allocation and release messages;
- overlap checks against active blocks and allocations using half-open dates;
- deterministic unit locking/versioning so concurrent claims cannot both commit;
- allocation confirmed/rejected/released integration events;
- unit-definition change events and an availability export that includes active allocations;
- cleanup or repair hooks only when a concrete failure mode requires them.

Temporary holds are still useful for a future multi-step booking UI, but direct allocation is enough for the first reservation workflow. Do not add expiring holds until an actual workflow needs pre-confirmation protection.

## Reservation Model

Initial statuses:

- `PendingAllocation`;
- `Confirmed`;
- `AllocationRejected`;
- `CancellationPending`;
- `Cancelled`.

Reserve future status names for `NoShow`, `CheckedIn`, and `CheckedOut`, but do not implement stay operations before guest/operational requirements are understood.

Core invariants:

- tenant, property, reservation, request, and selected unit identifiers are required;
- at least one and no duplicate inventory unit is requested;
- arrival is before departure and all dates are property-local values;
- guest count is positive and primary guest name is required;
- external `(source system, source reference)` is unique within a tenant when supplied;
- only Inventory confirmation can produce `Confirmed`;
- allocation rejection cannot silently become confirmation without a new versioned attempt;
- confirmed reservations cannot become cancelled until allocation release succeeds;
- expected versions protect every staff lifecycle command.

## Local Projection

Reservations owns a local Inventory projection for unit identity, label, property/room relation, topology status, sales mode, blocks, and allocation facts. It is built from Inventory contracts and repaired from `IInventoryAvailabilityProjectionExportSource`.

The projection supports booking screens and validation of identifier/property relationships. It is advisory for availability: Inventory always makes the final allocation decision because projection delivery is eventually consistent.

## Access Model

Start with scoped permissions:

- `reservations.read`;
- `reservations.create`;
- `reservations.manage`;
- `reservations.cancel`.

Use tenant and `tenant/property` scopes with descendant matching. Guests never authenticate or call these endpoints. Public management requests require real user tokens and persisted AccessControl grants; Admin API/CLI use their explicit admin authorization path.

## First Implementation Sequence

1. Add the Inventory allocation authority, contracts, events, persistence, and tests.
2. Scaffold Reservations Contracts, Domain, Application, Persistence, PostgreSQL migrations, API, Admin API, Admin CLI, inbox/outbox, tests, and host composition.
3. Add the local Inventory projection and rebuild task.
4. Implement reservation creation and allocation-result transitions.
5. Add cancellation/release transitions.
6. Add list/detail reads and source-reference lookup.
7. Add real-token and real-NATS Docker coverage for success, conflict, cross-scope denial, idempotency, and out-of-order delivery.

## Deferred

- rates, quotes, taxes, deposits, folios, invoices, and payment state;
- guest profiles, identity documents, consents, and sensitive guest workflows;
- room moves, split stays, partial cancellation, extensions, shortening, and amendments;
- group blocks, pooled inventory, controlled oversell, and waitlists;
- check-in, checkout, no-show, and housekeeping coordination;
- provider adapters and provider-specific payload storage;
- expiring pre-booking holds;
- guest-facing accounts, portals, or self-service APIs.

## Acceptance Checks

- Inventory remains the only module capable of creating/releasing concrete unit claims;
- two concurrent requests cannot allocate the same unit for overlapping dates;
- adjacent stays do not conflict under half-open range semantics;
- duplicate request/result delivery is idempotent and stale correlation ids are ignored;
- Reservations never confirms from its local projection alone;
- cancellation cannot finish while an allocation remains active;
- external source references are tenant-safe and idempotent;
- tenant/property permissions and cross-scope denial are proven with real tokens;
- live messaging and rebuild paths converge after missed or out-of-order delivery;
- module boundaries, migration drift, fast tests, and focused Docker scenarios pass.

## Completion Note

The first slice is implemented in `src/Modules/Reservations`. Inventory now owns durable multi-unit allocation/release decisions and emits versioned unit-definition, block, and allocation facts. Reservations consumes those facts into a rebuildable local projection, exposes scoped API/Admin API/Admin CLI lifecycle operations, and runs its saga handlers in the Worker. Focused Docker coverage proves real-token property denial and the live create, confirm, overlap rejection, cancellation release, and replacement-booking paths through PostgreSQL and JetStream.
