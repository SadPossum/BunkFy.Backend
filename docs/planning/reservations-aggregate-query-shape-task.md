# Reservations Aggregate Query Shape Task

Status: complete

## Goal

Remove the ambiguous multi-collection Reservation query observed in the exact
Preview lifecycle rehearsal. New reservation creation must not load unrelated
guest-link history, while use cases that genuinely need the complete aggregate
must choose an explicit query shape that cannot multiply requested units by
guest-link history in one result set.

## Ownership

- Reservations owns which parts of its aggregate each use case loads, its
  processing-restriction gates, and the performance/consistency trade-off for
  those queries.
- GMA continues to own provider selection, command transactions, optimistic
  concurrency mapping, and generic persistence infrastructure. It does not
  choose a global split-query policy for project modules.
- SQL Server and PostgreSQL remain supported through the existing module
  persistence boundary. No provider-specific SQL belongs in the application or
  domain projects.

## Invariants

1. Creation replay still acquires the reservation coordinate lock before any
   replay lookup.
2. Creation replay loads the root and requested units needed by
   `MatchesCreation`; it does not load guest links.
3. Ordinary, data-rights, required-continuation, and external-source aggregate
   loads retain their existing restriction and anonymisation semantics.
4. Complete aggregate loads remain tracked and explicitly use split-query
   execution, preventing a requested-unit by guest-history cartesian result.
5. Reservation optimistic concurrency and operation-lock serialization remain
   authoritative for writes; this slice does not add broad retries or change
   transaction ownership.
6. Query helpers stay internal to Reservations persistence and expose no new
   cross-module contract.

## Efficiency

- The steady-state create path executes a narrow replay lookup after acquiring
  its already-required coordinate lock and avoids loading guest history.
- Complete aggregate hydration uses one root query plus bounded collection
  queries instead of repeating the wide reservation row for every collection
  cross-product.
- List queries and single-collection readers keep their existing projections;
  no global context query mode is changed.

## Verification

- A relational query-shape test makes EF treat ambiguous multi-collection
  loading as an error and proves the complete graph explicitly compiles in
  split-query mode.
- The same test proves the creation-replay graph does not reference the guest
  table.
- Focused repository and creation-replay tests preserve restriction, replay,
  and aggregate hydration behavior.
- Run the Reservations non-Docker tests during implementation, then one
  consolidated backend gate and one targeted PostgreSQL/runtime proof at slice
  completion.

## Completion Evidence

- Backend candidate `fd249b459fedefd23d823c937d95e37dd25f8b92`
  passed a zero-warning build, migration-drift checks, all non-Docker tests,
  the 261-test Reservations suite, and the 102-test architecture suite.
- Root candidate `f9371904d16a6dd15525133e49f8792646ff2130` ran as
  Preview release `preview-f937190` with API and Worker bound to backend image
  digest `sha256:ecd42ff3f041dbf30bf801046bd279111aff196093ed86158b0faddbf5e7dd92`.
- The self-contained Preview onboarding rehearsal passed 11 checks, including
  its 11-check Reservations/Inventory child lifecycle. The child evidence is
  `.tmp/deployment-probes/preview-onboarding-20260811T025745Z.reservations-inventory.json`;
  the umbrella evidence is
  `.tmp/deployment-probes/preview-onboarding-20260811T025745Z.json`.
- Fresh candidate API and Worker logs contained no
  `MultipleCollectionIncludeWarning` or equivalent ambiguous-collection query
  warning. One unrelated Inventory room-retirement delivery retried after an
  optimistic conflict and reached `Processed` on attempt 2; that convergence
  observation belongs to the next Inventory slice.
