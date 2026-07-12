# Reservations

Reservations owns BunkFy's tenant- and property-scoped booking intent and lifecycle. Inventory remains the sole authority for concrete room/bed claims and the no-overbooking decision.

## Current Slice

- staff-created direct or externally referenced reservations for one or more explicit Inventory units;
- primary guest/contact snapshot, guest count, notes, and half-open `[arrival, departure)` stay dates;
- asynchronous `PendingAllocation` to `Confirmed` or `AllocationRejected` transitions through outbox/inbox and NATS;
- confirmed cancellation through `CancellationPending`, Inventory allocation release, and `Cancelled`;
- check-in with explicit business date and actor provenance;
- correlated no-show and check-out release flows that retain Inventory claims until release succeeds;
- distinct scoped permissions for read, create, manage, cancel, check-in, no-show, and check-out;
- public management API, Admin API, Admin CLI, PostgreSQL migration, and Worker composition;
- a versioned local Inventory projection with live unit/block/allocation handlers and a rebuild task sourced from `IInventoryAvailabilityProjectionExportSource`;
- an independent editable-details revision and provenance marker that does not move on allocation-only lifecycle changes;
- a Reservations-owned before/after details history projection with management timeline reads;
- revision-checked guest/contact/notes updates through the management API;
- canonical primary-guest links with explicit replacement, inactive-link audit retention, and a dedicated scoped permission;
- a PII-free, rebuildable local Guest eligibility projection used only to validate new links;
- idempotent external create, guest-change, allocation-amendment, and cancellation operations with a scoped request ledger and versioned outcomes;
- adapter operations protected by source-identity and details-revision checks, including non-terminal cancellation acceptance.
- pending allocation amendments that retain current booking truth until Inventory atomically confirms or rejects the candidate;
- inbox-transaction domain-event dispatch so external operations persist allocation/cancellation requests and details history atomically.

The local Inventory projection validates unit/property relationships and supports management reads, but it is advisory for availability. Only an Inventory allocation outcome can confirm a reservation.

## Runtime

Compose Properties, Inventory, Reservations, and Guests in the Worker with NATS consumers and publishing enabled. The normal Aspire graph enables all four. Reservations rebuild tasks source Inventory availability and Guest eligibility from their owning modules.

All Reservations-owned tables use the `reservations` schema. Lifecycle state shapes are protected by PostgreSQL constraints. The module has no foreign keys or writes into another module's schema.

Allocation-affecting adapter changes use a correlated Inventory amendment of the existing allocation and are not accepted by the guest-details command. Direct staff date/unit amendment surfaces, rates, billing, identity documents, business-day close/reopen policy, room moves, and temporary holds remain later slices.
