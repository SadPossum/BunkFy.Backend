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
- PII-free profile and reservation/stay integration contracts;
- monotonic stay history that retains inactive replaced links for audit without granting visibility;
- local Properties projection plus task-driven rebuilds for Properties and Reservation stay history;
- PostgreSQL migrations, inbox/outbox, Worker composition, focused tests, and a real PostgreSQL/JetStream saga.

Reservations owns booking roles and current participant links. Guests owns profiles, visibility associations, and its history projection. Neither module writes the other's schema or uses cross-module foreign keys.

Identity documents, consent and retention workflows, duplicate merge/split,
entity resolution, guest flags, preferences, and guest accounts remain deferred.
Protected export artifacts and download surfaces remain owned by DataRights.
The versioned restriction gate, transition event, rebuild export and
Reservations-owned eligibility projection are the next Guests slice.
