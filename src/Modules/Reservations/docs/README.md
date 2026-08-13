# Reservations

Reservations owns BunkFy's tenant- and property-scoped booking intent and lifecycle. Inventory remains the sole authority for concrete room/bed claims and the no-overbooking decision.

The module's executable personal-data contract is
[`personal-data-catalog.v1.json`](personal-data-catalog.v1.json). The generated
[`personal-data-inventory.v1.md`](personal-data-inventory.v1.md) is checked by
reflection-backed tests against Reservations persistence, commands, queries, APIs, adapter ingress,
domain events, and integration events. The catalogue is an engineering default until its country,
retention, and rights policies receive production approval.

## Current Slice

- staff-created direct or externally referenced reservations for one or more explicit Inventory units;
- primary guest/contact snapshot, guest count, notes, half-open `[arrival, departure)` stay dates, and optional expected arrival/departure times in property-local time;
- asynchronous `PendingAllocation` to `Confirmed` or `AllocationRejected` transitions through outbox/inbox and NATS;
- confirmed cancellation through `CancellationPending`, Inventory allocation release, and `Cancelled`;
- check-in with explicit business date and actor provenance;
- correlated no-show and check-out release flows that retain Inventory claims until release succeeds;
- distinct scoped permissions for read, create, manage, cancel, check-in, no-show, and check-out;
- directly projected, bounded operational directory pages with look-ahead
  pagination and no aggregate hydration or count query;
- one authoritative property-local operations snapshot with exact Reservation
  and guest counts, a disjoint seven-category attention breakdown, and a
  deterministic bounded upcoming-arrivals list;
- public management API, Admin API, Admin CLI, PostgreSQL migration, and Worker composition;
- a versioned local Inventory projection with live unit/block/allocation handlers and a rebuild task sourced from `IInventoryAvailabilityProjectionExportSource`;
- an independent editable-details revision and provenance marker that does not move on allocation-only lifecycle changes;
- a Reservations-owned before/after details history projection with bounded,
  newest-first management timeline reads;
- minimal management mutation receipts that disclose only identity, state, and
  concurrency revisions, while explicit booking reads and history disable HTTP
  storage;
- revision-checked expected-time, guest/contact, and notes updates through the management API;
- durable expected-arrival reminders dispatched two hours before the property-local arrival time, with revision/status checks that suppress stale, cancelled, checked-in, or already-arrived stays;
- canonical primary-guest links with explicit replacement, inactive-link audit retention, and a dedicated scoped permission;
- a Reservations-owned, PII-minimal create-and-link process that survives HTTP
  interruption, resumes by Reservation, dispatches bounded TaskRuntime work,
  safely rebases only while the primary role remains empty, and exposes stable
  review outcomes without deleting the Guest Record;
- PII-free, rebuildable local Guest profile and processing-restriction
  projections used only to validate new links, plus an authoritative Guests
  gate recheck that closes event-lag windows;
- idempotent external create, guest-change, allocation-amendment, and cancellation operations with a scoped request ledger and versioned outcomes;
- adapter operations protected by source-identity and details-revision checks, including non-terminal cancellation acceptance.
- pending allocation amendments that retain current booking truth until Inventory atomically confirms or rejects the candidate;
- durable staff inventory-amendment replay across pending, confirmed, and
  rejected outcomes through the Reservations management journal;
- inbox-transaction domain-event dispatch so external operations persist allocation/cancellation requests and details history atomically.
- DataRights discovery through exact reservation id or indexed normalized
  current/pending contact values, with bounded masked previews and exact-version
  selection revalidation;
- owner-local Data Rights correction execution protected by the public host's
  configured recent-authentication assurance in addition to scoped execution
  permission and approved-claim validation;
- catalogue-driven, transient DataRights export of Reservations-owned booking,
  pending amendment, Guest-link, details-history, adapter-receipt, and reminder
  records without staff actor attribution;
- immutable terminal lifecycle timestamps plus tenant-fair, country-policy
  retention scans that recheck property policy, processing restriction,
  pending operations, and owner-local holds before mutation;
- Reservations-owned automatic anonymisation execution, checkpoint, receipt,
  and authority-separated tombstone proof, exposed to the Retention control
  plane only through bounded contract outcomes;

The local Inventory projection validates unit/property relationships and supports management reads, but it is advisory for availability. Only an Inventory allocation outcome can confirm a reservation.

## Property Operations Snapshot

