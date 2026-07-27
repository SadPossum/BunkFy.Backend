# Data Rights Correction Execution Task

Status: verified; publication pending

## Outcome

Complete the approved correction path for one selected Guest or Reservation
record. An authorized operator supplies corrected values directly to the
owning module, the owner applies them through its normal domain command, and
the Data Rights case completes only after a matching PII-free owner receipt is
delivered through the durable outbox.

Correction is deliberately a single-operation, single-record workflow. It
does not combine correction with export, restriction, or anonymisation, and it
does not infer related records that the operator did not explicitly select.

## Ownership

- Data Rights owns the approved case, exact selected coordinate, one PII-free
  correction execution claim, field-policy key, actor, expiry, owner-proof
  validation, and terminal case state.
- Guests owns Guest profile correction values, validation, aggregate mutation,
  current-version checks, receipt, and profile projection.
- Reservations owns booking-guest correction values, validation, aggregate
  mutation, details-revision checks, immutable history, and receipt.
- Data Rights Contracts owns the product-level claim gate and generic PII-free
  completion-event protocol used by correction owners.
- No owner-specific value, patch document, name, contact detail, note, date of
  birth, nationality, or booking value is stored by Data Rights or published
  through messaging.
- GMA is unchanged. Its CQRS, scoped unit-of-work, outbox, messaging,
  assurance, and access-control primitives already cover the generic
  mechanics.

## Execution Model

1. The approved case must request exactly `Correction` and contain exactly one
   selected subject.
2. Under `data-rights.execute`, property scope, and recent privileged
   authentication, Data Rights creates one correction execution claim.
3. The claim records a caller idempotency key, approval revision, exact subject
   coordinate, bounded owner field-policy key, executing actor, start time, and
   short expiry. It moves the case from `Approved` to `Executing`.
4. Equivalent claim retries return the existing claim. A changed idempotency
   key, actor, policy key, coordinate, approval revision, or case version fails
   closed.
5. The operator submits corrected values to the selected owner endpoint. The
   owner asks the Data Rights claim gate to revalidate tenant, property, case,
   approval, subject, execution id, actor, policy key, and expiry before
   mutation.
6. The owner uses the correction execution id as its idempotency key, applies
   the change and stores its receipt in one owner-local transaction.
7. The owner outbox publishes a generic, PII-free completion proof containing
   only coordinates, revisions, field-policy metadata, receipt identity,
   changed-field count/digest, and completion time.
8. Data Rights consumes the proof, validates it against the persisted claim,
   stores a bounded proof, and marks the case `Completed`.
9. A crash or delayed message after owner commit leaves the case visibly
   `Executing`; outbox replay converges without repeating the mutation.

## Security And Concurrency

- A correction cannot run directly from an `Approved` case. The persisted
  claim is the one-time bridge between fresh operator assurance and owner
  mutation.
- The claim is actor-bound and expires. An owner endpoint rejects an absent,
  expired, mismatched, completed, or superseded claim.
- `data-rights.execute` does not silently grant access to owner PII. The
  operator form reads current values through the selected owner's ordinary
  read endpoint, so a custom correction role also needs `guests.read` or
  `reservations.read` for the record type it is allowed to correct.
- One case cannot acquire two correction claims. One claim cannot correct two
  records or be replayed with another payload.
- Owner receipts remain unique by tenant and execution id. Equivalent retries
  return the committed receipt; a changed payload or stale owner version is a
  conflict.
- The Data Rights completion consumer is idempotent under duplicate and
  out-of-order delivery. A proof that differs from the committed proof fails
  closed.
- Correction preserves immutable operational history. Owners append their
  normal change events and never rewrite prior audit facts.
- Logs, metrics, URLs, notifications, tasks, outbox headers, and error messages
  contain no corrected values.

## Efficiency

- Starting a correction performs one indexed Data Rights case read and one
  tracked case update.
- Owner execution performs one indexed claim read, one indexed owner read, and
  one bounded owner transaction.
- Completion performs one indexed case read and one tracked proof update.
- There are no broad scans, cross-module database reads, distributed
  transactions, polling workers, or unbounded collections. The UI polls only
  the one non-terminal case while the durable outbox converges.

## Delivery Slices

1. [x] Add the PII-free claim/proof model, aggregate transitions, persistence,
   approval gate, API, and focused Data Rights tests.
2. [x] Add the generic completion event and idempotent Data Rights consumer.
3. [x] Bind Guests and Reservations owner commands to the claim, emit durable
   proofs, and expose the missing Reservations correction endpoint.
4. [x] Add the owner-specific operator forms and terminal refresh behavior.
5. [x] Run the complete non-Docker backend and web gates once after the slice
   is functionally complete.
6. [x] Run the complete Docker gate once. Batch any failures and repeat only
   the failed full gate after the fixes.
7. [ ] Publish the exact candidate in dependency order and use GitHub Actions
   once to prove those published commits.

## Verification

- Backend non-Docker verification passed, including build, migration drift,
  module tests, and architecture guards. The final test-only handoff fixes
  also passed focused contract tests and a zero-warning integration build.
- Web `pnpm verify` and generated-contract drift checks passed.
- PostgreSQL/NATS/Worker Docker verification passed: 63 tests, 0 failures.

## Acceptance

- Domain tests cover claim creation, equivalent replay, conflicting replay,
  expiry, completion, and proof conflict.
- Application tests cover approval/coordinate revalidation, unique claim
  ownership, owner unavailability, stale versions, and delayed completion.
- Guest and Reservation tests cover changed payload replay conflicts, exact
  case/actor/policy binding, owner receipt persistence, and PII-free events.
- API tests prove permission, property scope, recent assurance, bounded
  responses, and no owner values in Data Rights DTOs.
- Persistence tests prove the claim and completion proof survive reload and
  that the migration rejects impossible state combinations.
- One real PostgreSQL/NATS/Worker scenario proves claim, owner commit, outbox
  delivery, terminal completion, exact replay, and no duplicate owner change.
- Frontend tests cover Guest and Reservation forms, conflict recovery,
  `Executing` progress, completion refresh, and error redaction.
- Personal-data catalogue, architecture, generated-contract, and migration
  guards remain green.

## Non-Goals

- Bulk correction or multi-owner correction in one case.
- Generic JSON Patch, JSON Merge Patch, or storing proposed values in Data
  Rights.
- Guest self-service correction.
- Correction of Ingestion source evidence or immutable provider payloads.
- Automatic correction of every record related to a selected Guest.
- A generic GMA correction-workflow abstraction.
