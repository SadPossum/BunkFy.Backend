# Inventory Room Retirement And Throughput Hardening Task

Status: completed
Date: 2026-07-15

## Goal

Close the remaining topology-retirement bypass and make Inventory's allocation path suitable for provider-driven bursts without weakening reservation safety, module ownership, or recovery behavior.

## Ownership

- Properties remains the source of truth for physical property, room, and bed topology.
- Inventory owns sellability, claim conflicts, room-local serialization, topology-change impact, and durable room/bed drain processes.
- Reservations owns reservation state, staff reassignment, and amendment history.
- Properties performs a room or bed retirement only after a correlated Inventory finalization request.
- Cross-module interaction remains Contracts-only. Product process language does not move into GMA.

## Room Retirement Workflow

1. Staff requests room retirement through Inventory with a reason.
2. Inventory creates one durable process for the room and immediately drains the room-level unit and every bed-level unit from new sales.
3. Active reservations and manual blocks remain unchanged and keep the process in `Draining`.
4. Existing bed-retirement processes must finish before room retirement can begin; a room drain prevents new bed-retirement processes.
5. Staff reassign reservations through Reservations and releases operational blocks normally.
6. When no active claim remains, Inventory requests finalization through a Properties contract.
7. Properties idempotently retires the room and its active beds, then publishes correlated completion plus normal topology events.
8. Inventory completes only after the room-retired topology fact arrives. Duplicate and reordered completion events remain safe.

Direct Properties room and bed retirement commands remain compatibility surfaces but reject with a stable error directing callers through Inventory. Property retirement remains allowed only after every room is retired.

## Throughput Work

- Load requested-unit topology, room mode, and active drain state in one availability query.
- Resolve hierarchical conflict unit ids once per command and reuse them for block and allocation checks.
- Keep optimistic room and unit fences as the final race guard.
- Bound affected-reservation samples in impact/process DTOs and expose explicit truncation metadata.
- Keep full reservation details in Reservations; Inventory returns only claim identifiers to authorized operators.
- Add realistic PostgreSQL query-count and burst evidence before claiming high-volume readiness.

## Operational Storage

- Projections remain compact, module-owned, and rebuildable.
- Processed outbox/inbox cleanup stays generic in GMA but must be explicitly enabled by production deployment configuration.
- History and failed journal records are retained intentionally; cleanup must never remove pending, retrying, failed, or exhausted work.

## Deferred

- scheduled/effective-date room mode transitions;
- checked-in guest move workflows and housekeeping handoff;
- forced topology retirement that cancels reservations;
- generalized process-manager primitives in GMA;
- database partitioning or specialized exclusion constraints before measurements justify them.

## Completion Criteria

- no public, admin, or CLI path can retire a room or bed around Inventory's drain authority;
- room retirement preserves active claims and history until explicit reassignment/release;
- room and bed retirement races serialize on the room fence;
- retirement state cannot jump from `Draining` directly to `Completed`;
- allocation conflict evaluation does not resolve the same topology hierarchy repeatedly;
- impact payloads remain bounded for arbitrarily large claim sets;
- provider-neutral EF models and PostgreSQL migrations agree;
- architecture tests, focused module tests, PostgreSQL lifecycle/race/burst tests, web verification, and canonical repository gates pass.

## Delivered

- Added durable, mutually exclusive room and bed retirement processes owned by Inventory.
- Removed the direct Properties retirement bypass while retaining stable compatibility errors.
- Made drains visible to same-unit-of-work availability reads and safe under reordered topology/finalization events.
- Consolidated availability context and hierarchy resolution into two reads, followed by one combined conflict read.
- Bounded impact samples to 25 reservation identifiers with total counts and explicit truncation metadata.
- Added public API, admin API, CLI, OpenAPI, web, persistence, and PostgreSQL migration coverage.
- Kept this workflow BunkFy-specific; no GMA framework or module code was changed for it.

## Verification

- `eng/verify.ps1 -SkipRestore` passes solution synchronization, a zero-warning full build, migration drift checks, architecture tests, and all non-Docker suites.
- Inventory tests pass 40/40; Properties tests pass 42/42.
- The PostgreSQL/NATS Inventory authorization scenario passes with room/bed race and reordered-event coverage.
- The real PostgreSQL query counter proves two availability-context SELECTs plus one combined conflict SELECT.
- `pnpm verify` passes 45 web tests, lint, type checking, and the production build.
- `pnpm contracts:check` confirms the generated web contracts and OpenAPI snapshot are current.
