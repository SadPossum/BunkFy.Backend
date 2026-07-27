# Inventory Data-Rights Owner Capability Task

Status: implemented; local production verification complete

## Goal

Make Inventory a complete owner in the Data Rights workflow. An authorized
operator must be able to discover, export, select, anonymize, and restore the
Inventory records linked to a reservation without moving hospitality policy or
Inventory semantics into GMA.

This is one Inventory-only production slice. It consumes the generic
multi-owner orchestration already owned by Data Rights.

## Ownership Boundary

- Inventory owns allocation state, assigned inventory units, stay dates,
  allocation request correlation, release correlation, and amendment decision
  records.
- Reservations owns the reservation, guest-facing booking details, and stay
  lifecycle that determines whether anonymisation is operationally safe.
- Data Rights owns case approval, selected subjects, work-item dispatch,
  terminal reconciliation, the canonical processing ledger, and restore
  coordination.
- GMA owns generic CQRS, transactions, scoping, messaging, tasks, and
  persistence mechanics. No Inventory field, record type, eligibility rule, or
  redaction behavior belongs in GMA.

## Subject Coordinate

Inventory exposes one subject coordinate:

- owner: `inventory`
- record type: `allocation`
- record id: Inventory allocation id
- record version: Inventory allocation version

Discovery accepts only an exact reservation id. Inventory does not search by
name, email, phone, or date of birth because it does not own those identifiers.
The candidate discloses bounded allocation context and no new direct identity.

## Export

The Inventory export contains only the reviewed allocation fields required to
explain how capacity was assigned:

- allocation id and version;
- reservation id;
- property id;
- arrival and departure dates;
- allocation state and rejection code;
- allocation, release, and amendment correlation ids;
- assigned inventory-unit ids; and
- lifecycle timestamps.

Export is scoped to the selected property and must fail closed for a missing,
stale, anonymised, or cross-scope coordinate.

## Anonymisation

An allocation is eligible only when:

- the selected coordinate and property still match;
- the allocation version is unchanged;
- the allocation is `Released` or `Rejected`; and
- the approved Data Rights evidence still matches the operation.

Anonymisation must:

1. serialize against allocation mutation;
2. replace the direct reservation correlation with a deterministic owner-proof
   surrogate;
3. remove all persisted amendment decisions for the allocation;
4. preserve non-identifying property, date, unit, state, and rejection history;
5. mark the allocation anonymised and increment its version once;
6. create an idempotent owner receipt and protected tombstone in the same
   transaction; and
7. return a canonical proof that Data Rights can seal into its ledger.

The allocation, allocation-request, and release-request ids remain bounded
idempotency and audit pseudonyms. Retaining those ids prevents an old allocation
request from recreating capacity after inbox cleanup. The original reservation
id may remain only in bounded proof material required for idempotency and
restore verification; it must not remain in ordinary allocation or
amendment-decision queries.

## Restore

Data Rights restore replay must reproduce the exact anonymised owner state
after a database restore:

- derive the same reservation surrogate from the owner receipt id;
- reapply the terminal allocation mutation when the restored row predates the
  anonymisation;
- remove restored amendment decisions;
- attach or recreate the tombstone;
- create an idempotent restore receipt tied to the canonical ledger entry; and
- fail closed when the allocation, original proof, resulting version, or
  canonical hashes conflict.

Restore must also prove the already-anonymised and missing-record cases without
manufacturing ordinary business data.

## Persistence

Add Inventory-owned:

- allocation anonymisation state and completion timestamp;
- owner receipt;
- tombstone with restore revision;
- restore receipt; and
- a durable allocation operation-lock row if the existing allocation row
  cannot safely serialize create, amend, release, and anonymisation races.

Every new table and index remains tenant scoped. Receipt and tombstone hashes
use canonical lowercase SHA-256 text and are covered by constraints.

## Catalogue

Update the Inventory personal-data catalogue and deterministic inventory for:

- discovery request/response surfaces;
- export descriptor and records;
- anonymisation commands, receipts, tombstones, and restore receipts;
- allocation anonymisation fields; and
- the removal of amendment decisions as an erasure action.

No new field may be allowed on log, metric, trace, notification, or support
bundle surfaces.

## Verification

1. Discovery accepts only an exact reservation id and enforces tenant/property
   scope.
2. Selection validation rejects missing, stale, anonymised, and cross-property
   allocations.
3. Export is deterministic, capped at 1,000 owner records, fails closed before
   writing on overflow, and rejects stale or anonymised coordinates.
4. Active allocations block anonymisation independently of Reservations.
5. Successful anonymisation severs every ordinary reservation correlation and
   removes amendment decisions atomically.
6. Repeated execution returns the same proof; conflicting idempotency fails.
7. Concurrent allocation mutation cannot produce a partial redaction.
8. Restore reproduces the exact result and is idempotent.
9. PostgreSQL migration backfill preserves existing allocations as
   non-anonymised.
10. Inventory catalogue guards, architecture tests, migration drift checks,
    complete non-Docker tests, Docker tests, and preview worker execution pass.

## Deferred

- automatic retention scheduling and legal-hold execution;
- correction and restriction operations for Inventory;
- deletion of generic processed message journals before their configured
  retention expires;
- founder or counsel approval of the engineering-default retention and rights
  policy; and
- any cross-property or cross-workspace subject search.

## Implemented Evidence

- exact-reservation discovery and deterministic allocation/amendment export
  with a fail-closed 1,000-record ceiling;
- terminal allocation anonymisation with deterministic reservation
  pseudonymisation, amendment-decision removal, and versioned owner proof;
- durable operation-lock rows shared by amend, release, anonymise, and restore;
- idempotent owner receipt, tombstone, and restore receipt with canonical
  SHA-256 verification;
- ordinary discovery/export suppression after anonymisation;
- PostgreSQL migration with existing-allocation lock backfill;
- six executable catalogue fields resolving 582 concrete bindings; and
- all 64 Inventory tests, all 65 architecture tests, all 2,759 non-Docker
  tests, all 59 Docker integration tests, solution synchronization, and
  repository-wide migration drift verification.

The Docker suite includes a previous-schema upgrade proof that preserves an
existing allocation, initializes its anonymisation state safely, and backfills
its operation-lock row. The production-shaped preview applied the migration to
14 existing allocations, backfilled 14 operation-lock rows, passed same-origin
health and smoke checks, and completed a fresh Worker schedule cycle without a
recurring startup error. Exact-commit CI remains required before this slice is
marked published.
