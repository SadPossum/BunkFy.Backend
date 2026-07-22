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
