# Workspace Operational Admission Integration Task

Status: completed
Date: 2026-08-05

## Goal

Restore production-shaped integration coverage after workspace termination
admission became fail closed. API tests that exercise tenant-owned mutations
must provision the authoritative Workspaces schema, and the staff onboarding
proof must demonstrate that a durable active access plan exists before either
an invitation or enrollment claim can grant membership.

## Ownership

- Workspaces owns the authoritative termination fence, operational-admission
  decision, staff access plans, and onboarding lifecycle.
- Properties, Inventory, Reservations, Guests, Staff, Ingestion, Organizations,
  and Access Control consume Workspaces decisions through contracts or product
  extensions. They do not infer workspace lifecycle state from their own data.
- Integration host fixtures own production-shaped schema preparation for every
  composed dependency used by a scenario.
- GMA owns the generic Organizations and Access Control mutation-policy
  extension points. It must not know BunkFy workspace fences or access plans.

## Audit Findings

- `AuthTestApplication.MigratePropertiesAuthorizationDatabaseAsync` prepares
  Organizations and the consumer module but omits `WorkspacesDbContext`. Since
  property authorization is the base for several other API fixtures, valid
  tenant mutations then receive the expected fail-closed unavailable decision.
- `MigrateIngestionDatabaseAsync` is also not independently complete for its
  Workspaces-backed tenant-lifecycle policy.
- Staff-specific setup migrates Workspaces directly, duplicating topology
  knowledge instead of sharing one explicit admission-schema helper.
- The end-to-end invitation/enrollment scenario consumes an issuance DTO but
  does not assert the active access plan carried by that DTO. A later onboarding
  failure therefore obscures whether issuance, persistence, or worker handling
  lost the plan.
- GMA's scoped EF context snapshots the scope during construction. Endpoint
  filters and message contributors can establish the scope later in the same
  dependency-injection scope, leaving query filters on the old value while
  mutation guards read the current value.
- Invitation and enrollment handlers observe acceptance before the onboarding
  processor acquires its operation lock. The processor then reloads the
  aggregate and discards that uncommitted transition, allowing the inbox row to
  finish while the onboarding application remains unchanged.
- Historical-migration tests that seed an old schema through a current
  admission-aware `DbContext` are separate module-owned fixture problems. The
  production contexts must not gain a legacy-schema bypass.

## Invariants

- Missing or unreadable Workspaces lifecycle state continues to deny mutation;
  tests become production-shaped instead of replacing the policy with allow-all.
- Schema preparation remains explicit and idempotent, with Workspaces migrated
  before a scenario sends tenant-owned mutations.
- Invitation and enrollment issuance return an active plan whose source,
  profile, and property scope exactly match the requested authority.
- No consumer project references Workspaces implementation or persistence;
  the extra persistence reference remains confined to the integration host.

## Delivery

1. [Completed] Centralize Workspaces admission-schema preparation in the
   shared API integration host and apply it to all dependent migration helpers.
2. [Completed] Add exact access-plan assertions to the invitation and enrollment
   end-to-end workflow.
3. [Completed] Add a static architecture guard so fixture topology cannot silently
   drop the authoritative schema again.
4. [Completed] Make the GMA scoped EF query-filter context live and cover scope
   changes that occur after context construction.
5. [Completed] Move invitation and enrollment acceptance into the processor's
   locked reload boundary and retain payload-free messaging diagnostics on
   asynchronous test timeout.
6. [Completed] Run focused Workspaces/architecture checks, then one exact Docker
   batch covering cross-module tenant mutation and restart-safe onboarding.

## Outcome

- Properties and Ingestion authorization fixtures now prepare the authoritative
  Workspaces schema through one shared helper.
- Invitation and enrollment issuance prove a durable active access plan before
  Organizations can grant membership.
- Scoped EF contexts follow the live scope established by endpoint or messaging
  contributors rather than retaining a constructor-time snapshot.
- Acceptance observations, aggregate reload, provisioning, and inbox completion
  now share one locked transaction boundary; both invitation and approval-based
  enrollment survive worker restart.

## Evidence

- Workspaces unit tests: 292 passed.
- GMA framework tests: 1,098 passed, including the live-scope regression.
- Architecture tests: 90 passed.
- Non-Docker integration tests: 54 passed.
- Integration test project build: succeeded with zero warnings and errors.
- Exact PostgreSQL and NATS onboarding scenario: 1 passed, including restart
  recovery and property-scoped access.

## Deferred

- Rewrite old-schema seed paths one owning module at a time using SQL or an
  explicitly historical model. Do not disable current tenant-revision or
  termination-fence enforcement.
- Run the broad Docker matrix only after those domain slices converge.
- Deployed multi-account onboarding smoke remains private environment evidence.

## Verification Cadence

Use only cheap Workspaces and architecture tests while editing. At the coherent
slice boundary, run one exact Docker filter containing the smallest cross-module
authorization scenario and the complete invitation/enrollment restart scenario.
Do not run the full Docker matrix or GitHub Actions for this slice.
