# Ingestion PII Minimization Task

Status: implemented

## Goal

Reduce long-lived normalized guest PII in Ingestion before adding timed terminal-history redaction. Keep only the facts required for source ordering, idempotency, amendment classification, and active proposal decisions.

## Data Classification

- Raw observation payload: sensitive source evidence in GMA file storage, already governed by a persisted retention deadline.
- Dispatch snapshot: normalized guest/stay data required while an external product operation is pending and useful for bounded terminal recovery/audit.
- Proposal diff: sensitive review data required while a proposal is pending or applying and useful for bounded terminal decision history.
- Source-link baseline: operational comparison state used for future classification. It does not require guest name, email, phone, notes, guest count, source sequence, or full normalized JSON.
- Source identity/revision/hash: pseudonymous operational identifiers required for lookup, stale detection, and exact replay; not removed in this slice.

## Decision

Replace `ReservationSourceLink.LastAppliedNormalizedSnapshot` with a versioned operational baseline containing only:

- schema version;
- arrival date;
- departure date;
- sorted distinct inventory unit ids.

The baseline is strict JSON with no unmapped fields. It is produced only from a validated normalized reservation observation and is used only to classify a later upsert as guest-details-only versus allocation-affecting. Missing, malformed, or unsupported baselines remain conservative and classify as an amendment.

Existing PostgreSQL source-link data must migrate to the reduced shape. The provider-specific migration may transform the existing JSON column because migration projects own provider details; domain/application projects remain provider agnostic.

## Active Proposal Evidence

A pending or applying proposal still loads its original raw payload when accepted. Raw-payload retention must therefore exclude receipts referenced by proposals in either active state, even after the receipt itself is marked processed and its ordinary retention deadline passes.

Terminal proposals do not block ordinary raw-payload purge. Explicit legal holds are the separate additive exclusion implemented in [Ingestion Legal Holds Task](ingestion-legal-holds-task.md) and are not simulated by extending every retention deadline.

## Security And Reliability Invariants

- source links never persist primary guest name, email, phone, notes, or full normalized snapshots after migration;
- baseline serialization is deterministic so semantically identical unit sets produce identical text;
- malformed historical baseline data fails conservative, never auto-applies an allocation-sensitive change;
- active proposal evidence cannot be claimed for raw-payload purge;
- terminal proposal evidence follows the existing raw deadline until explicit legal-hold behavior is added;
- query filtering remains tenant scoped and the proposal exclusion is evaluated in the same database query as retention claiming;
- no cross-module query or foreign key is introduced;
- public proposal behavior and reservation history remain unchanged.

## Acceptance Checks

- guest-only changes still classify as `ChangeGuestDetails`;
- arrival, departure, or inventory changes classify as `Amend`;
- baseline JSON contains no guest PII and has deterministic unit ordering;
- missing, malformed, or future-version baseline classifies conservatively;
- PostgreSQL migration transforms existing valid snapshots and safely clears invalid/cancellation snapshots;
- pending/applying proposals prevent raw purge, while terminal proposals do not;
- real PostgreSQL coverage proves the retention exclusion and transformed model constraints;
- build, migration drift, fast tests, complete Docker tests, dependency guards, and submodule checks pass.

## Next Retention Slice

- terminal proposal-diff and dispatch-snapshot retention/redaction is implemented in [Ingestion Sensitive History Retention Task](ingestion-sensitive-history-retention-task.md);
- explicit property-scoped legal holds and held operational counts are implemented in [Ingestion Legal Holds Task](ingestion-legal-holds-task.md);
- keep active source-link operational baselines because they are already non-PII and required for classification.

## Completion Note

Implemented with PostgreSQL migration `20260712071441_MinimizeReservationSourceBaseline`. Source links now retain only the strict versioned operational baseline, and applied cancellations clear it. Legacy valid upsert snapshots are reduced during migration; malformed and cancellation snapshots are cleared conservatively.

Raw-payload claims now exclude receipts referenced by pending or applying proposals in the claim query itself. Proposal creation and the receipt transition from pending to processed share one transaction, so retention observes either the still-ineligible pending receipt or the committed active proposal. PostgreSQL coverage proves pending/applying exclusion, terminal eligibility, and legacy baseline transformation.
