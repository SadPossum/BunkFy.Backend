# Inventory

Inventory owns BunkFy's tenant- and property-scoped sellable-unit model. Properties remains the source of physical property, room, and bed topology; Inventory consumes versioned events into local projections and can repair them through the Properties topology rebuild source.

The module's executable personal-data contract is
[`personal-data-catalog.v1.json`](personal-data-catalog.v1.json). Its generated
[`personal-data-inventory.v1.md`](personal-data-inventory.v1.md) is checked by
reflection-backed tests against person-linked persistence, commands, queries,
APIs, admin output, projection exports, domain events, and integration events.
Pure facility topology remains explicitly outside that catalogue. The policy
values are engineering defaults until country, retention, and rights approval.

## Current Slice

- durable room and bed inventory-unit identities;
- explicit `Unconfigured`, `RoomLevel`, or `BedLevel` room sales mode;
- manual half-open `[arrival, departure)` block groups targeting a property, configured building/floor, room, or unit;
- retry-safe manual-block creation and release with caller-owned operation ids,
  canonical request fingerprints, typed immutable receipts, and exact replay;
- retry-safe room and bed retirement requests, rejected-process retries, and
  draining-process cancellations with caller-owned operation ids, normalized
  intent fingerprints, expected process versions, compact journal pointers,
  and exact replay;
- database-enforced coordinate, range, outcome, lifecycle, and version
  invariants for authoritative allocations, amendment decisions, manual
  blocks, room configurations, retirement processes, and allocation locks;
- durable, idempotent multi-unit reservation allocations and releases with concurrent-claim serialization;
- exact-reservation Data Rights discovery/export plus terminal allocation
  anonymisation and restore-safe owner proof;
- date-range availability reads over the currently sellable units;
- scoped `inventory.read`, `inventory.configure`, `inventory.blocks.manage`, and
  sensitive `inventory.retire` permissions;
- public API, Admin API, Admin CLI, PostgreSQL migration, NATS handlers, and worker rebuild composition;
- versioned unit-definition, sales-mode, block, and allocation events plus `IInventoryAvailabilityProjectionExportSource` for downstream Reservations rebuilds.

Inventory does not own Properties topology, reservation lifecycle/contact data, temporary booking holds, rates, provider mappings, maintenance workflows, or housekeeping workflows.

Grouped blocks resolve against Inventory's local topology projection at creation time and persist one correlated block per currently sellable unit. Creation and group release are transactional, so a broad physical target cannot leave a partially blocked floor or room. Existing single-unit create/release contracts remain available for compatibility.

## Operational Surfaces

Room and manual-block directories use deterministic, bounded pages with `HasMore`; repositories determine continuation with `pageSize + 1` lookahead and do not execute exact count queries. Callers should continue only while `HasMore` is true. Availability intentionally remains one complete property and date-range decision snapshot because partial availability would be unsafe for assignment decisions.

Ordinary sales-mode and manual-block writes return identity, status, version, and affected-count receipts instead of nested read models. API and web callers invalidate and refetch the authoritative room, block, or availability read after a successful write. Retirement operations keep their bounded process DTOs because their impact and retry state are part of the immediate operator decision.

Manual-block writes require one non-empty operation id per logical attempt.
Inventory serializes creates on the property-and-operation coordinate and
releases on the durable block or block-group coordinate. An exact retry returns
the original receipt before consulting mutable topology or block state; changed
reuse returns a stable conflict, and failed attempts do not consume the id. The
journal stores only normalized request fingerprints and compact typed results,
not a duplicate copy of the free-text reason.

Room and bed retirement requests serialize on the room coordinate. Exact
request retries resolve the stored topology-change pointer and return the
current bounded process view, while a new operation id with the same normalized
reason adopts that process. Changed intent returns a stable conflict. Rejected
process retries additionally bind the caller-observed process version, acquire
the process and room fences in that order, and publish no duplicate finalization
request on exact replay.

