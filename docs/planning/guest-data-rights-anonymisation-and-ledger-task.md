# Guest Data Rights Anonymisation And Ledger Task

Status: in progress; approval-evidence foundation complete, execution closed

## Outcome

Execute an approved Guest Record anonymisation irreversibly, prove the owner
mutation with an immutable receipt, record the result in the DataRights
authoritative processing ledger, and prevent a database restore from making the
removed identity readable again.

This task implements anonymisation only. It does not call archive erasure, does
not hard-delete historical reservation facts, and does not let DataRights write
the Guests schema.

The capability is incomplete until:

- stale approval, Guest, policy, hold or retention evidence fails closed;
- an equivalent retry returns the same owner result and ledger entry;
- a conflicting retry cannot mutate or overwrite prior proof;
- the externally protected ledger delta exists before the case is completed;
- restoring a database from before anonymisation reapplies the owner tombstone
  before API or Worker readiness.

## Boundary Decision

DataRights owns:

- the approved operation revision and destructive execution lifecycle;
- one PII-free work item per selected owner record;
- work-item retry, blocker and terminal outcome state;
- the append-only authoritative processing ledger;
- the external ledger-delta contract and restore checkpoint;
- case completion derived from immutable owner and ledger proof.

Guests owns:

- Guest-specific hold and operational-obligation checks;
- the exact fields and projections that anonymisation changes;
- the `Anonymised` Guest lifecycle state;
- an immutable, idempotent owner completion receipt;
- the Guests tombstone projection;
- replaying an authoritative tombstone after restore;
- the PII-free status event consumed by dependent modules.

Reservations and later consumers own their local eligibility projections. They
must not read Guests or DataRights tables. DataRights must not write another
module's schema.

GMA is unchanged. GMA tasks, messaging, outbox/inbox, access control, recent
authentication policies, runtime abstractions and persistence primitives are
already sufficient. Guest rights vocabulary, legal holds, anonymisation rules,
processing ledgers and restore tombstones remain BunkFy product concerns.

## Scope And Authorization

A `GuestProfile` is tenant-owned even though discovery and ordinary operations
are property-scoped. Anonymising it removes the shared identity for every
property in the workspace, not only the property that routed the request.

Therefore:

- the case remains property-scoped for routing, discovery and country context;
- destructive execution requires a dedicated tenant-scoped permission;
- the executing subject must still have access to the routing property;
- the owner resolves every property where the Guest is visible and evaluates
  current policy, holds and operational obligations for each one;
- an unknown property, missing projection, unsupported policy, inaccessible
  scope or incomplete property set blocks execution;
- a property-scoped role cannot use one property's permission to destroy a
  shared workspace Guest Record.

The execution endpoint also requires the configured recent-authentication and
MFA assurance. That assurance authorizes enqueueing only; the durable work item
still revalidates current case approval and country-policy evidence immediately
before owner mutation.

## Approval Evidence

An approved operation flag is not enough authority for irreversible work.

For Anonymisation, the immutable DataRights decision revision must also bind:

- exactly one operation: `Anonymisation`;
- tenant and routing property;
- selected owner, record type, record id and record version;
- policy id, version and content digest;
- retention policy id and version;
- decision evidence revision and stable disposition code;
- whether actor separation was required and the approving actor;
- decision time and approval revision.

The decision does not store names, contact values, free-text legal advice or
exported records.

Owner execution rechecks the existing approval gate and current country policy
for every affected property. A changed policy digest, retention policy,
property set, Guest version or selected coordinate blocks the work item and
requires review; it is never silently accepted as equivalent.

## Execution Lifecycle

Add explicit DataRights execution transitions:

1. `Approved` to `Executing` creates bounded, immutable work items and enqueues
   one PII-free task for the operation revision.
2. An owner work item can become `Completed`, `NoOp`, `Blocked` or `Failed`.
3. A stable blocker moves the case to `Blocked`; retryable infrastructure
   failure leaves it `Executing`.
4. Equivalent task retries resume the same work item.
5. The case becomes `Completed` only when every work item has an externally
   protected ledger delta and matching in-database ledger entry.
6. A later slice may use `PartiallyCompleted` for a multi-owner case. This
   single-owner slice must not infer partial completion from task status.

The DataRights task calls a versioned product contributor contract. The request
contains only case/approval coordinates, owner coordinates, operation revision,
policy coordinates, idempotency key and deadline. The result contains only a
stable outcome, blocker code or owner receipt coordinates and digest.

The owner mutation and DataRights ledger cannot share a database transaction.
Safety comes from this retry order:

1. Guests commits anonymisation, local tombstone, owner receipt and outbox
   event atomically.
2. A retry returns the committed owner receipt without mutating again.
3. DataRights writes the external ledger delta idempotently.
4. DataRights appends the in-database ledger entry and completes the work item.

If the process stops after any step, replay resumes from durable proof. A
completed in-database case can never exist without the external restore delta.

## Guests Eligibility And Holds

