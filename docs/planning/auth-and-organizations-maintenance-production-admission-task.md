# Auth And Organizations Maintenance Production Admission Task

Status: complete
Date: 2026-08-05

## Goal

Prevent a Production BunkFy deployment from retaining identity and workspace
onboarding history indefinitely, expiring onboarding records inconsistently, or
running destructive maintenance from an undeclared process topology.

## Ownership

- GMA Auth owns generic bounded cleanup for expired exchanges, sessions,
  recovery and authentication challenges, authenticators, and failure history.
- GMA Organizations owns generic natural-expiry transitions and bounded cleanup
  of terminal invitation, enrollment-link, and enrollment-claim history.
- BunkFy owns the approved history windows, existing-history disposition,
  concrete Worker ownership, and deployment evidence for its product.
- Auth and Organizations remain independent admission policies. They may share
  one Worker profile today without requiring future deployments to couple them.
- No BunkFy workspace, Staff, role, notification, or deployment concept belongs
  in either reusable GMA module.

## Audit Findings

- Both GMA modules already provide opt-in, bounded, provider-neutral maintenance
  with startup validation and focused persistence proof. No reusable mechanic is
  missing.
- Auth persistence is composed by the public API, Admin API, and optional Worker,
  so enabling retention in more than one process can create avoidable cleanup
  contention.
- Organizations persistence has the same multi-host topology. Natural expiry is
  enabled only by the local AppHost, while retention remains disabled everywhere.
- Production can currently start with all three jobs disabled, leaving session,
  challenge, invitation, and enrollment history unbounded and delaying durable
  expiry events consumed by Workspaces.
- Enabling a cleanup switch would apply its windows to existing history
  immediately without a BunkFy-level approval acknowledgement.

## Invariants

- Auth retention and Organizations maintenance each have one independently
  declared Worker owner with an owner instance count of exactly one.
- API, Admin API, and non-owner Worker processes keep the relevant hosted jobs
  disabled.
- An owner composes the module it maintains. The Organizations owner enables
  both natural lifecycle processing and terminal-history retention.
- Every approved window is positive, bounded, and exactly matches runtime
  configuration on every long-running host.
- Production approval includes a non-secret evidence reference and an explicit
  decision to apply the approved windows to existing history.
- Development and tests retain opt-in composition and can run without production
  approval.

## Delivery

1. [Completed] Add Auth retention production admission, host composition,
   focused tests, and fail-closed configuration.
2. [Completed] Add Organizations lifecycle/retention production admission,
   independent owner topology, and focused tests.
3. [Completed] Enable both maintenance jobs on the local AppHost Worker and add
   architecture guards for owner/non-owner composition.
4. [Completed] Add the operator activation note and hosted-deployment references.
5. [Completed] Run focused checks, then one coherent non-Docker host gate.

## Outcome

- Auth retention and Organizations maintenance now have independent, fail-closed
  Production approvals and independently selectable Worker owners.
- API, Admin API, and non-owner Worker processes must keep the corresponding
  hosted jobs disabled; each owner must compose its reusable module.
- Runtime values are explicit on every composing host and must exactly match the
  approved windows before startup succeeds.
- Local AppHost composition exercises both jobs on one Worker, while production
  may split them without changing application code.
- GMA required no change: its existing bounded jobs, validation, persistence
  semantics, and tests remain the reusable owners of maintenance mechanics.

## Verification Evidence

- `dotnet build BunkFy.slnx --no-restore -m:1 --verbosity:minimal`: succeeded
  with 0 warnings and 0 errors.
- `BunkFy.Host.ServiceDefaults.Tests`: 61 passed, including 10 Auth-retention
  and 10 Organizations-maintenance admission tests.
- focused host runtime/configuration tests: 9 passed.
- complete non-Docker architecture gate: 87 passed.
- complete non-Docker integration gate: 54 passed.
- solution synchronization and `git diff --check` passed; only pre-existing
  line-ending notices were reported.
- no Docker scenario was required because no schema, cleanup query, broker, or
  provider implementation changed.

## Deferred

- Final legal and security history windows, approval references, and existing
  production-history decisions remain deployment inputs.
- Runtime configuration cannot prove orchestrator replica counts; deployment
  evidence must reconcile each declared single owner.
- Backup/restore drills, cleanup backlog alerts, and target-database execution
  remain environment activation gates.
- Cross-process singleton leasing is not added to GMA: these bounded services are
  deliberately host-enabled, while deployment topology remains a product concern.

## Verification Cadence

Use focused ServiceDefaults and architecture tests while editing. At the
coherent boundary, run the complete ServiceDefaults tests, non-Docker
architecture and integration gates, and a zero-warning solution build once. No
Docker scenario is required because this slice changes no schema, cleanup query,
broker behavior, or provider implementation.