The public API, Admin API, and Admin CLI expose the same current-state read for
one property under `reservations.read`. When no date is supplied, Reservations
derives the property-local date from one captured `ObservedAtUtc` and the valid
projected IANA time zone. An explicit `localDate` changes the date predicates
but still requires a known, active property and valid projected IANA time zone;
an identifier that is merely platform-resolvable but not IANA fails with
`Reservations.PropertyTimeZoneUnavailable`. The read is not a
historical as-of reconstruction. Projection lag can therefore affect current
state briefly.

Confirmed arrivals and scheduled departures are tied to the response
`LocalDate`; `CurrentlyInHouse` is current state. Those top-level cohorts may
overlap. The seven attention categories are disjoint, and their exact total is
returned with Reservation and guest counts. Upcoming items are limited to 0
through 50, ordered deterministically, reuse ordinary anonymisation and Guest
restriction rules, and use one-item lookahead for `HasMoreUpcoming`.

This is a property-local operational read, not a regional or fleet aggregate.
A future fleet dashboard must use a dedicated rollup/projection instead of
fanning this bounded personal-data response out across hundreds of properties.
All exact Reservations API path responses are marked no-store, including
binding and authorization failures before endpoint execution.

## Runtime

Compose Properties, Inventory, Reservations, and Guests in the Worker with NATS consumers, task scheduling, and publishing enabled. The normal Aspire graph enables all four. Reservations rebuild tasks source Inventory availability, Guest eligibility, and property time zones from their owning modules. A minute-level scoped task reads only the indexed due-reminder ledger and publishes reminder events through the Reservations outbox; the Notifications extension owns staff/owner fan-out and delivery preferences.

All Reservations-owned tables use the `reservations` schema. Lifecycle state shapes are protected by PostgreSQL constraints. The module has no foreign keys or writes into another module's schema.

The Reservations retention contributor registers one tenant-scoped schedule for
`reservation-operational`. It scans terminal rows by monotonic projection
ordinal, mutates only a bounded batch, and persists its cursor and proof in the
Reservations schema. `Reservations:Retention` may tune the interval, scan
size, and mutation batch size; validated conservative defaults apply when the
section is omitted.

## Tenant Termination

Reservations is the versioned `reservations` mandatory export owner and runs
after Inventory. It streams 19 deterministic, flat record types directly from
Reservations-owned booking, requested-unit, amendment, Guest-link, history,
adapter-operation, reminder, data-rights, anonymisation, and retention tables.
Consumer projections, inbox/outbox state, rebuild checkpoints, and the internal
tenant-revision row are excluded.

Export selection and relational writes share a tenant-scoped transaction key.
The exporter holds that key in a repeatable-read transaction, validates the
exact Workspaces process, epoch, and fence before and after streaming, and
returns only the unchanged local monotonic revision as proof. Ordinary writes
recheck the Workspaces fence while holding the same key and fail closed when
admission cannot be established. PostgreSQL independently protects immutable
data-rights receipts from update or deletion and anonymisation tombstones from
deletion.

The `Destroy` phase is also implemented as a Reservations-owned lifecycle. An
active Reservations legal hold blocks before local progress begins. Otherwise,
the owner closes ordinary, projection-rebuild, and scoped message creation
paths, waits for any active outbox lease, and removes one non-empty batch of at
most 500 rows per invocation in foreign-key-safe order. Retries resume the
same operation; completion leaves only the closed tenant lifecycle row and an
immutable, PII-free receipt with a chained removal proof. PostgreSQL protects
that receipt independently, and a focused container scenario proves bounded
resume, exact replay, conflict detection, tenant isolation, and post-close
admission.

The production coordinator, API/download route, protected replay, operator
controls, and termination admission remain disabled until every mandatory
owner and the cross-owner recovery contract are complete.

The Guest restriction projection starts empty after its migration by design.
Until `rebuild-reservation-guest-restrictions` completes for a tenant, missing
state denies new canonical Guest links. Existing factual links are preserved;
Guests transition events maintain the rebuilt projection afterward.

Expected times are minute-precision local wall-clock values and never replace the actual UTC timestamps recorded by check-in/check-out. Allocation-affecting adapter changes use a correlated Inventory amendment of the existing allocation and are not accepted by the guest-details command. Direct staff date/unit amendment surfaces, property-level expected-time defaults, rates, billing, identity documents, business-day close/reopen policy, room moves, and temporary holds remain later slices.