Guests evaluates destructive eligibility from its own authoritative data and
projections. It does not ask DataRights to decide Guest-domain facts.

Introduce independently releasable Guest data holds scoped by tenant, property
and Guest. A hold records only:

- hold id, property and Guest coordinates;
- stable reason code;
- placed/released actor and time;
- local optimistic version and state.

Sensitive free-text legal advice is not stored in the hold or emitted in
events. More than one hold may apply; anonymisation remains blocked until every
applicable hold is released.

Guests also blocks anonymisation when:

- the Guest has an active or future stay that still requires operational
  identity;
- a stay/property projection is missing, stale or unsupported;
- any affected property is unknown, inactive or lacks an allowed current
  erasure policy;
- the selected Guest version is stale;
- the Guest is already archived instead of active;
- another destructive operation for the same Guest is unresolved.

Completed historical stays do not require identity fields to remain. Their
non-identifying operational facts and random Guest coordinate may remain behind
the tombstone. If a later module proves a statutory retention requirement for a
specific identifying field, it must report a stable blocker rather than
silently weakening anonymisation.

Hold placement and release use dedicated permissions, exact versions and
immutable receipts. Ordinary workspace roles receive no hold-management or
destructive permission by default.

## Guests Anonymisation

Add `Anonymised` as a terminal state distinct from `Archived`.

One aggregate operation atomically:

- replaces the required display name with a fixed non-identifying label;
- clears legal name, email, phone, date of birth, nationality, preferred
  language and notes;
- clears or replaces every corresponding search field;
- advances the Guest version and terminal timestamp;
- raises a PII-free anonymised domain event;
- writes the local tombstone and immutable owner receipt;
- preserves only the random Guest id and minimum non-identifying operational
  facts needed for referential and audit correctness.

An anonymised profile is excluded in database predicates from ordinary list,
detail, search, update, archive, discovery and export surfaces. Rights and
restore replay may address it only through explicit internal coordinates.

The operation must not rewrite `CreatedBy` or other staff audit attribution as
though staff identity belonged to the Guest. Staff rights are handled through
the Staff owner workflow.

### Owner Receipt

The Guests receipt contains:

- receipt and idempotency ids;
- case, approval and operation revisions;
- tenant and routing-property coordinates;
- Guest id and selected/resulting versions;
- disposition and stable reason;
- affected-property count;
- policy evidence digest;
- event id, actor and completion time;
- a canonical receipt digest.

It contains no removed value, name, contact, free text or legal advice.
Equivalent retries return byte-equivalent receipts. Reusing an idempotency key
with changed coordinates, policy evidence or expected versions fails with a
stable conflict.

## Tombstone Contract

The Guests local tombstone is keyed by tenant and Guest and stores:

- supported tombstone contract version;
- monotonic tombstone revision;
- anonymised state and completed time;
- authoritative ledger-entry id when confirmed;
- owner receipt digest and last replay time.

Absence is allowed only for a never-anonymised Guest during normal operation.
An unknown or future tombstone version fails closed at restoration and
reprocessing boundaries.

Guests publishes a versioned PII-free anonymisation event and bounded rebuild
export. Reservations consumes it into its own Guest eligibility projection so
new links are denied. Existing factual reservation links and stay history are
not silently removed by this Guests slice.

## Authoritative Processing Ledger

Use a separate append-only entity rather than adding ledger history to the
case aggregate.

An entry records:

- ledger entry id and monotonic tenant sequence;
- case, approval and operation revisions;
- tenant and routing property;
- owner module, record type and stable opaque record pseudonym;
- disposition, stable reason and completion time;
- policy id/version/digest and retention policy id/version;
- owner receipt id and canonical digest;
- previous-entry digest and entry digest;
- replay/supersession coordinates where applicable.

No update or ordinary delete path exists. The database enforces uniqueness for
the owner receipt and case-operation coordinate. Conflicting duplicates fail.

The record pseudonym uses a versioned, deployment-supplied keyed
pseudonymisation service. Production readiness fails when that key is absent or
uses a development value. Key rotation preserves the version needed to compare
existing entries.

## External Ledger Delta

The database is not the only copy of deletion proof because a database restore
can roll it back.

Define a BunkFy `IDataRightsLedgerDeltaStore` port with:

- idempotent append by ledger-entry id and digest;
- ordered reads after a trusted tenant checkpoint;
- durable flush acknowledgement;
- integrity verification and bounded paging;
- no list-all-tenants operation.

The public repository includes a local protected-file adapter for development
and restore tests. It uses append-only records, restrictive file permissions
where the platform supports them, durable flush and an integrity chain.
Production configuration rejects that adapter and requires the private hosted
immutable-store implementation.

The external delta may contain an encrypted owner replay envelope so the owner
can locate a restored row without scanning every Guest. The in-database ledger
stores only the opaque pseudonym. Encryption keys are deployment supplied,
versioned and never written to configuration files, logs, tasks or ledger
records.

## Restore Gate

API and Worker readiness must remain false until the restore gate has:

