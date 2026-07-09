# Properties Follow-Up Notes

Status: temporary parking-lot note; non-permission hardening items partly completed
Date: 2026-07-09

These items came out of the Properties audit and are intentionally parked while access policy design is discussed separately.

## Deferred Until After Policy Work

- Docker-backed persistence/admin verification should wait until the policy/access-control path is ready enough to validate real admin flows.
- Remaining product-model follow-ups should wait until policy boundaries are clear, so Properties does not grow ahead of the access model.

## Completed In Current Slice

- Added a tenant-aware database relationship from rooms to properties inside the Properties schema.
- Added persistence model coverage for the room/property relationship.
- Changed nested list queries to return explicit not-found failures when the parent property or room is missing.
- Replaced bed list in-memory pagination with a direct read query.
- Added Properties integration event/status contract coverage and topology projection export contracts.

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

## Product Model Follow-Ups

- Consider property address/contact/display metadata before guest-facing communications, invoices, or operational reports need it.
- Consider explicit display/sort order for rooms and beds before operators need stable non-alphabetical ordering.
- Consider property archival/retirement semantics before reservations depend on active property topology.
- Keep Building/Floor as labels until product rules prove they need aggregates.
- Do not move inventory, maintenance closures, housekeeping, or reservation availability into Properties. Properties should stay the physical topology source.
