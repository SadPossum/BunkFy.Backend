# Data Rights Restriction Execution Task

Status: implemented and verified locally; publication pending

## Outcome

Complete one controlled Data Rights operation from approved case to durable
owner result. An authorized operator can apply or release processing restriction
for one selected Guest profile, and the Data Rights case reaches a terminal
state only after Guests commits the authoritative restriction receipt.

This slice does not expose correction. Correction values remain personal data
owned by Guests and must be bound to an approved owner-local plan before an
operator workflow is safe to publish.

## Ownership

- Data Rights owns case lifecycle, exact selected coordinates, approval
  revision, execution idempotency, contributor resolution, terminal state, and
  a PII-free copy of the owner proof.
- Guests owns the restriction aggregate, effective projection, apply/release
  transaction, country-policy admission, receipt, and propagation to dependent
  Reservations projections.
- Data Rights Contracts owns only the product-level contributor request/result
  used across those modules.
- GMA is unchanged. The existing CQRS, assurance, scoping, persistence, and
  result primitives already cover the generic mechanics.

## Invariants

- Only an exact `Restriction` case is executable in this slice.
- Exactly one selected coordinate is required and it must resolve to one unique
  contributor.
- The approved restriction directive, case, approval revision, tenant,
  property, record id, and record version are revalidated immediately before
  owner mutation.
- Apply creates an independent owner restriction even when another case is
  already active. Effective restriction is the owner projection's composed
  state across those obligations.
- Release is rejected unless exactly one active restriction exists. Selecting
  one owner restriction among several requires a future owner-local release
  plan; Data Rights does not guess or persist owner-specific targeting state.
- The Data Rights case stores only bounded identifiers, revisions, booleans,
  actor attribution, timestamps, and a canonical SHA-256 owner-proof digest.
- Equivalent retries return the committed proof. Changed idempotency,
  coordinates, directive, actor, or owner proof fail closed.
- A crash after Guests commits but before Data Rights completes converges by
  replaying the same owner idempotency key and proof.

## Product Flow

1. Create an apply- or release-restriction privacy case.
2. Verify and route the requester, discover the Guest profile, and select that
   exact record.
3. Review and approve the case through the existing decision workflow.
4. A separately permissioned operator executes the restriction with recent
   privileged authentication.
5. Data Rights calls the unique owner contributor. Guests applies or releases
   the restriction and returns its durable receipt.
6. Data Rights validates and stores the PII-free proof, then marks the case
   complete.
7. The UI refreshes the case, guest, reservation, and restriction views.

## Delivery Slices

1. [x] Add the contributor contract, coordinator proof value object, aggregate
   transition, persistence mapping, and focused domain/application tests.
2. [x] Add the Guests contributor, composed-active invariant, migration, and
   owner replay tests.
3. [x] Add the permission- and assurance-protected API plus the owner-facing
   apply/release flow and focused frontend tests.
4. [x] Run the complete non-Docker gate once, then the complete Docker gate
   once. Batch any failures and repeat only the failed gate.
5. [ ] Publish in dependency order and verify exact-candidate GitHub Actions
   once.

## Verification

- Backend `eng/verify.ps1 -SkipRestore`: passed after adding this task to the
  guarded documentation index; zero-warning build, migration drift, and all
  non-Docker tests are green.
- Web `pnpm verify`: 18 test files and 120 tests passed; type checking, lint,
  and the production build are green.
- Web `pnpm contracts:check`: OpenAPI snapshot and generated TypeScript
  contracts are current.
- Backend `eng/test-docker.ps1 -NoBuild`: 63 Docker integration tests passed in
  the single end-of-slice run, including Data Rights-to-Guests restriction
  apply, replay, release, durable proof, and owner-state verification.

## Security And Efficiency

- Execution is one indexed Data Rights case read, one indexed owner-state read,
  one bounded owner transaction, and one tracked Data Rights update.
- No broad owner scan, cross-module database access, unbounded parallelism, or
  raw personal value crosses the contributor boundary.
- The endpoint requires `data-rights.restrict`, property scope, and the hosted
  privileged-operation assurance policy.
- Contributor outcome codes are stable and bounded. Logs, metrics,
  notifications, task rows, URLs, and errors do not carry Guest values or
  unrestricted reasons.

## Acceptance

- Domain tests cover exact operation/subject/directive rules, completion,
  equivalent replay, and conflicting replay.
- Contributor tests cover apply, release, no active state, duplicate active
  state, approval denial, stale record version, and proof tampering.
- Persistence tests prove durable owner proof, composed restriction state, and
  historical upgrade behavior.
- API/OpenAPI tests prove permission, scope, recent assurance, and bounded
  response shape.
- Frontend tests prove apply/release creation, approved execution, terminal
  refresh, and no accidental erasure wording.
- Architecture and personal-data catalogue guards remain green.

## Non-Goals

- Correction execution or storing proposed corrected values in Data Rights.
- Multi-record restriction cases.
- Combining restriction with export, correction, erasure, or anonymisation in
  one case.
- Legal approval of a restriction outcome.
- A generic GMA workflow abstraction.