1. loaded and verified the trusted external checkpoint;
2. read every bounded ledger delta newer than the database checkpoint;
3. appended any missing DataRights ledger entries idempotently;
4. invoked the matching owner restore contributor for every missing tombstone;
5. re-anonymised restored Guest identity and search fields transactionally;
6. confirmed the Guests local tombstone and owner receipt digest;
7. advanced the database checkpoint only after all owners succeed.

Restore replay is not a normal anonymisation command. It is authorized by the
verified external ledger proof, runs through an internal contract, and does not
depend on a restored case approval that may be older than the delta.

Unknown owner, unsupported contract/key version, invalid chain, missing delta,
digest mismatch or owner replay failure keeps readiness false and surfaces a
stable operational error without logging subject coordinates.

## Efficiency

- Work items, receipts, holds, tombstones and ledger entries use tenant-first
  fixed-width indexes.
- Guest eligibility resolves the bounded distinct property set from indexed
  owner projections; it does not scan a tenant.
- One work item processes one owner record and one operation revision.
- External delta reads are ordered and bounded by tenant checkpoint.
- Restore replay addresses the owner through an encrypted opaque coordinate;
  it does not hash every Guest to find a pseudonym.
- Events, tasks and receipts are constant-size and PII-free.
- Generic GMA command retries are unchanged; retry behavior is local to the
  destructive work item and owner contributor.

## Delivery Steps

1. [In progress] Persist destructive approval evidence and add explicit
   execution/work-item lifecycle without enabling owner mutation.
2. Add Guest data holds, eligibility checks and exact stable blocker codes.
3. Add `Anonymised`, the aggregate mutation, owner receipt, local tombstone,
   event and ordinary-surface enforcement.
4. Add the versioned owner contributor and retry-safe DataRights task
   orchestration.
5. Add the append-only in-database ledger, keyed pseudonym and canonical
   receipt/entry digests.
6. Add the external delta port, local protected-file test adapter and
   production configuration denial.
7. Add the startup restore gate and Guests restore contributor.
8. Update Reservations eligibility from the versioned Guests event and rebuild
   export through contracts only.
9. Advance executable personal-data catalogues and generated OpenAPI contracts.
10. Prove PostgreSQL migrations, concurrency, Docker task recovery, pre-ready
    restore replay, architecture boundaries and exact-commit GitHub gates.

Only one numbered step is implemented at a time. Later steps may refine code
from completed steps, but owner mutation remains closed until every prerequisite
gate needed for that mutation is present.

### Completed Slice: Verified Destructive Approval Evidence

- Anonymisation can be approved only as the exact standalone operation.
- DataRights resolves the routing property's governance binding from its own
  event-fed projection; clients cannot submit trusted policy coordinates.
- Approval re-evaluates the current country-policy pack for the erasure surface
  and freezes the property revision, policy and retention versions, content
  digest, purpose, surface, provenance and evaluation time into the case.
- A PostgreSQL constraint rejects approved standalone anonymisation without the
  complete evidence set. The migration stops for manual review if legacy rows
  would otherwise violate that invariant.
- The approval gate requires a bounded executor identity and enforces
  approver/executor separation before returning the frozen evidence.
- Properties lifecycle and processing-policy events populate the local
  projection. A tenant-scoped, resumable worker task rebuilds it through the
  Properties export contract.
- No execution endpoint, work item or Guests mutation is enabled by this
  slice. Delivery step 1 remains open until the durable execution lifecycle is
  implemented.

## Acceptance Evidence

- Property-only authorization cannot anonymise a tenant-owned Guest.
- Wrong tenant, property, owner, record type, Guest version, case revision,
  policy digest or retention policy cannot execute.
- Active/future stays, any active hold, unknown property state and stale policy
  produce stable blockers and no owner mutation.
- Anonymisation clears all profile and search PII, including notes, in one
  transaction and makes the Guest terminally non-operational.
- The owner receipt is committed with the mutation and is byte-stable on retry.
- A crash after owner mutation resumes from the receipt without changing the
  Guest again.
- A case cannot complete before external delta durability is acknowledged.
- Ledger duplicates are idempotent; conflicting receipt digests fail.
- Restoring a pre-anonymisation database and starting API/Worker with the newer
  external delta re-anonymises the Guest before readiness.
- Missing or tampered external deltas keep readiness false.
- Reservations denies new links after event delivery and after projection
  rebuild; existing factual records remain.
- Personal-data catalogue, payload, log/metric and architecture tests prove no
  removed value leaks into contracts or infrastructure surfaces.

## Non-Goals

- Hard deletion of Guest or reservation history.
- Reservations, Ingestion, Notifications, Staff or Workspaces owner
  anonymisation semantics.
- Automatic retention scheduling under SP-003.
- Public Guest self-service.
- Private immutable-cloud-store implementation or key-management procedures.
- Moving BunkFy rights or ledger concepts into GMA before another product
  proves a vocabulary-neutral reusable contract.
