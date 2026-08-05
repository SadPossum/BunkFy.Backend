# Properties Operational Surface Hardening Task

Status: implemented
Date: 2026-08-04

## Goal

Make Properties' topology management surfaces bounded, cache-safe, atomic for the existing multi-bed workflow, and inexpensive to consume without moving inventory, reservation, or access policy into the module.

## Ownership

- Properties owns physical property, room, and bed topology, facility labels, time zones, and property processing lifecycle.
- Inventory continues to own sellability and the reservation-safe room and bed retirement process.
- AccessControl continues to own grants and scope evaluation; Properties only resolves visible property identifiers.
- GMA Pagination continues to own page normalization. Response shapes, topology batch limits, and write semantics are BunkFy Properties concerns, so no GMA change is required.

## Findings

- Property, room, and bed directories stop at a bounded page but do not expose continuation metadata; web callers currently assume the first 100 rows are complete.
- Directory rows reuse full detail DTOs. Property lists can carry governance detail that the workspace picker and topology screen do not use.
- Create and update commands return full detail models even though callers invalidate and refetch authoritative reads.
- The existing multi-bed form performs one request per bed. A later failure leaves an intentional but awkward partial result and creates avoidable request, transaction, and event-dispatch overhead.
- Public and Admin API responses do not consistently prevent shared caching, and Admin endpoints do not declare explicit success response metadata.
- The topology projection rebuild contract pages property roots but embeds every room and bed for each selected property. Fixing that requires a versioned cross-consumer protocol rather than silently changing this operator surface.

## Decisions

- Property, room, and bed directories use minimized list-item contracts and deterministic `pageSize + 1` lookahead with `HasMore`; no exact count query is introduced.
- Full property and room details remain available through explicit detail endpoints.
- Ordinary property, room, and bed create/update operations return minimal contract-owned mutation receipts. Retirement and processing suspension remain no-content lifecycle commands; processing activation returns a property receipt and callers refetch processing state.
- Add a bounded, atomic `AddBeds` command and public/Admin endpoint. One room aggregate validates the complete label set before mutation, emits one existing bed-added event per bed, and commits all or none.
- A batch contains at most 100 labels. The existing single-bed command remains for compatibility and uses the same aggregate rules.
- Public and Admin Properties routes emit `Cache-Control: no-store`, `Pragma: no-cache`, and `Expires: 0`. Admin routes declare their 200 or 204 success responses explicitly.
- Admin CLI mutation commands emit structured receipts; add a bounded multi-bed command for remote operator use.
- Web callers follow `HasMore`, use the atomic batch endpoint, and refetch authoritative topology after writes.

## Delivery

- [x] Add list-item DTOs, `HasMore`, direct projections, stable ordering, and focused repository tests.
- [x] Add minimal property, room, bed, and bed-batch mutation receipts and align command handlers.
- [x] Add aggregate-level atomic bed batching with explicit limits and failure tests.
- [x] Align public API, Admin API, Admin CLI, endpoint metadata, and cache controls.
- [x] Regenerate OpenAPI and align workspace, topology, dashboard, and related web consumers.
- [x] Add focused contract, API-security, application, and frontend regression coverage.
- [x] Update Properties development notes and run the coherent slice gates once.

## Deferred

- A versioned flattened or resumable topology rebuild protocol. The current root snapshot embeds rooms and beds, and changing it safely requires coordinated consumer replacement semantics across Inventory, Reservations, Staff, Guests, Ingestion, Workspaces, and Data Rights.
- Cursor pagination until measured offset depth justifies a contract change.
- Server-side topology search and virtualized room or bed management for unusually large properties.
- Dashboard-specific aggregate counts. Loading operational directories to calculate cross-domain dashboard metrics should eventually be replaced by a dedicated read model, but that is not Properties ownership.

## Completion Criteria

- all three topology directories report continuation truthfully without exact count queries or full-detail list rows;
- a multi-bed request is explicitly bounded and cannot partially commit;
- ordinary topology writes return bounded receipts and clients refetch authoritative detail;
- public and Admin responses are no-store and publish correct success metadata;
- web callers can traverse more than 100 properties, rooms, or beds without a redundant terminal probe;
- existing access scope, lifecycle, event, retirement, projection, and tenant-termination behavior remains unchanged;
- focused Properties, architecture, composition, generated-contract, and web checks pass.

## Verification Cadence

Use focused contract, aggregate, repository, API, CLI, and web checks while editing. At the coherent slice boundary, run the full non-Docker backend gate and web lint/tests/typecheck/build/contracts check once. No Docker scenario is required unless implementation changes persistence schema or provider-specific behavior.

## Completion Note

Implemented minimized, stably ordered property, room, and bed directories with
truthful `HasMore` lookahead and no exact-count query. Ordinary writes now
return bounded receipts, and multi-bed creation is one aggregate-level atomic
command with a 100-label contract limit. Public and Admin APIs prevent shared
caching, Admin success metadata is explicit, and the Admin CLI exposes
structured mutation receipts plus `beds add-many`.

Generated OpenAPI and TypeScript contracts are aligned. Workspace, topology,
and dashboard consumers traverse `HasMore` without a redundant terminal probe,
use the atomic bed-batch route, and refetch authoritative reads after writes.
The obsolete unserved full-bed DTO was removed while the list-item contract was
kept explicit.

Verification completed on 2026-08-04:

- backend solution build, including cross-domain and integration projects: passed with zero warnings and errors;
- migration drift checks for every configured GMA and BunkFy provider: passed;
- Properties tests: 92 passed;
- architecture guards: 83 passed;
- non-Docker integration tests: 54 passed;
- web tests: 142 passed across 23 files;
- web lint, TypeScript check, and production build: passed;
- generated OpenAPI and TypeScript contract drift check: passed;
- Docker: intentionally not run because the slice changed no schema, provider-specific behavior, or broker contract.
