# Properties Follow-Up Notes

Status: core module complete; later product follow-ups are non-blocking
Date: 2026-07-09

These items came out of the Properties audit and were parked while access policy design was developed in GMA. The policy track reopened on 2026-07-10. Active sequencing, policy work, and the expanded audit now live in [GMA Access Control And Properties Alignment](gma-access-control-properties-alignment.md).

## Original Deferral

- Docker-backed persistence/admin verification should wait until the policy/access-control path is ready enough to validate real admin flows.
- Remaining product-model follow-ups should wait until policy boundaries are clear, so Properties does not grow ahead of the access model.

The dependency baseline, GMA follow-ups, persisted AccessControl composition, Properties policy implementation, topology lifecycle/versioning work, and product-specific Docker authorization scenarios are complete.

## Completed In Current Slice

- Added a tenant-aware database relationship from rooms to properties inside the Properties schema.
- Added persistence model coverage for the room/property relationship.
- Changed nested list queries to return explicit not-found failures when the parent property or room is missing.
- Replaced bed list in-memory pagination with a direct read query.
- Added Properties integration event/status contract coverage and topology projection export contracts.
- Migrated Properties ownership to GMA scope primitives while preserving tenant-shaped product contracts.
- Added tenant/property access scopes, fail-closed endpoint policies, and explicit parent verification for room/bed operations.
- Added one-query property-list visibility based on granted tenant or exact property scopes.
- Added terminal property retirement and explicit room-to-bed retirement cascade behavior.
- Added optimistic concurrency preconditions and monotonic property, room, and bed versions across DTOs, events, and exports.
- Replaced mutable-code rebuild paging with an immutable generated property ordinal.
- Added a data-preserving PostgreSQL migration and Docker coverage for migration upgrade, rebuild ordering, and stale writes.
- Added real-token Docker coverage for tenant/property grants, SQL-filtered visibility, cross-scope denial, and immediate revocation.
- Fixed Properties outbox scope propagation and verified retirement payloads plus composed-host export resume.

## Integrity And Persistence

- Keep the no cross-module foreign key rule. This relationship is allowed because both tables belong to Properties.

## Query Semantics

- Nested list endpoints now return not-found when the parent is missing:
  - `GET /api/properties/{propertyId}/rooms`
  - `GET /api/properties/rooms/{roomId}/beds`

## Read-Path Scale

- Keep the aggregate path for commands. The read path can be optimized without changing room/bed ownership.

## Contract And Rebuild Safety

- Keep external provider ids out of Properties events unless we make a separate product decision. Provider identity should probably belong to an ingestion/provider module or local projections.
- Normalize concurrent unique-key violations into stable conflict responses before operator-heavy concurrent editing becomes a real workflow.

## Product Model Follow-Ups

- Consider property address/contact/display metadata before guest-facing communications, invoices, or operational reports need it.
- Consider explicit display/sort order for rooms and beds before operators need stable non-alphabetical ordering.
- Keep Building/Floor as labels until product rules prove they need aggregates.
- Do not move inventory, maintenance closures, housekeeping, or reservation availability into Properties. Properties should stay the physical topology source.
