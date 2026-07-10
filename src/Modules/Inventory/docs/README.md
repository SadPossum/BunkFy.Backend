# Inventory

Inventory owns BunkFy's tenant- and property-scoped sellable-unit model. Properties remains the source of physical property, room, and bed topology; Inventory consumes versioned events into local projections and can repair them through the Properties topology rebuild source.

## Current Slice

- durable room and bed inventory-unit identities;
- explicit `Unconfigured`, `RoomLevel`, or `BedLevel` room sales mode;
- manual half-open `[arrival, departure)` blocks with optimistic release versions;
- durable, idempotent multi-unit reservation allocations and releases with concurrent-claim serialization;
- date-range availability reads over the currently sellable units;
- scoped `inventory.read`, `inventory.configure`, and `inventory.blocks.manage` permissions;
- public API, Admin API, Admin CLI, PostgreSQL migration, NATS handlers, and worker rebuild composition;
- versioned unit-definition, sales-mode, block, and allocation events plus `IInventoryAvailabilityProjectionExportSource` for downstream Reservations rebuilds.

Inventory does not own Properties topology, reservation lifecycle/contact data, temporary booking holds, rates, provider mappings, maintenance workflows, or housekeeping workflows.

## Runtime

The API hosts expose management commands and reads. When the worker is enabled, compose Properties and Inventory; compose Reservations as well when allocation requests should be consumed. Enable NATS consumers and publishing, and enable the task worker when projection rebuild tasks should execute.

All inventory tables are in the `inventory` schema. No Inventory table or migration reaches into another module's schema.
