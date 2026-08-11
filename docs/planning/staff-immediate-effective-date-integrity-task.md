# Staff Immediate Effective-Date Integrity Task

Status: complete
Date: 2026-08-11

## Goal

Keep the current Staff assignment and departure model honest. These transitions
take effect when the command commits, so a request must not use a future date to
make an already-current or already-ended record look scheduled.

## Confirmed Gaps

- Assignment, unassignment, and departure commands apply immediately but the
  aggregate currently accepts dates after the command clock's UTC date.
- A future assignment is therefore exposed as current immediately, while a
  future unassignment or departure ends operational state immediately.
- A new same-property assignment may be backdated into an already-ended
  assignment period, producing overlapping Staff-owned history.
- The Staff UI permits those future dates even though scheduled lifecycle state
  is not implemented.

## Ownership

- Staff owns immediate transition semantics, assignment-period integrity,
  request errors, events, and immutable operation receipts.
- The existing UTC command date is the conservative boundary for this first
  slice. Property-local business dates and scheduled transitions require an
  explicit future model rather than hidden clock behavior.
- Properties continues to own property lifecycle; Workspaces continues to own
  access plans and lifecycle coordination.
- GMA requires no change. Generic clocks, transactions, locks, retries, and
  outbox delivery already provide the required primitives.

## Invariants

1. Assignment `EffectiveFrom`, unassignment `EffectiveTo`, and departure
   `EffectiveOn` may be historical or equal to the command clock's UTC date,
   but never later.
2. Future-date denial occurs before aggregate mutation, policy coordination,
   operation-receipt creation, or event emission.
3. Exact replays of previously committed operations remain immutable and are
   resolved from their receipt before a fresh transition is evaluated.
4. A new open-ended assignment may not overlap any prior assignment for the
   same property. Because `EffectiveTo` is inclusive, its start must be later
   than every prior end date.
5. Current-assignment, one-primary, optimistic-version, property-availability,
   and safety-transition behavior remains unchanged.
6. Workspace reconciliation continues to use the command date and remains a
   desired-state operation.
7. Workspace reconciliation rejects a legacy future-current assignment before
   mutating any assignment in the aggregate.
8. The browser date pickers expose the same maximum date as the server.

## Delivery

1. Enforce the immediate-date boundary in the Staff aggregate using the
   supplied command timestamp.
2. Reject overlapping same-property assignment periods before advancing the
   aggregate.
3. Add focused domain and application replay coverage.
4. Cap assignment, unassignment, and departure pickers at the current UTC date
   so ordinary users cannot compose a request the server rejects.
5. Reconcile the superseded onboarding task note with the current lifecycle
   boundary.

## Upgrade Note

This project has not entered hosted production. Before a database created by an
older build is promoted, audit current Staff assignments for an
`EffectiveFrom` later than their command date. Reset or explicitly remediate
such preproduction rows; the application now fails closed instead of inventing
an end date for ambiguous history.

## Verification Cadence

- Use focused Staff tests and web type checking while editing.
- At the slice boundary, run the complete Staff suite, architecture guards,
  web tests/build, solution synchronization, and one warning-free solution
  build.
- No Docker or migration run is needed because persistence shape, provider SQL,
  locking, and operation-journal schema do not change.

## Deferred

- Scheduled assignments, future departures, activation jobs, property-local
  business dates, and explicit scheduled/active/ended states require a separate
  product slice.

## Outcome

- Staff now rejects future immediate dates without changing aggregate state or
  binding an operation receipt.
- Same-property assignment history cannot overlap, and workspace
  reconciliation detects legacy future-current rows before mutating a plan.
- Staff date controls use the same UTC-day maximum as the server.

## Evidence

- Staff tests: 248 passed.
- Architecture tests: 102 passed.
- Web verification: 50 test files and 258 tests passed; lint, typecheck, and
  production build passed.
- `BunkFy.slnx` synchronization check passed.
- Full solution build passed with 0 warnings and 0 errors.
- Docker and migration rehearsal were intentionally not repeated because this
  slice changes neither persistence shape nor provider behavior.
