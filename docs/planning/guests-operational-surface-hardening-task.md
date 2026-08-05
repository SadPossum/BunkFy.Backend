# Guests Operational Surface Hardening Task

Status: complete
Date: 2026-08-05

## Goal

Minimize ordinary Guest mutation responses and prevent Guest HTTP responses
from being stored by browser or intermediary caches, while preserving the
existing property-scoped directory, sensitive detail, and bounded stay-history
use cases.

## Ownership

- Guests owns Guest response shapes, lifecycle receipts, visibility,
  permissions, and personal-data classification.
- GMA pagination continues to own normalized page and page-size limits.
- Public API, Admin API, Admin CLI, and web consumers use the same application
  mutation contract.
- Cache policy remains an HTTP front-door concern and does not enter the domain
  or persistence layers.
- No GMA change is required for this BunkFy Guest-record policy.

## Audit Findings

- Directory and stay-history reads already use bounded one-row look-ahead
  pagination and direct persistence projections.
- Create, update, and archive commands return the complete sensitive profile
  even though their consumers need only the affected Guest coordinate and
  lifecycle version.
- Public and Admin mutation endpoints do not declare success response schemas.
- Guest API groups do not currently apply the `no-store` policy used by other
  sensitive BunkFy modules.
- Mirrored Admin API profile inputs are not bound into the executable
  personal-data catalogue even though the corresponding public inputs are.
- The web invalidates and rereads the selected profile after a mutation, so it
  does not rely on profile PII in the write response.

## Invariants

- Create, update, and archive return one PII-minimal receipt containing only
  `GuestId`, `Status`, `Version`, and `LastChangedAtUtc`.
- The sensitive profile remains available only through the explicit detail
  read guarded by `guests.read` at the property scope.
- Directory and stay-history contracts, visibility rules, restriction gates,
  country-policy admission, optimistic concurrency, and outbox behavior do not
  change.
- Every public and Admin Guest response carries `Cache-Control: no-store`,
  `Pragma: no-cache`, and an expired response timestamp, including failures.
- OpenAPI metadata identifies every ordinary mutation success contract.
- New response bindings remain covered by the executable personal-data
  catalogue and deterministic inventory.

## Delivery

1. [Completed] Add the bounded Guest mutation receipt and return it from the
   three ordinary lifecycle commands.
2. [Completed] Align public API, Admin API, Admin CLI, web consumers, and affected
   integration tests.
3. [Completed] Apply and test sensitive-response cache headers at both HTTP front
   doors.
4. [Completed] Advance the Guests personal-data catalogue and regenerate its
   deterministic inventory.
5. [Completed] Run focused checks, then one coherent non-Docker domain gate.

## Outcome

- Ordinary Guest lifecycle writes now return a PII-minimal mutation receipt.
- Public and Admin Guest responses use the same explicit sensitive-response
  cache policy and mutation schemas.
- Admin request and mutation-receipt bindings are covered by personal-data
  catalogue version 12 and its deterministic inventory.
- Admin CLI and web mutation consumers use the receipt without retaining or
  exposing the full Guest profile returned by the previous contract.

## Verification Evidence

- Guests tests: 139 passed.
- Architecture guards: 83 passed.
- Non-Docker integration tests: 54 passed.
- Solution build: succeeded with 0 warnings and 0 errors.
- Generated web contract drift check: current.
- Web verification: typecheck, lint, 150 tests, and production build passed.
- Docker was intentionally omitted because the slice changed no schema,
  transaction, broker, or provider behavior.

## Deferred

- The directory keeps the fields currently required by the Guest operations
  screen and reservation Guest picker. A second picker-specific contract needs
  measured exposure or permission pressure before adding another surface.
- Full profile reads still hydrate the flat aggregate. A separate projected
  detail reader requires query evidence before introducing another repository
  path.
- Substring search and offset pagination stay aligned with the current product
  scale. PostgreSQL-specific indexing or keyset pagination requires measured
  query and UX evidence.
- Identity documents, consent, duplicate merge/split, entity resolution,
  preferences, and Guest accounts remain separate future domains.

## Verification Cadence

Use focused Guests tests and web type checking while editing. At the coherent
slice boundary, run the complete Guests suite, architecture guards, affected
integration tests, generated-contract drift, and the full web gate once. No
Docker scenario is required because the slice changes no schema, transaction,
broker, or provider behavior.
