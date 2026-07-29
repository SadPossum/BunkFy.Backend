# Guests

The module's versioned personal-data policy is defined in
[`personal-data-catalog.v1.json`](personal-data-catalog.v1.json). The generated
[`personal-data-inventory.v1.md`](personal-data-inventory.v1.md) is checked by
reflection-backed tests against persistence, commands, queries, responses,
projection exports, and integration events.

Guests owns BunkFy's tenant-wide canonical guest profiles and staff-facing stay history. Guest records never imply authentication, membership, or guest-facing access.

## Current Slice

- bounded profile/contact fields with actor provenance, archive lifecycle, optimistic concurrency, and duplicate-contact allowance;
- property-scoped create, search, read, update, archive, and stay-history surfaces across public management API, Admin API, and Admin CLI;
- visibility through the profile's origin property or any current or historical
  stay association at the property;
- an in-process DataRights discovery contributor that accepts exactly one
  strong coordinate, returns bounded masked candidates, and revalidates opaque
  coordinates without exposing Guests persistence;
- a catalogue-versioned DataRights export contributor that revalidates the
  selected profile version and streams one profile plus only stay history for
  the authorized property;
- fail-closed embedded-catalogue validation at composition time, explicit
  one-hour transient export bindings, bounded field values and no staff audit
  attribution, normalized search copies or unrelated-property stays;
- normalization-aware profile update outcomes containing only changed field
  semantics, previous/current versions, event id and time, ready for later
  PII-free correction receipts without a second mutation path;
- approved DataRights correction execution with exact Guest-version binding,
  idempotent immutable receipts and no second profile mutation path;
- the processing-restriction owner foundation: reference-safe restriction
  records, immutable transition receipts, a versioned effective projection,
  initialization for every operationally visible Guest/property coordinate and
  a conservative PostgreSQL backfill;
- approved, idempotent restriction apply/release commands plus bounded active
  restriction listing, with exact approval intent and optimistic revisions;
- fail-closed ordinary detail, list, stay-history, update and archive
  enforcement while DataRights discovery, export, correction and restriction
  workflows retain explicit owner access;
- a versioned fail-closed restriction gate, constant-size transition event and
  bounded keyset rebuild export for dependent modules, with no direct identity,
  contact or free-text values crossing the boundary;
- independently releasable tenant/property/Guest data holds with stable reason
  codes, exact optimistic versions, immutable idempotent receipts, bounded
  listing and a dedicated permission absent from ordinary workspace roles;
- a versioned internal anonymisation-eligibility contract that resolves the
  bounded Guest property set from Guests-owned projections, checks every
  active hold, operational stay, property state and current erasure policy,
  compares the exact frozen routing-policy evidence, and returns only stable
  blocker codes and canonical digests;
- terminal Guest anonymisation that clears every profile and normalized-search
  personal-data field in one owner transaction, persists an immutable
  canonical-digest receipt and monotonic tombstone, and emits one PII-free
  event;
- a versioned restore contributor that accepts only verified DataRights ledger
  proof, re-scrubs restored profiles or recreates a PII-free terminal profile,
  and persists an immutable restore receipt plus attached tombstone proof;
- database-level exclusion of anonymised Guests from ordinary detail, list,
  discovery and export surfaces, plus operation locks that serialize new
  holds and owner projection changes against destructive eligibility;
- internal-only owner execution and restore commands exposed only through
  versioned DataRights contributor contracts; no public Guest surface can
  dispatch either command;
- explicit stay-projection contract versions so unknown future projection
  semantics block destructive eligibility instead of being interpreted
  optimistically;
- tenant-scoped automatic retention for the country-policy data class
  `guest-operational`, using a persisted fair-scan cursor, bounded set-based
  discovery, exact retry receipts, active-hold enforcement, and the latest
  applicable property-local stay deadline;
- authority-bound automatic anonymisation proof that cannot be replayed as a
  DataRights approval. The proof is currently local to Guests and is
  deliberately excluded from the DataRights subject export and protected
  replay ledger;
- PII-free profile and reservation/stay integration contracts;
- monotonic stay history that retains inactive replaced links for audit without granting visibility;
- local Properties projection plus task-driven rebuilds for Properties and Reservation stay history;
- PostgreSQL migrations, inbox/outbox, Worker composition, focused tests, and a real PostgreSQL/JetStream saga.

Reservations owns booking roles and current participant links. Guests owns profiles, visibility associations, and its history projection. Neither module writes the other's schema or uses cross-module foreign keys.

Identity documents, consent, orphan-profile retention triggers, duplicate
merge/split, entity resolution, guest flags, preferences, and guest accounts
remain deferred.
Protected export artifacts and download surfaces remain owned by DataRights.
Reservations owns its local restriction-eligibility projection and rechecks
this module's authoritative gate before every new canonical Guest link.
