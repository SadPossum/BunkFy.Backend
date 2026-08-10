# EF Enum Default Sentinel Hardening Task

Status: complete
Date: 2026-08-10

## Goal

Make every database-generated enum default explicit about the CLR value that
means "not set". This removes EF model-validation ambiguity without changing
the existing database defaults, check constraints, migrations, or domain
states.

## Finding

The production-shaped migration rehearsal emits thirteen
`BoolWithDefaultWarning` diagnostics. In each case an enum has `Unknown = 0`,
the database default is a valid nonzero state, and the EF model does not state
that `Unknown` is the sentinel. EF therefore substitutes the database default
whenever the CLR value is `Unknown`, but that behavior is implicit and easy to
change accidentally.

GMA Auth already uses the intended reusable pattern:
`HasDefaultValue(...).HasSentinel(default)`. GMA Notifications owns one affected
model; BunkFy owns the remaining property-processing projections and tenant
lifecycle records.

## Ownership

- GMA Notifications fixes and tests `UserNotification.DeliveryPolicy` in its
  own repository.
- Each BunkFy module explicitly configures its own enum sentinel.
- The BunkFy migration host owns a composition-level guard that discovers all
  registered DbContexts and rejects enum defaults without an explicit zero
  sentinel.
- No product-specific convention is added to GMA Framework.

## Invariants

1. `Unknown` remains invalid persisted business state and means unset only at
   the EF insert boundary.
2. Existing valid database defaults remain unchanged.
3. No schema migration is generated for model-only sentinel metadata.
4. The guard discovers composed contexts instead of maintaining a module list.
5. GMA and BunkFy changes are committed independently before parent pointers
   move.

## Verification

- Run the focused GMA Notifications model test and module test suite.
- Run the BunkFy migration-host model guard and migration drift checks.
- At slice completion, run one complete non-Docker backend gate.
- Do not run a Docker rehearsal for this model-only change: it alters no schema,
  query, transaction, or provider-specific behavior, so migration drift and the
  composed-model guard are the relevant regression evidence.

## Done When

All thirteen warnings are gone, both regression guards pass, migration
snapshots remain drift-free, the deployed schema contract is unchanged, and
the independently published GMA pointer is recorded by BunkFy.

## Completion Evidence

- GMA Notifications commit `3566efe` adds the explicit delivery-policy
  sentinel and a design-time model regression test; its non-Docker module gate
  passed 130 tests with clean SQL Server and PostgreSQL migration drift.
- The BunkFy composed-model guard discovers every registered DbContext and
  rejects an enum database default without an explicitly configured zero
  sentinel.
- The consolidated backend build completed with zero warnings and errors, all
  SQL Server and PostgreSQL migration drift checks passed, and the former
  thirteen EF diagnostics are absent.
- The fail-fast documentation finding was corrected, then the remaining suites
  passed: Architecture 101, migration host 26, service defaults 63, and
  non-Docker integration 60.
- No Docker rehearsal was run because neither the relational schema nor
  provider-executed behavior changed.
