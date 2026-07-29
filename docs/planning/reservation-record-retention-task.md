# Reservation Record Retention Task

Status: in progress; implementation and final local gates complete, publication pending

## Outcome

Add automatic, country-policy-driven minimisation for terminal reservation
records. Reservations remains the sole owner of candidate selection, legal-hold
checks, policy evaluation, mutation, idempotency, receipts, and tombstones.
Retention receives only bounded counts and stable outcome codes through the
existing `IRetentionExecutionContributor` contract.

The first slice covers one country-policy data class,
`reservation-operational`, with trigger `reservation-ended`. When the approved
period has elapsed, the owner removes guest identity, contact details, notes,
guest links, sensitive details history, direct provider references, pending
reminders, and reducible adapter evidence while retaining the non-personal
minimum reservation, stay, allocation, and accountability facts.

## Boundary

- Reservations owns all reservation records and every retention-side mutation.
- Retention owns generic schedule reconciliation, PII-minimized execution
  attempts, health, retry, and operator status.
- GMA owns leases, retries, cancellation, heartbeats, worker execution, and
  terminal task history. No BunkFy data class or policy belongs in GMA.
- Properties remains authoritative for topology and processing-policy facts.
  Reservations uses only its local projections and never reads another
  module's database.
- Country-policy packs authorize the purpose, surface, source provenance,
  retention data class, trigger, period, and policy evidence.
- A selected retention policy id/version is a versioned rule set; its rules are
  unique by policy id, version, data class, and trigger so one property binding
  can resolve multiple owner data classes.
- Existing data-rights anonymisation and automatic retention are separate
  authorities. They share the domain redaction primitive and owner-local
  persistence reduction, but not approval coordinates or receipts.

## Decisions

### Tenant-scoped scheduling

The contributor registers one tenant-scoped schedule. A property-scoped
schedule would stop when a property is suspended or retired and could strand
old reservation PII. The owner instead scans the tenant's terminal reservations
fairly, then resolves the exact current property projection and country policy
for each candidate. An inactive organization still receives no new ordinary
work from the Retention control plane.

### Immutable terminal trigger

`UpdatedAtUtc` is not a retention trigger because legitimate post-stay
correction, guest-link, restriction, hold, and anonymisation activity can
change it. Reservations therefore persists `TerminalAtUtc` exactly once when
the reservation first reaches `AllocationRejected`, `Cancelled`, `NoShow`, or
`CheckedOut`.

Existing terminal rows are backfilled conservatively from their terminal
lifecycle timestamps, with `UpdatedAtUtc` and then `CreatedAtUtc` as bounded
legacy fallbacks. The value never changes after the first terminal transition.
The retention deadline is `TerminalAtUtc + current approved policy period`.

### Eligibility

A reservation is eligible only when all of these remain true at mutation time:

- the tenant, reservation, property, record version, details revision, and
  terminal trigger are valid;
- the reservation is terminal, not already anonymised, and has no pending
  allocation amendment or release;
- the current processing-restriction projection contract is available;
- no active reservation data hold exists;
- the property projection and governance binding are current and usable,
  including for a retired or processing-suspended property;
- the country pack permits purpose `reservation-retention`, surface
  `retention`, source provenance `retention-worker`, data class
  `reservation-operational`, and trigger `reservation-ended`;
- the resolved period has elapsed.

Missing or stale owner facts fail closed. A due active hold returns a blocked
outcome and bounded review timestamp. A record changed between scan and mutation
is re-evaluated under its new version and skipped safely.

### Mutation and authority

The automatic path acquires the existing reservation operation lock, reloads
and re-evaluates the candidate, invokes the existing aggregate anonymisation
primitive with actor `system:retention`, and uses the existing owner-local
redaction repository for related history, adapter evidence, reminders, and
guest links.

It writes:

- an append-only retention execution and fair-scan checkpoint;
- an append-only retention anonymisation receipt containing only owner-local
  coordinates, selected/resulting versions, terminal trigger, deadline,
  policy digest, reduction counts, event id, actor, and completion time;
- the existing reservation anonymisation tombstone with explicit
  `Retention` authority.

Existing data-rights tombstones are migrated to explicit `DataRights`
authority. Data-rights restore accepts only `DataRights` tombstones; an
automatic retention decision cannot be restored through a rights-case replay.
The retention receipt raises the existing reservation-anonymised event so
owner projections and downstream module contracts remain coherent.

