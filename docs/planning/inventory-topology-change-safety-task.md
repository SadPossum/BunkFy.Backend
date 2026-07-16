# Inventory Topology Change Safety Task

Status: completed
Date: 2026-07-14

## Goal

Make room sales-mode changes and bed retirement safe in the presence of reservations and manual inventory blocks. Preserve module ownership, historical topology, and transaction boundaries while preventing room-level and bed-level inventory from being sold concurrently.

## Ownership

- Properties owns physical room/bed topology, topology history, and the final bed-retirement state change.
- Inventory owns sellability, room sales mode, hierarchical availability conflicts, availability concurrency fences, topology-change impact, and the durable drain process.
- Reservations owns reservation details, explicit staff reassignment, amendment history, and the existing allocation-amendment saga.
- Cross-module interaction uses owning-module contracts and integration events only. No module references another module's Application, Domain, Persistence, or API projects.
- BunkFy composition code may connect public contracts but must not own module state or business decisions.
- Nothing in this slice moves to GMA. Only a proven generic durable-process primitive may be proposed later, without BunkFy room or reservation language.

## Safety Rules

Inventory conflicts are room-scoped even when the requested unit is a bed:

- a room-level allocation or block conflicts with every bed-level allocation or block in the same room for an overlapping stay;
- a bed-level allocation or block conflicts with the room-level unit and the same bed, but not with another bed;
- allocation, release, amendment, block, unblock, mode change, and drain transitions contend on one room-level availability fence;
- a room sales-mode change is idempotent when the mode is unchanged;
- an immediate room sales-mode change is rejected while any active allocation or manual block exists in the room;
- historical or released claims do not block a change.

EF optimistic concurrency is the final race guard. Preflight reads provide useful errors, but correctness must not depend on a check performed before a concurrent write.

## Bed Retirement Workflow

Beds are retired, never hard-deleted.

1. Staff requests retirement through Inventory with the property, room, bed, and reason.
2. Inventory records a durable topology-change process and drains the bed from new sales.
3. Existing active room-level or target-bed claims keep the process in `Draining`; historical claims do not.
4. Staff may reassign affected confirmed reservations through Reservations. The current allocation remains authoritative until Inventory confirms the atomic amendment.
5. When no active allocation or block depends on the bed, Inventory requests final retirement through a Properties-owned integration command contract.
6. Properties retires the bed idempotently and publishes a correlated completion or rejection contract.
7. Inventory completes the process from that outcome. A transient failure leaves the bed drained and recoverable rather than reopening it.

Only one active retirement process may exist per bed. Checked-in room moves and forced destructive retirement are deliberately excluded from the first slice.

## Operator Surface

- expose room topology-change impact before mutation;
- return stable conflict codes with counts, without leaking guest PII;
- expose retirement process state and affected reservation identifiers to authorized staff;
- expose staff inventory reassignment for confirmed reservations through Reservations;
- make destructive actions use explicit confirmation UI, never browser-native confirmation dialogs.

## Delivery Order

1. Add hierarchical room conflict queries and a shared room availability fence in Inventory.
2. Guard immediate room sales-mode changes and expose impact reads.
3. Add the staff reservation inventory-amendment command and endpoint using the existing saga.
4. Add the durable bed drain/finalization process and Properties outcome contracts.
5. Add API/admin/CLI and web operator surfaces.
6. Add provider migrations, focused unit/integration tests, Docker race scenarios, architecture guards, and the canonical repository verification gate.

## Deferred

- scheduled/effective-date room mode transitions;
- checked-in guest room moves and operational housekeeping handoff;
- forced retirement that cancels or rewrites reservations;
- moving any process-manager abstraction to GMA before another product validates it;
- rate, accounting, housekeeping, and channel-manager consequences of topology changes.

## Completion Criteria

- overlapping room-level and bed-level claims cannot both commit under concurrency;
- mode changes cannot commit while the room has active allocations or blocks;
- bed retirement is durable, idempotent, recoverable, and preserves historical reservation references;
- staff reassignment is atomic and auditable through Reservations and Inventory contracts;
- cross-module project references remain Contracts-only;
- the provider-neutral EF model and the product's PostgreSQL migration agree;
- focused tests, Docker concurrency scenarios, architecture tests, and `eng/verify.ps1` pass.

## Verification

- `eng/verify.ps1 -SkipRestore` passes the synchronized solution build, migration drift checks, architecture tests, and all non-Docker tests.
- the PostgreSQL/NATS Inventory authorization integration scenario proves hierarchical conflicts, mode-change concurrency, draining an allocated bed, release-driven finalization, and out-of-order completion events.
- `pnpm verify` and `pnpm contracts:check` pass for the web operator surfaces and generated API contracts.
