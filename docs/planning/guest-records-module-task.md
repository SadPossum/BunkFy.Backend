# Guest Records Module Task

Status: first slice implemented
Date: 2026-07-12

## Goal

Build `Guests` as the tenant-wide canonical owner of staff-managed guest profiles while preserving property-scoped operational access. A guest record is not an authentication identity and never implies login, credentials, membership, or guest-facing access.

The first complete slice must make canonical guests useful to Reservations without moving booking ownership into Guests: Reservations owns which guests participate in a booking and their booking roles; Guests projects those links and later lifecycle facts into a staff-readable stay history.

## Ownership

Guests owns:

- canonical guest identity and contact data;
- guest lifecycle (`Active` or `Archived`);
- the property associations through which ordinary staff may discover a guest;
- a local, rebuildable projection of reservation/stay history;
- guest-specific privacy and visibility rules.

Reservations owns:

- the booking participant link and role;
- the primary guest/contact snapshot used by the booking;
- reservation and stay state transitions;
- versioned facts that Guests projects into stay history.

Auth identities, tenant memberships, roles, and grants remain in GMA Auth/AccessControl. Inventory, Billing, Files, and Ingestion retain their existing ownership. There are no cross-module foreign keys or cross-schema writes.

## Profile Model

The initial `GuestProfile` aggregate is tenant scoped and has:

- stable `GuestId`;
- required culture-neutral `DisplayName` and optional `LegalName`;
- optional email, phone, date of birth, nationality country code, preferred language tag, and staff notes;
- an origin property association;
- `Active` or `Archived` status;
- optimistic `Version`;
- created/last-changed UTC timestamps and authenticated actor provenance.

Email and phone are not unique identities. Families, shared contacts, provider placeholders, formatting differences, and recycled contact points make uniqueness unsafe. Search may surface possible matches, but the module must never auto-merge or reject a valid profile solely because contact data overlaps.

Profile integration events contain identity, status, origin/property association, and version facts only. They must not publish names, contact details, birth dates, notes, or document data through the general integration bus.

## Property Visibility

Canonical profiles are tenant-wide, but ordinary public management operations are property scoped:

- creating a guest at a property creates an `Origin` association;
- linking a guest to a reservation creates or confirms a `Stay` association in the Guests projection;
- list/search/get/update/archive routes require a property association and a property-scoped permission;
- lack of an association denies by default, even when the caller knows the guest id;
- Admin API/CLI use the same application commands and property boundary for this slice.

The first slice consumes Properties lifecycle facts into a local projection and rejects creation/link visibility against missing or retired properties. A rebuild source repairs that projection.

## Reservation Participants

Reservations gains a collection-backed participant model rather than a single hard-coded guest foreign id. Stable roles begin with `Primary`; later `Occupant`, booker/contact, group, and per-guest arrival/departure roles may append without replacing the relationship shape.

For the first slice:

- a reservation may have at most one primary canonical guest;
- linking requires an active guest in Reservations' local Guests projection;
- linking is expected-version controlled and idempotent for the same guest/role;
- replacing a primary link is explicit and emits a new versioned fact;
- the booking's existing primary guest/contact snapshot is not silently overwritten by profile changes;
- archived guests cannot be newly linked, but historical links remain valid.

`ReservationGuestLinked` carries no guest PII. It includes tenant, property, reservation, guest, role, stay range, current reservation status, lifecycle business dates, and reservation version so late/out-of-order consumers can establish current history safely.

## Stay History

Guests consumes reservation participant and lifecycle facts into a Guests-owned projection keyed by tenant, guest, and reservation. The projection records property, stay range, role, current reservation status, business dates, and the latest reservation version.

Handlers are monotonic by reservation version. Lifecycle events that arrive before a participant link do not create an unowned guest history row; the later link snapshot supplies current state. Duplicate and out-of-order delivery cannot regress history. A Reservations export source and Guests rebuild task repair missed facts.

## Surfaces And Permissions

Public management API, Admin API, and Admin CLI expose property-scoped create, list/search, get, update, and archive operations. Admin archive is confirmation gated. Initial permissions are distinct:

- `guests.read`;
- `guests.create`;
- `guests.manage`;
- `guests.archive`.

Reservations adds `reservations.manage-guests` for participant links rather than reusing broad lifecycle management.

## Persistence And Security

- PostgreSQL is the first provider migration; domain/application remain provider agnostic.
- Every guest-owned table is in the `guests` schema and visibly tenant scoped.
- Sensitive fields are bounded and never written to logs, operation names, integration subjects, or general event payloads.
- Query filters enforce both tenant and property association in SQL; do not authorize once per result row.
- Archive is reversible only through a later explicit policy; it is not erasure.
- Search is bounded and paged; invalid status/filter values fail validation.
- Actor ids and lifecycle shapes have database constraints.

## Verification

- domain tests cover profile validation, updates, archive, versions, and idempotency;
- application tests cover tenant/property visibility, actor provenance, duplicate-contact allowance, and archived behavior;
- contract tests cover stable enums, permission metadata, event privacy, and subject names;
- persistence tests cover scoped keys/indexes, constraints, property associations, and monotonic stay history;
- Reservations tests cover participant-role uniqueness, active-guest projection checks, replacement, and event snapshots;
- a real PostgreSQL/NATS scenario proves property-scoped profile access, reservation linking, stay-history convergence through check-in/check-out, stale-version denial, duplicate delivery, and cross-property denial;
- build, migration drift, non-Docker tests, Docker tests, dependency audits, and GMA submodule checks pass.

## Deferred

- identity documents, document files, verification, and expiry workflows;
- consent/legal-basis records and communication preferences;
- duplicate review, merge/split, survivorship, and source-identity reconciliation;
- subject-access export, anonymization, erasure, and jurisdiction-specific retention;
- fuzzy/entity-resolution matching and transliteration;
- guest flags, bans, VIP/loyalty, preferences, and accessibility needs;
- household/company/group relationships;
- guest accounts, login, portals, messaging, or self-service;
- per-guest check-in/out and room/bed assignment;
- billing balances, payment instruments, and folios.

## Implementation Checkpoint

The first slice is implemented in `src/Modules/Guests` with tenant-wide canonical profiles, property-scoped visibility, PII-free lifecycle contracts, public/Admin API and Admin CLI surfaces, PostgreSQL migrations, and projection rebuild tasks. Reservations owns versioned primary-guest links behind `reservations.manage-guests`, validates them against a local rebuildable Guest eligibility projection, and retains replaced links as inactive audit records.

Guests consumes current-link and reservation lifecycle snapshots into monotonic stay history. Active stay links extend property visibility; replaced links remain auditable but do not grant visibility. Properties, Guest eligibility, and stay history projections all have live event paths and task-driven rebuild sources. Focused tests and a real PostgreSQL/JetStream saga cover profile creation, projection, linking, idempotent retry, check-in/out convergence, stale event protection, and cross-property denial.
