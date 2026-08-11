# Staff Profile Update Idempotency Task

Status: completed
Date: 2026-08-07

## Goal

Make manager and identity-bound self-service Staff profile updates safe to
retry after an uncertain response. One logical update must produce at most one
Staff version and event, while reuse of its operation id for different input
fails closed.

## Boundary Decision

- Staff owns profile-update equivalence, immutable operation receipts,
  persistence, privacy lifecycle, tenant portability, and replay results.
- GMA continues to own transactional command dispatch, transaction-scoped
  locking, persistence exception classification, and outbox delivery. Staff
  operation kinds, fingerprints, response receipts, and lifecycle rules do not
  belong in GMA.
- Public API, Admin API, Admin CLI, account UI, management UI, and workspace
  onboarding supply one non-empty operation id for one logical update.
- HTTP actor identity remains server-resolved. Actor validation and current
  authorization run on every attempt, but actor identity is not request
  equivalence and replay never rewrites original attribution.

## Operation Contract

1. An operation id is scoped to one tenant and Staff member. The existing
   Staff member mutation coordinate serializes first execution and replay.
2. Input is normalized and validated before the member lock or replay lookup.
   Each update surface fingerprints the Staff member id, expected version, and
   all normalized values writable through that surface, but never stores the
   profile payload. The later
   [self-service ownership slice](staff-self-service-profile-ownership-task.md)
   separated its narrower fingerprint from management updates.
3. Under the member lock, an equivalent receipt returns the original status,
   version, and completion timestamp. Reusing the operation id with another
   target, expected version, or normalized profile returns HTTP 409.
4. A new operation still enforces the supplied expected version. A distinct
   stale operation cannot become successful merely because its desired values
   now match current state.
5. A valid update whose requested profile already equals current state records
   one no-change receipt without advancing the Staff version or emitting a
   domain event. The receipt prevents later reuse of that operation id.
6. The first successful profile mutation and its operation receipt commit in
   the same Staff transaction and outbox boundary. Failed validation,
   authorization, visibility, uniqueness, or version checks record nothing.
7. Processing-restricted or anonymised profiles remain non-disclosing. A
   receipt is not consulted until the currently authorized operational profile
   has been locked and reloaded.
8. Self-service first resolves the current Auth subject, then locks and reloads
   the Staff member and rechecks the subject correlation before reading a
   receipt or mutating profile data.

## Response And Surfaces

- Add `OperationId` to both profile-update commands and the public and
  administrative request contracts. Require `--operation-id` for Admin CLI
  update. The self-service request later became a dedicated allowlisted
  contract while management retained the full profile request.
- Return a small mutation receipt containing Staff member id, status, resulting
  version, and completion time. Its current shared name is
  `StaffMemberMutationReceiptDto`; do not duplicate profile PII in an immutable
  receipt merely to recreate a historical response body.
- Manager and account UIs retain an operation id while the same normalized form
  and expected version are retried, clear it after success/cancel, and refetch
  the current representation after receiving the mutation receipt.
- Workspace onboarding keeps its operation id and original expected version in
  session storage until profile completion succeeds or the draft is abandoned.
  This allows a retry after the update committed but its response was lost.

## Persistence And Privacy

- Add a Staff-owned append-only profile-update operation record with operation
  id, tenant and Staff coordinates, expected/result versions, result status,
  canonical SHA-256 request fingerprint, and microsecond-normalized completion
  time. The auth-subject slice later generalized its current storage name to the
  Staff member-mutation journal without changing these semantics.
- Use a tenant/Staff/operation composite key, a Staff foreign key, provider-
  agnostic domain/application contracts, and a project PostgreSQL migration.
- Treat the request fingerprint as pseudonymous personal data. Catalogue it,
  include operation records in bounded subject export and tenant export, and
  delete them during either Data Rights or retention anonymisation.
- Add a tenant-destruction cleanup stage without changing persisted terminal
  stage value `22`. The new stage uses a new numeric value and an explicit
  non-linear transition so already-completed operations remain completed.
- Operation records remain immutable and are removed only by Staff-owned
  anonymisation or authorized tenant destruction.

## Verification

- Focused domain/application tests cover normalized replay, conflicting reuse,
  no-change success, stale versions, actor validation before replay, hidden
  profiles, self-service subject recheck, and one event/receipt.
- Persistence and PostgreSQL tests cover model constraints, append-only
  enforcement, concurrent identical updates, atomic rollback, tenant
  isolation, exact receipt replay, and lifecycle cleanup/export.
- API, Admin API, Admin CLI, OpenAPI, generated TypeScript, management UI,
  account UI, and onboarding tests cover required and stable operation ids.
- Use focused non-Docker checks during implementation. At the completed slice
  boundary, run one consolidated backend/web/contract gate and only the
  targeted PostgreSQL scenarios needed by this schema and concurrency change.

## Deferred Follow-Up

- Auth-subject changes were completed in
  `staff-auth-subject-idempotency-task.md`. Employment lifecycle transitions and
  property assignments remain later Staff slices.
- Reconsider a generic GMA operation-journal abstraction only after another
  module proves identical storage, replay, privacy, and lifecycle semantics.

## Completion Evidence

- The complete non-Docker backend gate passed, including 208 Staff tests,
  94 architecture tests, and 54 integration tests.
- The targeted PostgreSQL concurrency, replay, immutability, and rollback
  scenario passed.
- Migration drift, solution synchronization, generated OpenAPI contracts,
  web typecheck, lint, 190 web tests, and the production build passed.
