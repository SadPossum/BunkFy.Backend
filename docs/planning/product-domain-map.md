# BunkFy Product Domain Map

Status: working planning note
Date: 2026-07-09

BunkFy is a management-only hostel PMS. Operators and staff authenticate into the system; guests are PMS records and do not get direct system access.

This note lists likely product domains and sorts them into first/core versus future areas. The sorting is about implementation priority, not importance. Some future areas should still shape early IDs, permissions, events, and boundaries.

## Design Bias

- Keep PMS product rules in BunkFy modules.
- Reuse GMA modules for generic capabilities: Auth, Administration, Files, Notifications, TaskRuntime, Tenancy, messaging, caching, and storage.
- Use PostgreSQL as the product/default deployment database direction, while keeping module domain/application code database-provider agnostic. Provider-specific behavior belongs in persistence adapters and migration projects.
- Treat tenant and property as explicit operating boundaries.
- Prefer operation-specific permissions over broad hard-coded roles.
- Use GMA Auth, Administration, and policy helpers first. If BunkFy needs richer policy behavior, prefer extending GMA generically before adding project-specific auth/JWT infrastructure.
- Keep module data ownership strict. A module stores another module's facts only as local projections that it owns, repairs, and rebuilds.
- Keep provider/import adapters outside product domain modules. Adapters should update the system through commands/events, not direct database writes.
- Keep external provider IDs separate from BunkFy-owned IDs.

## Ubiquitous Language

Use these terms consistently in module names, contracts, APIs, tests, and docs.

- Tenant: the operator/account boundary used for isolation and ownership.
- Property: a hostel/site managed within a tenant.
- Building: an optional physical subdivision of a property.
- Floor: an optional physical subdivision inside a building.
- Room: a physical room inside a property, building, or floor.
- Bed: a bookable sleeping place, usually inside a room.
- Inventory unit: a sellable or assignable unit such as a bed or room.
- Hold: a temporary inventory claim that prevents conflicting sale/allocation.
- Allocation: the assignment of reservation demand to concrete inventory units.
- Reservation: the booking lifecycle record managed by staff.
- Guest record: staff-managed guest data used by reservations, stays, files, billing, and operations. It is not a login account.
- Staff member: an authenticated operator-side user who works in the PMS.
- Policy: a permission or rule deciding whether a staff member can perform an operation.
- Adapter: a live process or integration component that pulls data from an external source and submits normalized updates.
- Provider: an external data/source system such as an OTA, corporate API, email inbox, web source, or file import.
- Projection: a local copy of another module's facts owned, repaired, and rebuilt by the consuming module.

## First/Core Domains

These domains form the operational spine. They should be designed first, even if only a subset is implemented in the first build slice.

### Properties

Owns the physical and organizational setup:

- properties/hostels;
- buildings, floors, rooms, beds;
- property-level settings;
- operational areas or zones when needed.

This is the first real product module and its core topology, policy, lifecycle, event, and rebuild contracts are complete.

### Inventory

Owns availability primitives and operational inventory state:

- sellable room/bed inventory;
- reservation holds and allocations;
- out-of-service periods;
- closures and blocks;
- the no-overbooking invariant;
- status transitions that affect whether something can be booked or assigned.

Inventory should not own pricing, payments, or external channel behavior.

Inventory's first operational slice and direct reservation allocation authority are implemented after Properties.

### Reservations

Owns booking lifecycle:

- reservation records and confirmed bookings;
- cancellation, no-show, check-in, check-out;
- references to inventory holds/allocation decisions;
- booking source references;
- reservation notes and lifecycle events.

Reservations should depend on Properties/Inventory concepts, but should not absorb inventory locking/allocation, accounting, rates, or provider-adapter logic.

The first Reservations slice is implemented with direct multi-unit allocation, cancellation release, scoped management surfaces, and a rebuildable local Inventory projection. Stay operations, amendments, guest profiles, rates, and accounting remain later slices.

### Guest Records

Owns staff-managed guest data:

- guest profiles;
- identity/document metadata;
- stay history;
- notes, consents, and guest-specific operational flags.

This is not a guest account module and should not imply guest login.

### Staff And Access Policy

Uses GMA Auth and Administration for identity, roles, permissions, and admin surfaces. BunkFy may later need product-specific staff profile and policy modules for:

- staff profile details;
- department/job labels;
- property assignments;
- staff status;
- property-scoped permissions;
- approvals and overrides.

Early modules should declare clear operation names so policy can grow without rewriting domain logic.

Evaluate the existing GMA policy helpers before this module grows too far. If the helper surface is too thin, improve the generic GMA policy/auth pipeline rather than baking BunkFy-only JWT behavior into product modules.

