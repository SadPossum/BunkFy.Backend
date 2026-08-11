# Operations Notifications Extension

This extension converts selected BunkFy product events into already-addressed
GMA notification requests. Source modules own business facts, BunkFy owns
recipient and content policy, Organizations authoritatively filters active
membership, and Notifications owns generic inbox persistence and delivery.

The executable personal-data catalogue is
[personal-data-catalog.v1.json](personal-data-catalog.v1.json). Its deterministic
resolved view is
[personal-data-inventory.v1.md](personal-data-inventory.v1.md).

Payloads are a closed set of typed navigation records. Product source events may
contain richer audit or workflow data, but the inbox receives only the minimal
resource identifiers and dates needed to understand or open the affected item.

Property fan-out is least-privilege as well as membership-scoped. Every
property notification declares the read permission for the destination it
opens; candidate Staff and workspace-owner recipients are batch-authorized at
the exact property scope before any copy is persisted. Property, Inventory,
Reservations, Ingestion, and Data Rights retain ownership of their permission
codes. Direct Staff lifecycle copies remain addressed only to the affected
active Staff account.

Data Rights owns Guest Rights deadline evaluation, append-only dispatch
receipts, and the transactional outbox event. This extension resolves the
current operational audience and then batch-authorizes every candidate for
`data-rights.read` at the exact property scope before creating a mandatory
inbox notification. The notification payload contains only property and case
identifiers: requester identity, requested rights, notes, and policy evidence
remain in Data Rights. The case identifier is governed through the authorized
Data Rights case policy rather than treated as an ordinary guest-history copy.

Every BunkFy-addressed copy carries two opaque lifecycle coordinates: one for
the tenant's complete BunkFy operational history and one for the authoritative
Staff record of its recipient. The Staff coordinate survives Auth account
relinking and supports exact Staff access export and anonymisation. Those exact
coordinates remain subject-rights tools; whole-tenant termination uses GMA
Notifications' separate scope lifecycle.

Reservation notifications carry an opaque reference to their authoritative
Reservations record. Provider-operation attention notifications additionally
carry the authoritative Ingestion reservation source-link reference, resolved
once from the exact dispatch before audience fan-out. Their Guest Rights
companions support bounded discovery, strict buffered export, exact closure,
replay suppression, and record-specific restore. The extension depends only on
Ingestion Contracts, and the provider bridge is registered only by API or
Worker hosts that also compose Ingestion.

GMA Notifications owns generic inbox persistence, paging, delivery-lease
coordination, close receipts, replay suppression, and the product-neutral scope
lifecycle. This extension adapts those primitives to BunkFy's Staff,
reservation, Ingestion source-link, and tenant-termination policies; it owns no
database and no BunkFy policy is implemented in GMA.

Tenant owner catalogue v3 supports both `Export` and `Destroy`. The export
selects one monotonic GMA scope revision and streams 12 deterministic typed
stores: user notifications, preferences, routes, tag definitions, deliveries,
attempts, tenant broadcasts and reads, plus history-reference lifecycle state
and proof. Transport inbox rows and platform-global broadcasts remain excluded.
Every field is bound to the protected tenant-portability surface; preference
data is not admitted to the ordinary notification surface.

Destruction requires the matching frozen Workspace fence and invokes GMA's
bounded resumable scope lifecycle. The first accepted batch installs the scope
tombstone; retries reuse the owner idempotency key, validate durable progress or
the payload-free receipt, and report completion only after disposable scope
state is gone. GMA retains only its scope tombstone and authorized lifecycle
proof needed for replay suppression.

## Production Admission

GMA's generic retention engine remains disabled until BunkFy supplies an
approved product policy. API, Worker, and Admin API share a fail-closed
production admission contract tied to the exact embedded catalogue version and
SHA-256, approved runtime windows, one cleanup owner, and an explicit
pre-reference history disposition.

Repository defaults remain pending and are not production-admissible. The
deployment workflow is documented in
[Operations Notifications Production Admission](../../../../docs/operations/operations-notifications-production-admission.md).
