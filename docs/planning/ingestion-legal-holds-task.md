# Ingestion Legal Holds Task

Status: implemented

## Goal

Add explicit, auditable property-scoped legal holds that stop deletion of raw observation payloads and redaction of normalized reservation history. A hold changes retention eligibility; it does not rewrite persisted retention deadlines and cannot restore data that was already purged or redacted.

## Domain Model

Each legal matter is an independently releasable `LegalHold` aggregate containing:

- tenant scope, hold id, and property id;
- a bounded reason;
- active or released state;
- placement actor and timestamp;
- optional release actor, release reason, and timestamp;
- an optimistic version.

More than one active hold may exist for a property. Retention remains blocked until every applicable hold is released. Released rows are permanent audit records and are never reused or deleted by ordinary retention.

Legal-hold reasons are privileged compliance metadata. They are available only through dedicated Admin API and Admin CLI operations protected by `ingestion.legal-holds.manage`; they are not added to ordinary management API contracts or connection health.

## Retention Enforcement

Raw-payload claim and sensitive-history redaction candidate queries exclude a row when any active legal hold exists for its tenant and property. The exclusion is evaluated in the same database query as deadline, lifecycle, active-proposal, and claim-state eligibility.

A property retention-fence version serializes hold placement and release with retention candidate selection:

- placing or releasing a hold advances the property fence in the same transaction as the hold change;
- a retention batch advances each selected property's fence in the same transaction as its claims or redactions;
- optimistic concurrency makes a concurrently committing hold or retention batch fail and retry instead of both succeeding from stale observations;
- placement fails explicitly while any raw payload for the property is already in the non-revocable `Purging` phase. A successfully placed hold therefore never claims to protect an external deletion that had already begun.

Releasing a hold permits later retention runs to process overdue data immediately. It does not trigger retention synchronously.

## Operator Surface

Admin API and Admin CLI provide property-scoped list, get, place, and confirmation-gated release operations. Placement records the authenticated actor. Release requires the hold id, expected version, release reason, authenticated actor, and explicit confirmation.

List operations are paged and may filter by active or released state. Responses expose hold metadata only; they never include raw payloads, proposal diffs, or dispatch snapshots.

## Health Evidence

Connection health remains factual and reports:

- the number of active legal holds on the property;
- expired raw payloads currently claimable;
- expired raw payloads protected by active proposal workflows;
- otherwise-claimable expired raw payloads protected by a legal hold;
- due normalized-history bodies currently claimable;
- due normalized-history bodies protected by a legal hold;
- already redacted normalized-history bodies.

The raw-payload categories are exclusive so operators can reconcile the backlog without double counting. Health exposes counts, not hold reasons or a compliance verdict.

## Migration

The PostgreSQL migration must:

- create the scoped legal-hold table with lifecycle and non-empty-text constraints;
- add the optimistic property retention-fence version and backfill it to zero;
- add property/state and placement-order indexes;
- preserve all existing retention deadlines and sensitive bodies;
- keep legal-hold lifecycle enforcement in the database as well as the domain.

## Host-Local Quarantine Decision

The `json.file-drop` quarantine is adapter-owned node-local input, may be malformed before a trustworthy property identity exists, and is not the durable Ingestion raw-payload record. It is outside property legal holds. The adapter now applies its own bounded, deployment-pausable local retention policy with count-only failure evidence; export, centralized inventory, and audited quarantine holds remain separate future work. The Ingestion hold never pretends to protect files it cannot address transactionally.

## Security And Reliability Invariants

- tenant query filters apply to all hold records and retention candidates;
- a hold for one property never affects or reveals another property;
- overlapping holds cannot be released as a group accidentally;
- hold placement/release is actor-attributed and optimistic;
- successful hold placement is serialized against retention claims and redactions;
- active holds change eligibility, never deadlines or historical audit facts;
- already purged or redacted data is never represented as recoverable;
- ordinary read and retention permissions do not grant access to legal-hold reasons;
- no cross-module query, foreign key, or provider concern enters domain/application code.

## Acceptance Checks

- place, list, get, and release respect tenant/property boundaries and dedicated permission metadata;
- multiple active holds block retention until the last one is released;
- release is confirmation-gated and rejects stale versions or repeated release;
- unknown properties and placement during an in-flight raw purge fail closed;
- raw and normalized-history candidate queries exclude held properties in PostgreSQL;
- candidate selection and hold changes advance the same optimistic property fence;
- health reports exclusive claimable, workflow-protected, legally-held, due, and redacted counts;
- migration constraints reject malformed lifecycle rows;
- build, migration drift, fast tests, complete Docker tests, dependency guards, and submodule checks pass.

## Completion Note

Implemented with PostgreSQL migration `20260712081938_AddLegalHolds`. Independently releasable hold aggregates preserve placement and release audit, while a dedicated permission protects their reasons from ordinary reads. Admin API and Admin CLI provide paged inspection, placement, and confirmation-gated release.

Raw-payload and normalized-history candidate queries exclude active holds in SQL. Hold changes and selected retention batches advance the same optimistic property fence, and placement rejects a raw payload already in the external-deletion phase. Connection health exposes exclusive claimable, workflow-protected, legally-held, due, and redacted counts without hold reasons. PostgreSQL coverage proves lifecycle constraints, migration backfill, overlapping holds, fence conflicts, HTTP/CLI authorization, held retention, and immediate eligibility after the final release.
