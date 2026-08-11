# Staff Auth-Subject Idempotency Task

Status: completed
Date: 2026-08-07

## Goal

Make Staff account-link changes safe to retry after an uncertain response. One
logical link or lifecycle-safe unlink must produce at most one Staff version
and one integration event, while reuse of its operation id for different input
fails closed. Direct replacement is now rejected by the later
[transition-safety contract](staff-auth-subject-transition-safety-task.md).

## Boundary Decision

- Staff owns account-link normalization, uniqueness, request equivalence,
  immutable operation receipts, privacy lifecycle, tenant portability, and
  replay results.
- Replace the profile-only operation store with one Staff-internal member
  mutation journal. Profile updates and account-link changes have the same
  tenant/member/operation coordinate, expected and resulting version,
  directory-safe result, fingerprint, immutability, and cleanup semantics.
- Operation kind remains explicit in each record and in request equivalence.
  Future lifecycle or assignment commands may join this journal only when
  their completed-result and privacy semantics are proven equivalent.
- GMA continues to own transactional command dispatch, transaction-scoped
  locking, persistence retry classification, and outbox delivery. Staff
  operation kinds, account subjects, fingerprints, and response receipts do
  not belong in GMA.

## Operation Contract

1. Every public API, Admin API, and Admin CLI attempt supplies one non-empty
   operation id for one logical account-link change.
2. Normalize and validate the requested Auth subject and actor before locking
   or consulting a receipt. HTTP actor identity remains server-resolved and is
   never accepted from request content.
3. Acquire and reload the operational Staff member before reading the journal.
   Restricted, anonymised, or otherwise hidden profiles remain
   non-disclosing.
4. Fingerprint the operation kind, Staff member id, expected version, and
   normalized requested Auth subject. The actor is revalidated on every
   attempt but is not request equivalence and replay cannot rewrite original
   attribution.
5. Under the member lock, return the original receipt for an exact retry.
   Reusing an operation id for another kind, target, version, or normalized
   value returns HTTP 409.
6. A new operation always enforces the supplied expected version, including a
   no-change request. A distinct stale operation cannot succeed because the
   current link happens to match its requested value.
7. A valid no-change operation records one receipt without advancing the Staff
   version or emitting an event. A real change and its receipt commit in the
   same Staff transaction and outbox boundary.
8. Auth-subject uniqueness remains tenant scoped. Failed validation,
   authorization, visibility, uniqueness, or version checks record nothing.

## Persistence And Privacy

- Rename the existing profile-update operation entity and table to a Staff
  member-mutation operation and add an explicit operation kind. Migrate current
  profile receipts as `ProfileUpdate` without losing their ids or timestamps.
- Keep the composite tenant/Staff/operation key and Staff foreign key. One
  operation id cannot silently cross mutation kinds for the same Staff member.
- Store only a canonical SHA-256 request fingerprint, expected/result versions,
  result status, operation kind, and completion time. Do not persist the Auth
  subject a second time in the journal.
- Treat the fingerprint as pseudonymous personal data. Keep it in bounded
  subject export and tenant export, and remove it during Staff anonymisation,
  retention anonymisation, or authorized tenant destruction.
- Preserve append-only enforcement. Operational deletion is allowed only
  through the existing Staff-owned privacy and tenant lifecycle paths.

## Response And Surfaces

- Use a shared `StaffMemberMutationReceiptDto` containing Staff member id,
  status, resulting version, and completion time for profile and account-link
  mutations. The wire shape stays small and contains no Auth subject.
- Require `--operation-id` for Admin CLI account-link changes.
- The Staff UI retains an operation id while the same normalized Auth subject
  and expected version are retried, and clears it after success, cancellation,
  target change, or changed input. It refetches the current member after the
  receipt is accepted.
- The later transition-safety slice constrains which new operations are valid;
  exact receipts created under this contract retain their replay semantics.
- The later manual-create authority slice removes Auth-subject correlation from
  management creation, leaving this dedicated mutation as the only operator
  account-link path.

## Verification

- Focused domain/application tests cover normalization, exact replay,
  conflicting reuse across payload and operation kind, no-change receipts,
  stale versions, actor validation before replay, hidden profiles, uniqueness,
  and one event/receipt.
- Persistence and PostgreSQL tests cover the table migration, constraints,
  append-only enforcement, concurrent identical changes, atomic rollback,
  tenant isolation, exact replay, and privacy/tenant cleanup and export.
- API, Admin API, Admin CLI, OpenAPI, generated TypeScript, and Staff UI tests
  cover required and stable operation ids.
- Use focused non-Docker checks while editing. Run one consolidated backend,
  web, contract, migration, and targeted PostgreSQL gate at the completed slice
  boundary.

## Deferred Follow-Up

- Give employment lifecycle and property-assignment commands durable operation
  identities in later Staff slices after their policy and result semantics are
  handled independently.
- Do not generalize the Staff journal into GMA unless another module proves the
  same storage, replay, privacy, and lifecycle contract.

## Completion Evidence

- Solution synchronization, source-package checks, the warning-free backend
  build, and every provider migration-drift check passed. The documentation
  index guard found the new task during the first pass; after adding its link,
  all 94 architecture tests passed.
- All 213 Staff tests passed. The remaining host and non-Docker integration
  suites passed 25 migration-host tests, 63 service-default tests, and 54
  integration tests.
- The targeted PostgreSQL scenario passed 1/1 and proved legacy receipt
  preservation, concurrent exact replay for both operation kinds, immutable
  storage, atomic outbox rollback, tenant isolation, and lifecycle cleanup.
- Generated OpenAPI and TypeScript contracts are current. Web type checking,
  lint, all 193 tests across 33 files, and the production build passed.
- GMA source remained unchanged; the journal, operation kinds, and Auth-subject
  semantics remain owned by the Staff module.