### Files

Use GMA Files with MinIO by default. BunkFy modules should use files for:

- property images;
- guest documents;
- invoices and receipts;
- operational attachments.

Create BunkFy-specific file metadata/lifecycle only when product rules require it.

### Notifications

Use GMA Notifications initially for staff-facing events. If notification behavior becomes BunkFy-specific, copy or wrap the module into BunkFy-owned modules rather than bending generic GMA behavior.

Likely notification subjects:

- reservation changes;
- housekeeping and maintenance events;
- task failures;
- provider/import errors;
- staff/admin actions requiring review.

### Tasks And Worker

Use GMA TaskRuntime and the worker host. Product modules own task payloads and handlers.

Early useful tasks:

- local tenant/property seeding;
- inventory rebuilds;
- stale hold cleanup;
- provider sync/import runs later.

## Future Domains

These are likely important, but they should not block the first Properties/Inventory slice.

### Billing And Accounting

Owns financial truth:

- folios;
- charges;
- adjustments;
- taxes/fees;
- deposits;
- refunds;
- invoices/receipts;
- balances;
- accounting exports.

Reservations can reference price/charge outcomes, but should not become the accounting system.

### Payments

Owns payment-provider interactions later:

- payment intents;
- captures/refunds;
- card-on-file references;
- provider webhooks;
- reconciliation.

Payments should integrate with Billing/Accounting rather than with Reservations directly.

### Rates And Revenue

Owns pricing strategy:

- rate plans;
- seasonal prices;
- restrictions;
- promotions;
- occupancy or availability driven adjustments;
- channel-specific price rules.

Inventory answers what can be sold. Rates answers at what price and under what restrictions.

### Housekeeping

Owns operational readiness:

- cleaning tasks;
- room/bed readiness;
- assignment to staff;
- status changes after check-out;
- inspection flows.

Housekeeping should integrate with Inventory and Staff policy, not own the physical property model.

### Maintenance

Owns repair and asset issues:

- maintenance requests;
- closures caused by repairs;
- assignment and completion;
- recurring checks;
- asset notes.

Maintenance can affect Inventory through explicit closures/out-of-service periods.

### Data Providers And Ingestion

Future high-impact platform area. It should support live adapters that pull from:

- OTA/channel providers;
- corporate APIs;
- email parsing;
- web parsing;
- file drops or manual imports.

The ingestion layer should own:

- provider config;
- adapter run state;
- cursors/checkpoints;
- raw payload references;
- normalized inbound commands/events;
- errors, retries, and health;
- admin controls for runs and adapters.

Product modules should receive normalized updates and remain agnostic to where data came from and how adapters run. This may later become a reusable GMA adapter/consumer capability.

Sequence this after Reservations once Properties, Inventory, and Reservations expose enough canonical model to update. It may come before or after Staff/Access depending on the first real product or integration need.

### Reporting And Analytics

Owns operational and financial views:

- occupancy;
- ADR/RevPAR-like metrics;
- balances;
- housekeeping workload;
- source/channel performance;
- staff activity;
- provider/import quality.

Early modules should emit enough events/facts to make reporting possible later.

### Audit And Business Day

Cross-cutting domain for:

- who did what;
- reason/notes for sensitive changes;
- business date/night-audit style accounting periods;
- close/reopen controls;
- immutable financial/action history.

Do not assume wall-clock date is always the operational business day.

### Staff Scheduling And Workforce

Potential later domain:

- shifts;
- availability;
- assignments;
- time-off;
- handoff notes.

Do not mix this into the initial staff access policy model.

### Communications

Potential later domain:

- staff-to-staff operational messages;
- reservation communication history;
- email/SMS/WhatsApp provider integration;
- templated messages.

Guest-facing portals are out of scope for now, but staff may still record or send communications to guests.

### Compliance And Retention

Potential later domain:

- document retention;
- data export/deletion;
- consent records;
- country-specific guest registration requirements;
- access restrictions around sensitive guest/financial data.

### Integrations Marketplace

Potential later domain for managed third-party integrations:

- connected apps;
- provider credentials;
- sync status;
- integration health;
- per-property enablement.

This overlaps with Data Providers/Ingestion but may become a separate management surface.

## Suggested Next Slice

Properties, Inventory, and the first Reservations lifecycle are complete. Choose the next active module from the first real product pressure: Guest Records for durable guest identity/history, Data Providers/Ingestion for importing external reservations, or Staff Profiles if operational assignments become the immediate need. Keep Rates, Billing, stay operations, and temporary holds separate until their workflows are concrete.
