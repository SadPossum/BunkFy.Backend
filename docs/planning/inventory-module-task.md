# Inventory Module Task

Status: first slice and Reservations allocation authority implemented
Date: 2026-07-11

Build Inventory as the consumer-owned availability model on top of the completed Properties topology contracts. Keep Inventory independent of reservation, pricing, provider, and operational-workflow concerns.

## Goal

Create a tenant- and property-scoped module that owns:

- local projections of property, room, and bed topology;
- the explicit choice to sell a room as one unit or sell its beds individually;
- inventory-unit operational state;
- manual date-range blocks;
- availability reads over property-local stay dates.

## Domain Decisions

- Properties remains the source of physical topology. Inventory stores its own projection and never writes Properties tables or adds cross-module foreign keys.
- Topology handlers apply only newer entity versions. Duplicate or stale events are harmless, and projection rebuild can repair missed delivery.
- A room starts `Unconfigured`. An operator must select `RoomLevel` or `BedLevel`; both modes cannot be active together. This prevents accidental double-selling of a room and its beds.
- Stay ranges use property-local `DateOnly` values with half-open semantics: `[arrival, departure)`. Arrival must be before departure.
- Retired Properties topology remains projected for history but is unavailable for new configuration or blocks.
- Manual blocks are Inventory facts. Maintenance and housekeeping may request them later through contracts, but their workflows stay in their own modules.
- One operator action may target a property, configured building/floor, room, or unit. Inventory resolves that target to currently sellable units and persists the correlated block group atomically.
- Optimistic versions protect inventory configuration and block changes. Integration events and exports carry those versions.

## First Slice

1. Scaffold Contracts, Domain, Application, Persistence, PostgreSQL migrations, API, Admin API, Admin CLI, tests, and host composition.
2. Project versioned Properties events for property, room, and bed create/update/retire changes.
3. Add a rebuild task that reads `IPropertiesTopologyProjectionExportSource`, checkpoints its cursor, and replaces stale topology projection state safely.
4. Add room sales-mode configuration with explicit expected-version handling.
5. Materialize room-level or bed-level inventory units from the selected mode without deleting historical unit records when the mode changes.
6. Add manual block create/release commands and date-range availability queries.
7. Publish versioned configuration/block events and expose an Inventory projection export for Reservations.

## Access Model

Start with these scoped permissions:

- `inventory.read`;
- `inventory.configure`;
- `inventory.blocks.manage`.

Use tenant and `tenant/property` scopes with descendant matching, following Properties. Public management endpoints require real authenticated user subjects and persisted AccessControl grants. Admin API/CLI use the same application handlers and explicit admin authorization path.

## Invariants

- A projected room or bed must belong to the same tenant and property as its parent projection.
- Only one sales mode is effective for a room at a time.
- Room-level mode exposes one active room unit and no active bed units; bed-level mode exposes active units only for active projected beds.
- Retired or blocked units are unavailable.
- Block ranges must be valid and cannot be silently widened, shortened, or released by stale commands.
- Reads and writes are tenant-safe even when callers supply valid identifiers from another tenant.

## Deferred

- temporary reservation holds and hold expiry;
- group inventory, pooled capacity, room-type inventory, and controlled oversell;
- pricing, restrictions, packages, and channel availability;
- maintenance/housekeeping workflows;
- provider mappings and synchronization state;
- automatic mode inference from Properties topology.

## Acceptance Checks

- [x] module boundaries and host composition pass architecture guardrails;
- [x] PostgreSQL initial migration covers Inventory-owned state;
- [x] stale/out-of-order Properties events cannot regress projection details or status;
- [x] live handlers and rebuild writes materialize the same durable unit model;
- [x] room-level and bed-level modes cannot be simultaneously sellable;
- [x] date-range availability obeys half-open stay semantics and manual blocks;
- [x] broad physical block targets create and release correlated unit blocks atomically;
- [x] real-token Docker coverage proves tenant/property permissions and cross-scope denial;
- [x] downstream availability export includes current unit configuration and block history.

## Completion Note

The first slice is implemented in `src/Modules/Inventory`. Concurrent block and allocation mutations serialize through an optimistic unit version, scope-aware integrity is retained, and topology records remain append-retained when a sales mode changes or Properties retires a source entity. The Reservations slice subsequently added direct allocation/release authority and no-overbooking enforcement here; expiring pre-booking holds remain deferred.
