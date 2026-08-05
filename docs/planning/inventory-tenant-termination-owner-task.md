# Inventory Tenant Termination Owner Task

Status: complete
Date: 2026-08-04

## Goal

Make Inventory an authoritative destructive tenant-termination owner without
moving hostel availability, allocation, topology-consumer, or anonymisation
policy into Data Rights or GMA.

The owner must close every late mutation and transport path, remove all
tenant-owned Inventory state in bounded resumable batches, and return exact
PII-free proof. Production execution remains disabled until all remaining
owners, protected replay, operator controls, and production admission are
complete.

## Ownership

- Inventory owns availability units, room sales configuration, manual blocks,
  reservation allocations and their unit rows, amendment decisions,
  allocation-operation locks, allocation anonymisation proof, bed and room
  retirement processes, replicated topology, transport journals, rebuild
  checkpoints, and local tenant lifecycle state.
- Inventory does not own Reservations or Properties. Reservations must remove
  booking authority first; Properties remains authoritative for physical
  topology and is destroyed later.
- Inventory has no independent legal-hold authority. Reservation and Guest
  holds remain with their owning modules and must not be duplicated here.
- Data Rights supplies approved process coordinates and stores only bounded
  owner status, counts, catalogue coordinates, and proof revisions.
- Workspaces supplies the exact frozen process and termination-epoch fence.
- GMA supplies provider-neutral transaction key locks and messaging admission
  hooks. No Inventory owner key, sales rule, table graph, or deletion policy
  belongs in GMA.

## Destruction Policy

Once Reservations has completed its destructive contribution, active,
released, rejected, and anonymised allocation state is all tenant-owned
Inventory history and may be removed. Availability blocks, sales
configuration, retirement workflows, operation locks, and replicated topology
are control state rather than independent termination holds.

A currently leased outbox message blocks progress until publication completes
or the lease expires. No other Inventory record is a local blocker after the
exact workspace fence and upstream owner proof have been accepted.

Completion retains only one closed tenant lifecycle row and one immutable,
PII-free destruction receipt bound to the exact operation, request digest,
selected/resulting revision, batch size, removal count, chained proof, and
timestamps.

## Lifecycle And Concurrency

- The existing tenant revision becomes the local `Open`, `Closing`, or
  `Closed` lifecycle fence. The first accepted destruction operation advances
  the selected revision exactly once.
- One operation id and one scope may identify only one canonical request.
  Equivalent retries resume or replay; changed coordinates conflict.
- Ordinary writes and admitted inbox handlers use the shared tenant lock.
  Export and destruction use its exclusive counterpart, so closure drains
  already-admitted transactions without serializing unrelated ordinary work.
- Scoped projections, rebuild checkpoints, inbox delivery, outbox creation,
  and outbox claims are rejected or excluded after local closing begins.
- Unknown or unavailable Workspaces fence state fails closed. Every attempt
  rechecks the exact process id, epoch, and frozen state.

## Bounded Removal

Each invocation removes at most one non-empty batch of 500 physical rows.
Allocation units are deleted explicitly before allocations, even though the
normal aggregate relationship cascades, so a parent delete cannot exceed the
declared bound. Empty stages advance in the same transaction.

Foreign-key-safe stages cover every tenant-owned table:

1. outbox and inbox journals;
2. allocation restore receipts, anonymisation receipts, tombstones, amendment
   decisions, allocation units, and allocations;
3. manual blocks and allocation-operation locks;
4. bed and room retirement processes plus room sales configuration;
5. inventory units, then bed, room, and property topology projections;
6. projection rebuild checkpoints; and
7. final absence verification and immutable receipt creation.

Every committed batch extends a versioned SHA-256 chain over the stage and
stable owner-local row keys. Central state receives only the cumulative count
and selected/resulting revisions.

Existing PostgreSQL anonymisation proof remains immutable for ordinary work.
Destruction may delete it only when transaction-local operation, scope, and
request-digest coordinates match the live local `Closing` state. Updates
remain prohibited. The final destruction receipt is independently append-only
in application and PostgreSQL layers.

## Ordering

Inventory keeps its `Properties` dependency for export. For destruction it
depends on Reservations, ensuring booking authority and blockers are resolved
before allocations disappear. Properties destruction will depend on Inventory
and other remaining topology consumers before removing authoritative topology.

## Acceptance

- export behavior and its deterministic 11-stream schema remain unchanged;
- equivalent retries resume and completed retries replay exactly;
- changed operation coordinates conflict;
- closing suppresses scoped operational, projection, checkpoint, inbox, and
  outbox mutation paths and excludes new outbox claims;
- active outbox leases cannot be deleted beneath a publisher;
- one call removes at most one non-empty batch of 500 physical rows;
- every tenant-owned Inventory table is empty at completion except lifecycle
  state and the immutable receipt;
- normal anonymisation proof mutation and deletion remain blocked;
- another tenant remains untouched;
- migration, provider trigger authorization, lock drain, foreign-key order,
  replay, admission, batch bounds, receipt immutability, and tenant isolation
  are proven once at the end of the slice; and
- the complete fast Inventory suite passes before one exact PostgreSQL 16
  scenario is run.

## Deferred Production Work

- cross-owner preflight and final active-booking assurance;
- protected replay delta generation and restore-readiness enforcement;
- operator API, Admin API, CLI, assurance, retry, and recovery UX; and
- production admission tied to the exact final mandatory-owner catalogue.

## Verification

- all 84 fast Inventory tests pass;
- the Integration test project and PostgreSQL migration project build with
  zero warnings and zero errors;
- EF reports no pending Inventory model changes after
  `AddInventoryTenantDestructionLifecycle`;
- the exact PostgreSQL 16 scenario proves shared/exclusive lock drain, active
  outbox-lease blocking, closed-scope claim suppression, bounded 500-row
  progress, foreign-key-safe graph removal, exact replay and conflict,
  application and database proof immutability, and tenant isolation; and
- the provider proof exposed and fixed a copied query defect in the Inventory,
  Reservations, Staff, Ingestion, and Guests outbox stores: claim admission now
  compares the mapped lifecycle status instead of the unmapped `IsOpen`
  convenience property.
