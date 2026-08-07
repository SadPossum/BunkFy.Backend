# Properties

Properties owns BunkFy's tenant-scoped physical accommodation topology:
properties, rooms, owned beds, facility labels, time zones, lifecycle state,
and immutable projection ordering. It publishes versioned topology facts for
consumer-owned projections and never writes another module's schema.

The module's executable personal-data contract is
[`personal-data-catalog.v1.json`](personal-data-catalog.v1.json). Its generated
[`personal-data-inventory.v1.md`](personal-data-inventory.v1.md) is checked by
reflection- and EF-model-backed tests. Property, room, building, floor, and bed
labels describe the facility and remain outside the personal-data catalogue.

Properties carries only two current person-linked coordinates: the transient
authenticated subject used to resolve visible property scopes, and the bounded
actor reference propagated on property retirement for audit correlation and
self-notification suppression. Neither is stored in Properties topology or
returned through API, admin, or projection-export contracts. Catalogue policy
values are engineering defaults until country, retention, and rights approval.

Properties does not own guests, reservations, sellable inventory, staff
profiles, accounts, roles, grants, rates, provider mappings, maintenance,
housekeeping, or arbitrary notes. Consumers keep their own projections and do
not read Properties tables.

For tenant termination, Properties is the mandatory `properties` owner.
Export depends on Workspaces and emits the authoritative property, governance
acknowledgement, room, bed, and governance-revision records. Destruction waits
for Inventory, Operations Notifications, and Retention, then removes all
tenant-owned topology, governance history, and transport journals in explicit
scope-bound batches of at most 500 physical rows. It retains only closed local
lifecycle state and one immutable, PII-free destruction receipt. Derived
projections, inbox/outbox messages, and local consistency state are not
portable business data and remain excluded from export.

Relational operational writes and tenant export selection share the BunkFy
tenant-mutation transaction key. A write rechecks the Workspaces termination
fence under that key and fails closed when admission is unavailable; each
successful Properties unit of work advances one tenant-local revision. Export
holds the same key in a repeatable-read transaction and must return the same
selected and resulting revision. PostgreSQL also rejects updates or deletes to
property governance revisions independently of the application guard, except
for an exact live destruction operation bound to the local closing state.

## Operational surface

Property, room, and bed directories return minimized rows in stable
code/name/label-and-id order. Pages use one-row lookahead and report `HasMore`;
they do not run exact-count queries. Detail endpoints remain the authority for
full property and room state.

Ordinary topology writes return contract-owned receipts containing only the
affected identifiers, status, and concurrency versions. Clients must invalidate
and refetch the reads they display. Public and Admin HTTP routes emit no-store
headers, and Admin endpoints declare their success response shape explicitly.

Property creation is keyed by a caller-supplied operation id that becomes the
property id. Each attempt rechecks workspace admission and serializes that
tenant-scoped coordinate before reading or creating topology. An exact
normalized retry returns the current minimal receipt without another event;
reusing the id for different property details is a conflict. Property-code
uniqueness remains independently serialized, and no request payload is retained
for replay.

Multi-bed creation is one atomic room command. The complete label set is
validated before mutation, is limited to 100 beds, and publishes the existing
bed-added fact once per created bed. The single-bed command remains available
for compatibility and follows the same aggregate rules.

The topology projection rebuild export still pages property roots while
embedding each selected property's rooms and beds. Replacing it requires a
versioned protocol coordinated with every projection consumer; it must not be
changed as an incidental read-performance optimization.
