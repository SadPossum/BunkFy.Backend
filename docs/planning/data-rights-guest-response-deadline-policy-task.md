# Data Rights Guest Response Deadline Policy Task

Status: complete
Date: 2026-08-04

## Goal

Assign truthful, auditable response deadlines to externally requested,
property-scoped Guest Rights cases without treating a calendar month as a fixed
duration, borrowing another scope's jurisdiction, or rejecting intake when a
deployment policy is temporarily unavailable.

## Ownership

- Data Rights owns the case deadline, frozen deadline-policy evidence,
  controller-routing gate, and operator presentation.
- Properties owns property topology, the local time zone, and the active
  governance-policy binding. Data Rights consumes those facts through its own
  projection only.
- `BunkFy.DataGovernance` owns the BunkFy country-pack schema, strict validation,
  calendar-rule evaluation, and immutable policy evidence. It is product shared
  kernel, not GMA.
- GMA remains unchanged. Generic CQRS, time, persistence, and messaging
  primitives do not need BunkFy rights or jurisdiction vocabulary.

## Findings

- `DueAtUtc` is persisted and returned but is never assigned.
- The current country-policy schema contains right-reference keys but no
  response-period or calculation-zone rule.
- Existing retention periods are invariant `TimeSpan` values. Reusing them
  would make calendar obligations such as one month incorrect around month
  length and daylight-saving changes.
- Data Rights already projects the exact property governance binding, but not
  the property time zone carried by the same versioned topology events.
- External requests require controller routing before discovery. Intake must
  still be recorded if the policy projection is unavailable; losing the
  request would be worse than surfacing a blocked routing step.
- Staff Rights cases are tenant scoped. Their employment jurisdiction cannot be
  inferred from any assigned or selected property and therefore cannot share
  this property's deadline path.

## Decisions

### Versioned Country Policy

- Preserve schema-v1 policy artifacts as immutable legacy inputs.
- Add schema v2 with required, bounded response rules for export, correction,
  restriction, and erasure rights.
- Represent a response period structurally as years, months, and days. Apply it
  in that order to the request's local receipt timestamp; do not convert it to a
  fixed number of seconds.
- Each rule allowlists calculation time-zone ids. Invalid, unavailable,
  duplicate, or non-allowlisted zones fail closed.
- Evaluate every requested right and use the earliest resulting deadline. The
  controlling right and rule reference are frozen in the case evidence.
- Add a synthetic v2 development artifact beside v1 and pin both digests. V1
  remains usable for existing processing but cannot authorize a new response
  deadline.

### Case Lifecycle

- Only `DataSubject` and `AuthorizedRepresentative` Guest Rights requests need
  this deadline. Controller-initiated cases and tenant termination do not.
- At case creation, attempt to resolve the current property binding and time
  zone at `CreatedAtUtc`. If successful, persist `DueAtUtc` and immutable policy
  evidence as part of the initial case revision.
- If resolution is unavailable, create the case with no deadline so intake is
  durable. Controller routing retries the same resolution from `CreatedAtUtc`
  and remains blocked until assignment succeeds.
- Once assigned, the deadline and evidence are immutable. Later property time
  zone, policy, allowlist, or pack changes affect future requests only.
- Existing pre-slice cases remain readable. An external Guest Rights case with
  no deadline follows the same routing-time repair path.

### Evidence And Operator Surface

- Freeze policy id/version/digest, operating country, controlling right,
  right-reference key, calendar period, calculation time zone, policy effective
  interval, evaluation timestamp, and property topology/policy source versions.
- Return bounded evidence only on case detail; the queue keeps its compact
  summary and existing `DueAtUtc` field.
- Show a due date, due-soon state, overdue state, or explicit policy-pending
  state in the controller UI. Overdue is derived from the immutable deadline;
  it is not another persisted case status.

## Delivery

- [x] Add schema-v2 country-policy response rules, strict validation,
  calendar/DST-safe evaluation, evidence, and focused shared-kernel tests.
- [x] Add and digest-pin the synthetic v2 development policy without mutating
  the v1 artifact.
- [x] Project property time zone into Data Rights with version-safe rebuild and
  persistence coverage.
- [x] Persist immutable response-deadline evidence on Guest Rights cases and
  add the PostgreSQL migration.
- [x] Resolve at intake, retry at controller routing, and fail routing closed
  while preserving the original request.
- [x] Align detail contracts, generated web types, due-state presentation, and
  focused backend/web tests.
- [x] Update module notes and run one coherent slice gate.

## Deferred

- Staff Rights deadlines require an explicit employment-jurisdiction path and
  selected Staff governance revalidation. No property country or time zone may
  be borrowed.
- Tenant termination is a controller-owned destructive workflow, not a natural
  person's rights-response deadline.
- Statutory extensions, pauses while awaiting identity evidence, holiday or
  business-day calendars, and superseding deadline history require explicit
  policy rules and auditable transitions; none are inferred in this slice.
- Due-soon and overdue notifications belong in a follow-up Operations
  Notifications integration after the case deadline is authoritative.
- Server-side due filters and ordering should follow measured queue needs; the
  current operational slice already provides bounded stable paging.

## Completion Criteria

- a calendar-month rule produces correct end-of-month and daylight-saving
  results in an allowlisted property time zone;
- external Guest Rights intake is never discarded because policy resolution is
  temporarily unavailable;
- discovery cannot begin after controller routing without an assigned deadline;
- the exact rule and property projection versions used for assignment are
  durably auditable and never silently rewritten;
- controller-initiated, Staff Rights, and tenant-termination behavior remains
  explicit and unchanged outside the documented boundary;
- no BunkFy policy leaks into GMA.

## Verification Cadence

Use focused shared-kernel and Data Rights tests while editing. At the coherent
slice boundary, run one full non-Docker backend gate and one full web gate. Run
one exact PostgreSQL Docker scenario because the slice changes Data Rights
schema and projection persistence; do not repeat it after every edit.
