# Ingestion Operational Surface Hardening Task

Status: implemented
Date: 2026-08-04

## Goal

Bound ordinary Ingestion reads and management write responses so high-volume
adapter activity remains efficient and each permission returns only the
operational data required by its use case.

## Ownership

- GMA Pagination owns normalized page and page-size limits.
- BunkFy Ingestion owns adapter lifecycle, observation provenance, proposal
  review semantics, response shapes, deterministic ordering, and sensitive-data
  classification.
- Adapter and parser abstractions continue to own protocol limits. Reprocessing
  outputs remain bounded by `ObservationParserLimits.MaximumOutputs`.
- API, Admin API, Admin CLI, and web consumers must preserve the same bounded
  application contracts.

## Invariants

- Connection, run, receipt, reprocessing-attempt, proposal, and credential
  directories project use-case-specific list items directly from persistence.
- Paged operator directories derive `HasMore` from one bounded look-ahead row;
  they do not run an exact count query.
- Every directory has deterministic domain-appropriate ordering with an
  identifier tie-breaker; activity streams remain newest-first.
- Full connection, run, receipt, reprocessing-attempt, and proposal details are
  available only through their explicit detail queries.
- The ordinary proposal list does not disclose sensitive diffs, decision actor
  data, decision reasons, retention metadata, or product operation identifiers.
- Connection management, credential revocation, and proposal decisions return
  minimal mutation receipts. One-time credential issuance remains the explicit
  exception because its token can be returned only once.
- Sensitive operational and personal-data reads prevent shared HTTP caching.
- Public and administrative HTTP surfaces declare their success response
  schemas explicitly, and CLI and web consumers use the same contracts.
- Existing tenant, property, country-policy, assurance, lifecycle, concurrency,
  ingress, retention, and raw-payload permission checks remain fail-closed.
- New response members are present in the executable personal-data catalogue.

## Delivery

1. [Completed] Introduce minimized list items, look-ahead envelopes, and mutation receipts.
2. [Completed] Replace exact-count repository reads with direct bounded projections.
3. [Completed] Align command handlers, API, Admin API, and Admin CLI response contracts.
4. [Completed] Align the web integration workspace and generated contracts.
5. [Completed] Update the Ingestion personal-data catalogue and deterministic inventory.
6. [Completed] Run focused checks, then one coherent non-Docker domain gate.

## Deferred

- Connection health and retention metrics keep exact counts because those values
  are the product semantics, not pagination bookkeeping.
- Legal holds remain a separate Admin-only governance surface. Their low-volume
  exact directory and full audit rows are not changed by this operator slice.
- Reprocessing output details remain an explicit bounded child collection under
  one attempt; parser protocol limits cap their cardinality.
- Cursor pagination is unnecessary until measured offset depth demonstrates a
  real bottleneck. The contract can evolve independently inside Ingestion.
- No GMA change is required; the framework already supplies the generic page
  normalization needed by this slice.

## Verification Cadence

Use focused Ingestion contract, persistence, handler, API, CLI, and web checks
while editing. At the coherent slice boundary, run the full Ingestion suite,
architecture guards, host-composition build, web lint/tests/typecheck/build,
generated-contract drift check, and personal-data catalogue guard once. No
Docker scenario is required because this slice changes no persistence schema,
database-provider behavior, or broker contract.

## Completion Note

Implemented minimized list items for connections, credentials, proposals,
runs, observation receipts, and reprocessing attempts. Their repositories now
project directly, read one additional row to derive `HasMore`, and avoid exact
count queries. Connection management, credential revocation, and proposal
decisions return contract-owned mutation receipts; one-time credential issuance
continues to return the token-bearing response.

Authenticated API and Admin API operational routes now declare their success
schemas and prevent shared caching. Admin CLI and the web integration workspace
consume the same contracts, invalidate authoritative detail queries after
writes, and use look-ahead pagination. Personal-data catalogue version 11 and
its generated inventory classify the bounded operator responses and one-time
credential boundary.

Verification completed on 2026-08-04:

- Ingestion tests: 277 passed;
- architecture guards: 83 passed;
- host/OpenAPI composition build: succeeded with zero warnings and errors;
- generated OpenAPI and TypeScript contract drift check: passed;
- web lint: passed;
- web tests: 138 passed across 21 files;
- web production build and TypeScript check: passed;
- Docker: intentionally not run because the slice changed no schema, database
  provider behavior, or broker contract.
