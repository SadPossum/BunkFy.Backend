# Properties Topology Lifecycle Task

Status: implemented and verified
Date: 2026-07-10

Finish the Properties topology lifecycle before starting Inventory. This slice keeps Property and Room as separate aggregate roots, with Room owning its beds.

## Decisions

- Property, room, and bed facts have monotonic versions starting at `1`.
- Property and room versions are EF concurrency tokens and command preconditions. A stale expected version fails before mutation; overlapping saves also fail at persistence.
- Creating a room advances the property version. This serializes room creation against property retirement without provider-specific row locking.
- Any room or bed mutation advances the room version. Bed facts also advance their own versions.
- Property retirement is allowed only after every room is retired. It does not perform a cross-aggregate cascade.
- Room retirement with active beds requires an explicit cascade flag. A confirmed cascade retires active beds in the same transaction and raises bed events before the room event.
- Retired topology remains readable and exportable, but rejects further setup mutations.
- Integration events and topology exports carry entity versions so consumers can ignore stale or out-of-order facts.
- Projection rebuild paging uses an immutable generated property ordinal, not mutable property code or provider-specific runtime SQL.

## Front-Door Contract

- Property update and retirement require `ExpectedVersion`.
- Room creation requires `ExpectedPropertyVersion`.
- Room update and retirement require `ExpectedVersion` for the room.
- Bed add/update/retirement require `ExpectedRoomVersion` because Room is the owning aggregate.
- Public API, Admin API, and Admin CLI expose the same preconditions.
- Confirmation and cascade intent are separate inputs; confirmation alone never implies cascading children.

## Completion

- [x] Property retirement and the `PropertyRetired` integration event exist.
- [x] Active children cannot remain beneath retired topology.
- [x] Stale and overlapping mutations return conflict behavior.
- [x] Versions flow through DTOs, events, and exports.
- [x] Rebuild paging uses the immutable property ordinal.
- [x] Migration drift, Properties tests, architecture tests, and Docker-backed persistence tests pass.
- [x] Retirement persists a tenant-scoped outbox envelope and composed-host export resume includes retired descendants.