A room or bed retirement can be canceled only while it is `Draining`.
Cancellation binds the observed process version, normalized reason, and actor;
acquires the process and room fences in that order; and records a compact replay
pointer before commit. It republishes the room definition in the same transaction
so sellability returns with a higher unit version. Canceled attempts remain
queryable as history but are excluded from active-target lookups. PostgreSQL
partial unique indexes allow later attempts while still enforcing at most one
active retirement per room or bed.

Public and Admin Inventory endpoints emit `Cache-Control: no-store`, `Pragma: no-cache`, and an expired response date. This prevents block reasons, staff references, claim identifiers, and current availability state from being retained by shared caches. Admin endpoints also declare explicit success response metadata so generated clients match runtime responses.

Authoritative operational rows are protected by named relational checks in
addition to aggregate guards. Nullable transition evidence is required
explicitly, so PostgreSQL three-valued logic cannot admit an incomplete
amendment, completion, rejection, or cancellation shape. Rebuildable topology
placeholders remain outside this contract because their valid out-of-order
state machine is deliberately different from operational authority.

## Tenant Termination

Inventory is a mandatory `Export` and `Destroy` owner. Export depends on
Properties and contains Inventory-owned unit identities, room sales
configuration, manual blocks, allocations and allocation units, amendment
decisions, anonymisation proof, bed or room retirement processes, and immutable
management-operation receipts including retirement topology-change pointers.
Replicated topology, transport journals, rebuild checkpoints, operation locks,
and the internal tenant revision are deliberately excluded from portability.

Destruction depends on Reservations so booking authority is removed before
allocations disappear. It closes local admission, waits for live outbox leases,
and removes all 18 tenant-owned record families in foreign-key-safe stages.
One invocation removes at most one non-empty batch of 500 physical rows;
allocation units are explicitly removed before allocations. Completion retains
only a closed lifecycle row and one immutable, PII-free destruction receipt
with a versioned SHA-256 proof chain. Personal-data catalogue version 6 binds
the retained proof to its dedicated tenant-destruction policy.

Relational writes and export selection share the tenant mutation transaction
key. Each operational save rechecks the authoritative Workspaces termination
fence and advances a module-local revision in the same transaction. Export runs
under repeatable-read, validates the exact frozen process, epoch, and fence
before and after streaming, and succeeds only when that local revision remains
unchanged. Closing also suppresses scoped inbox delivery and outbox claims.
Anonymisation receipts, restore receipts, and the final destruction receipt are
protected in EF and PostgreSQL; anonymisation tombstones remain updateable for
restore proof but cannot be deleted outside the exact destruction operation.

Migrations `AddInventoryTenantExportRevision`,
`AddInventoryTenantDestructionLifecycle`,
`AddInventoryManualBlockManagementOperations`,
`AddInventoryRetirementManagementOperations`, and
`AddInventoryRetirementCancellation`, and
`AddInventoryAuthoritativeStateIntegrity` add the revision/lifecycle state,
resumable operation, typed receipt ledger, cancellation audit state, partial
active-retirement uniqueness, and provider-side proof guards. All 160 focused
non-Docker Inventory tests pass, EF reports no pending model changes, and the
Inventory PostgreSQL scenarios passed through 2026-08-21 with lock drain,
outbox suppression, bounded graph removal, replay/conflict, concurrent block
mutation, retained cancellation history, active-attempt enforcement, valid
legacy-row upgrade, exact named check enforcement, closed admission, downgrade
refusal, and tenant isolation.

Production execution remains disabled until every mandatory owner, terminal
orchestration, protected replay, operator controls, and final admission are
complete.

## Runtime

The API hosts expose management commands and reads. When the worker is enabled, compose Properties and Inventory; compose Reservations as well when allocation requests should be consumed. Enable NATS consumers and publishing, and enable the task worker when projection rebuild tasks should execute.

All inventory tables are in the `inventory` schema. No Inventory table or migration reaches into another module's schema.
