# Staff Management Create Idempotency And Lock Ordering Task

Status: complete
Date: 2026-08-07

## Goal

Make Staff member creation safe to retry after an uncertain response, while
correcting Staff operational lock ordering so profile mutations cannot deadlock
with workspace termination.

## Ownership

- Staff owns creation equivalence, Staff member identity, profile visibility,
  mutation serialization, and replay results.
- The public API, Admin API, Admin CLI, and web client supply one non-empty
  operation id for one logical create attempt.
- The operation id is also the canonical Staff member id. No request journal,
  duplicate profile payload, or personal-data fingerprint is persisted.
- Staff Persistence owns tenant-admission and Staff-coordinate lock ordering.
- GMA continues to own the transactional command pipeline, transaction-scoped
  key-lock primitive, database exception classification, and outbox delivery.
  Staff request semantics and resource names do not belong in GMA.

## Lock Order

Every relational Staff profile mutation follows one order:

1. Acquire shared tenant mutation admission and verify the termination fence.
2. Acquire the creation-operation or existing-member coordinate.
3. Acquire the tenant revision-advance coordinate when saving an operational
   mutation.
4. Persist the aggregate, projections, lock state, and outbox in the command
   transaction.

Workspace termination may acquire the tenant coordinate exclusively, but must
never observe a Staff mutation holding a narrower coordinate while waiting for
tenant admission.

## Creation Invariants

1. The operation id is the Staff member id and its tenant-scoped serialization
   coordinate.
2. Tenant scope, authorization, ordinary write admission, and actor validation
   are evaluated on every attempt before replay can succeed.
3. Creation acquires tenant admission and the operation coordinate before
   reading by Staff member id or deciding that the profile is absent.
4. An equivalent retry compares the aggregate's current normalized profile and
   returns the current directory-safe receipt without persisting another member
   or repeating creation events.
5. Reusing an operation id for a different normalized profile returns a stable
   conflict.
6. Actor identity is authenticated and validated on every attempt, but is not
   part of request equivalence and replay never rewrites original attribution.
7. A processing-restricted or anonymized coordinate remains non-disclosing and
   cannot be reused to create a second profile.
8. Existing employee-number and authentication-subject uniqueness rules remain
   tenant scoped. Concurrent conflicting creates converge to their existing
   semantic conflict rather than leaking a persistence exception.

## Surfaces

- Add `OperationId` to the Staff management create command and both HTTP request
  contracts.
- Require `--operation-id` in the Admin CLI so remote automation can retry
  safely.
- Keep one operation id for an unchanged web create-form attempt; allocate a
  new id when the normalized profile changes or the attempt succeeds or is
  abandoned.
- Map operation-reuse conflicts to HTTP 409 on public and administrative HTTP
  surfaces.
- Regenerate OpenAPI and web contracts after the backend contract settles.

## Persistence And Governance

- Use the existing GMA transaction-scoped key lock for the pre-aggregate Staff
  creation coordinate; no schema migration is expected.
- Admit the tenant before the existing persisted member lock is touched.
- The operation id is the canonical Staff member identifier, not a new
  personal-data category or retained idempotency secret.
- Update the executable personal-data catalogue for newly exposed command and
  API fields, then regenerate its checked inventory.

## Verification

- Focused domain and application tests cover exact and normalized replay,
  changed-payload conflict, hidden-coordinate handling, current receipt replay,
  authorization before replay, and admission-before-coordinate ordering.
- Persistence coverage proves transaction and tenant-scope requirements and
  one durable winner for concurrent attempts on the same operation id.
- API, Admin API, Admin CLI, OpenAPI, generated web contracts, and web attempt
  tests cover the required operation id on every management surface.
- Use focused non-Docker checks while editing. At the coherent slice boundary,
  run one consolidated Staff, architecture, composition, contract, and web gate,
  followed by one targeted PostgreSQL concurrency scenario.

## Completion Evidence

- `eng/verify.ps1 -SkipRestore` passed solution synchronization,
  source-package checks, a zero-warning build, migration-drift checks, and every
  non-Docker test suite. This included 198 Staff tests and 94 architecture
  guards.
- `pnpm verify` passed type checking, lint, all 186 web tests, and the production
  build.
- `pnpm contracts:check` confirmed that the OpenAPI snapshot and generated
  TypeScript contracts are current.
- The focused PostgreSQL Staff export scenario passed 1/1 and proved that
  concurrent acquisition of one creation operation serializes to one durable
  Staff member, restriction projection, and operation-lock row.

## Deferred Follow-Up

- Profile updates and authentication-subject changes are completed in
  `staff-profile-update-idempotency-task.md` and
  `staff-auth-subject-idempotency-task.md`.
- Give employment lifecycle and property-assignment commands durable operation
  identities and replay receipts in later Staff slices.
- Do not generalize Staff-specific equivalence, visibility, or operation
  receipts into GMA.
