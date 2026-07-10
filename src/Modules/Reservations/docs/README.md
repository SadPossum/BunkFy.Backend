# Reservations

Reservations owns BunkFy's tenant- and property-scoped booking intent and lifecycle. Inventory remains the sole authority for concrete room/bed claims and the no-overbooking decision.

## Current Slice

- staff-created direct or externally referenced reservations for one or more explicit Inventory units;
- primary guest/contact snapshot, guest count, notes, and half-open `[arrival, departure)` stay dates;
- asynchronous `PendingAllocation` to `Confirmed` or `AllocationRejected` transitions through outbox/inbox and NATS;
- confirmed cancellation through `CancellationPending`, Inventory allocation release, and `Cancelled`;
- scoped `reservations.read`, `reservations.create`, `reservations.manage`, and `reservations.cancel` permissions;
- public management API, Admin API, Admin CLI, PostgreSQL migration, and Worker composition;
- a versioned local Inventory projection with live unit/block/allocation handlers and a rebuild task sourced from `IInventoryAvailabilityProjectionExportSource`.

The local Inventory projection validates unit/property relationships and supports management reads, but it is advisory for availability. Only an Inventory allocation outcome can confirm a reservation.

## Runtime

Compose Properties, Inventory, and Reservations in the Worker with NATS consumers and publishing enabled. The normal Aspire graph enables all three. The Reservations rebuild task also requires Inventory persistence because Inventory supplies the projection export source.

All Reservations-owned tables use the `reservations` schema. The module has no foreign keys or writes into another module's schema.

Rates, billing, guest profiles, stay operations, amendments, temporary holds, and provider adapters remain later slices.