### Fairness and efficiency

- Scan the indexed, non-anonymised terminal set by monotonic
  `ProjectionOrdinal`.
- Persist one cursor per tenant, data class, and execution-policy version.
- Read at most `ScanSize + 1` candidate heads and load bounded related facts
  with set-based queries.
- Mutate at most `MutationBatchSize` records per occurrence.
- Reset the cursor only after reaching the end, so a large tenant cannot starve
  later records and newly terminal earlier ordinals are picked up on the next
  cycle.
- Do not place reservation ids, property names, guest data, provider
  references, or free text in Retention payloads, logs, metrics, or receipts.

## Delivery

1. [x] Persist immutable terminal triggers and harden terminal transitions.
2. [x] Add Reservations-owned retention execution, checkpoint, receipt, and
   tombstone authority.
3. [x] Add bounded candidate queries, country-policy eligibility, and exact
   mutation-time re-evaluation.
4. [x] Register the tenant-scoped Retention contributor and validated runtime
   options.
5. [x] Add the development and integration country-policy purpose/rule and
   update the checked-in personal-data catalogue.
6. [x] Add focused domain, application, persistence, contributor,
   architecture, and catalogue coverage.
7. [x] Extend the existing PostgreSQL/Worker control-plane proof without
   creating another container.
8. [ ] Run the complete non-Docker gate once, the relevant Docker gate once,
   then publish and verify exact candidates.

## Evidence

- Focused Reservations retention tests pass 24/24, including domain,
  eligibility, contributor, mutation, policy-version cursor, append-only
  receipt, tenant boundary, and model constraints.
- Reservations personal-data catalogue tests pass 8/8, the generated inventory
  is current, and the focused module-boundary architecture guard passes.
- The final Reservations migration builds without warnings and reports no
  pending model changes. Its upgrade path backfills terminal timestamps and
  migrates legacy tombstones to explicit DataRights authority before enforcing
  the new constraints.
- The existing two-tenant Retention worker scenario now includes one due
  Reservations candidate per tenant and verifies bounded central results,
  owner execution, redacted history, receipt, and Retention-authority
  tombstone.
- The complete non-Docker gate passed after correcting two stale rule-set and
  catalogue-version test expectations. Solution synchronization, source-package
  boundaries, the full build with zero warnings, migration drift, architecture,
  and all non-Docker test projects are green.
- The complete Docker gate passed 63/65 before exposing two isolated issues:
  a legacy migration assertion still expected tombstone contract v1, and the
  Reservations candidate query applied its cursor predicate after a correlated
  projection that PostgreSQL could not translate. Both were fixed as one batch;
  the exact migration scenario and the full two-tenant Retention scheduler
  scenario then passed 1/1 independently. The other 63 Docker scenarios were
  not replayed because neither correction touched their execution surfaces.
- Retention now logs owner exceptions with owner, data class, execution, and
  attempt coordinates before persisting the bounded generic failure outcome;
  payloads and durable outcomes remain free of reservation and guest data.

## Acceptance

- A terminal reservation becomes due only after its exact approved deadline.
- Active/future/pending, held, stale-policy, stale-projection, and changed
  reservations are never anonymised.
- Retired and processing-suspended property records continue to converge while
  their organization remains active and their last approved binding is usable.
- Exact retry returns the persisted result; partial progress resumes from the
  persisted cursor without duplicate mutation or proof.
- Data-rights and retention authority cannot impersonate or restore each
  other.
- The owner mutation removes every currently catalogued direct reservation
  identity surface and preserves minimum non-personal operational facts.
- Retention sees only counts, timestamps, stable codes, and schedule
  coordinates.
- Architecture tests prove Reservations references Retention only through
  Contracts and neither module reads the other's persistence.

## Deferred

- Final legal approval of periods, enabled country packs, and market-specific
  exceptions.
- Tenant-termination deletion and backup/object-store expiry.
- Retention of anonymisation receipts, holds, restriction receipts, message
  journals, and other accountability data classes.
- Provider-side deletion or remote OTA workflows.
- Financial, tax, payment, invoicing, and statutory accounting records that a
  future Accounting module must own.
- Generic retention policy abstractions in GMA.
