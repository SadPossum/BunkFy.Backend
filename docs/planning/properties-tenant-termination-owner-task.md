# Properties Tenant Termination Owner Task

Status: complete
Date: 2026-08-04

## Goal

Make Properties an authoritative destructive tenant-termination owner without
moving physical-topology policy into Data Rights, Workspaces, or GMA.

The owner must close every late topology and transport mutation, remove all
tenant-owned Properties state in bounded resumable batches, and return exact,
PII-free proof. Production execution remains disabled until the remaining
owners, protected replay, operator controls, and production admission are
complete.

## Ownership

- Properties owns property, room, and bed topology; property governance
  bindings and acknowledgements; immutable governance revisions; transport
  journals; and local tenant lifecycle state.
- Properties does not own sellable inventory, bookings, guests, staff,
  ingestion evidence, retention schedules, notifications, workspace identity,
  or access policy. Those owners must remove their copies first.
- Data Rights supplies approved process coordinates and stores only bounded
  owner status, counts, catalogue coordinates, and proof revisions.
- Workspaces supplies the exact frozen process and termination-epoch fence.
- GMA supplies provider-neutral shared/exclusive transaction-key locks and
  messaging admission hooks. No topology table, owner key, or deletion order
  belongs in GMA.

## Destruction Policy

Physical topology and its governance audit are tenant-owned business records,
not independent termination holds. Destruction may begin only after Inventory
has removed sellable topology and after the Operations Notifications and
Retention branches have completed, transitively covering Reservations, Guests,
Staff, and Ingestion consumers.

A currently leased outbox message blocks progress until publication completes
or the lease expires. No Properties-local record is a legal hold after the
exact workspace fence and upstream owner proofs have been accepted.

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
- Ordinary topology writes and admitted inbox handlers use the shared tenant
  lock. Export and destruction use its exclusive counterpart, so closure drains
  already-admitted transactions without serializing unrelated ordinary work.
- Closing suppresses topology, governance, inbox, outbox, and publisher-claim
  mutation paths.
- Unknown or unavailable Workspaces fence state fails closed. Every attempt
  rechecks the exact process id, epoch, and frozen state.

## Bounded Removal

Each invocation removes at most one non-empty batch of 500 physical rows.
Empty stages advance in the same transaction. Foreign-key-safe stages cover
every tenant-owned table:

1. outbox and inbox journals;
2. property governance revisions and acknowledgements;
3. beds, then rooms, then properties; and
4. final absence verification and immutable receipt creation.

Owned collection rows are deleted explicitly before their parents so cascade
behavior cannot exceed the declared physical-row bound. Every committed batch
extends a versioned SHA-256 chain over the stage and stable owner-local row
keys. Central state receives only the cumulative count and selected/resulting
revisions.

Governance revisions remain append-only for ordinary work. Destruction may
delete them only when transaction-local operation coordinates match the live
local `Closing` state. The final destruction receipt is independently
append-only in application and PostgreSQL layers.

## Ordering

Properties retains its Workspaces dependency for export. For destruction it
depends on Inventory, Operations Notifications, and Retention. Those three
predecessors cover the independent topology-consumer branches without forcing
Properties to wait for generic Organizations, Access Control, or Task Runtime
owners that do not own physical topology.

## Acceptance

- export behavior and its deterministic five-stream schema remain unchanged;
- equivalent retries resume and completed retries replay exactly;
- changed operation coordinates conflict;
- closing suppresses scoped operational, governance, inbox, and outbox paths
  and excludes new outbox claims;
- active outbox leases cannot be deleted beneath a publisher;
- one call removes at most one non-empty batch of 500 physical rows;
- every tenant-owned Properties table is empty at completion except lifecycle
  state and the immutable receipt;
- normal governance-revision mutation and deletion remain blocked;
- another tenant remains untouched;
- migration, provider trigger authorization, lock drain, foreign-key order,
  replay, admission, batch bounds, receipt immutability, and tenant isolation
  are proven once at the end of the slice; and
- the complete fast Properties suite passes before one exact PostgreSQL 16
  scenario is run.

## Deferred Production Work

- cross-owner preflight and final topology-consumer assurance;
- protected replay delta generation and restore-readiness enforcement;
- operator API, Admin API, CLI, assurance, retry, and recovery UX; and
- production admission tied to the exact final mandatory-owner catalogue.

## Verification

- all 82 fast Properties tests pass;
- the PostgreSQL migration project builds with zero warnings and EF reports no
  pending model changes;
- the exact PostgreSQL 16 destruction scenario passes with 507 tenant-owned
  rows, including a bounded `500 + 1` bed split; and
- the provider proof covers transaction-lock drain, active outbox-lease
  handling and claim suppression, governance trigger authorization, exact
  replay/conflict, closed admission, append-only receipt enforcement, and
  preservation of another tenant's property, room, bed, acknowledgement, and
  governance revision.

The provider scenario exposed and corrected a tenant-isolation defect before
completion: governance revisions do not implement the ambient scoped-entity
contract, so destruction now applies explicit scope predicates to every source
and final absence check instead of relying on query-filter conventions.
