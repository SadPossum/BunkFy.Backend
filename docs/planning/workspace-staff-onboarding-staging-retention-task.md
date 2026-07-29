# Workspace Staff Onboarding Staging Retention Task

Status: complete
Date: 2026-07-28

## Goal

Expire and redact abandoned staff-onboarding profile data after its
Organizations-owned enrollment link has naturally expired, without racing a
claim that was validly submitted before the deadline or copying
Organizations lifecycle authority into BunkFy.

## Ownership

- Organizations owns enrollment-link and enrollment-claim deadlines, claim
  state, durable terminal history, and the generic exact claim-inspector
  contract.
- Workspaces owns staged staff profile data, access plans, onboarding state,
  and the product decision to retain, reconcile, or redact that data.
- Retention owns tenant schedule execution, bounded run receipts, health, and
  retry visibility. It does not read Workspaces or Organizations storage.
- GMA Task Runtime owns generic scheduling, leases, retries, and worker
  execution. No new hosted service is introduced.

## Eligibility

A Workspaces application is a retention candidate only when all of the
following remain true in its transaction:

- its source is an enrollment link;
- its state is `Submitted`;
- it has no observed claim id or claim version;
- its matching access plan has observed `SourceExpiredAtUtc`;
- source expiry plus the configured grace period has elapsed.

Disabled or rotated links continue through the existing supersession flow.
Pending, accepted, failed, completed, rejected, superseded, and already
expired applications are not abandoned pre-claim staging.

This ordering closes the claim-creation race: once Organizations has expired
the link, it rejects new claims, while a claim submitted before expiry remains
available through the authoritative inspector during its history window.

## Reconciliation

For each bounded candidate, Workspaces queries
`IOrganizationEnrollmentClaimInspector` by exact organization, enrollment
link, and subject coordinates:

- no retained claim within the authority window: expire the application,
  redact every staged profile field, and expire the source-expired access plan
  when no active onboarding remains;
- `Pending`: observe the claim request and preserve profile data and the plan;
- `Rejected`: observe rejection, redact profile data, and finalize an unused
  source-expired plan;
- `Expired`: observe claim expiry, redact profile data, and finalize an unused
  source-expired plan;
- `Accepted`: observe acceptance and invoke the existing idempotent onboarding
  processor; a recoverable provisioning failure remains `Failed` for the
  existing retry workflow;
- unknown or inconsistent claim facts: fail closed.

The normal claim-change handler and manual retry path must also finalize an
unused access plan after a rejected or successfully processed claim when link
expiry arrived first. This makes either integration-event order converge.

## Authority Window

Organizations guarantees at least one day of terminal enrollment history and
deletes terminal claims before their parent link. Workspaces uses bounded
defaults:

- grace period: 2 hours;
- authority window: 20 hours from source expiry;
- schedule interval: 60 minutes;
- batch size: 50.

Configuration must keep the authority window below 24 hours and greater than
the grace period plus one schedule interval. If the inspector returns no claim
after the authority window, Workspaces does not redact the application. The
contributor returns failed outcome
`workspaces.staff-onboarding-staging.authority-lapsed` so operations can
reconcile the ambiguous record. A retained claim can still be reconciled after
that ceiling.

## Efficiency And Isolation

- Retention emits one deterministic tenant-scoped schedule for owner
  `workspaces`, data class `staff-onboarding-staging`, policy version 1.
- A tenant-first indexed query joins source-expired access plans to unclaimed
  submitted applications, orders deterministically, and takes one bounded
  batch.
- Each candidate is reloaded and revalidated in its own normal Workspaces
  transaction with the observed aggregate version.
- Inspector access is an exact, no-tracking Organizations projection query.
- No profile value, subject id, source id, or application id crosses into
  Retention task payloads, receipts, logs, or status views.
- Repeated schedules and stale candidates are idempotent no-ops.

## Delivery

1. [x] Advance the Organizations module pointer to the published generic
   claim-inspector contract.
2. [x] Add validated Workspaces retention options, coordinates, contributor,
   transactional reconciliation command, and bounded persistence port.
3. [x] Add the tenant-first retention index and PostgreSQL migration.
4. [x] Complete access-plan convergence in claim-change and retry paths.
5. [x] Update the Workspaces personal-data catalogue and development notes.
6. [x] Add focused domain, handler, contributor, composition, persistence, and
   one PostgreSQL proof.
7. [x] Run one complete backend non-Docker gate and one relevant Docker gate.

## Acceptance

- abandoned unclaimed staging is automatically redacted after source expiry
  and grace;
- a retained pending or accepted claim is never discarded because an
  integration event lagged;
- ambiguous missing history fails visibly without deleting personal data;
- either link-expiry/claim-change event order converges to the same access-plan
  state;
- candidate scans are tenant-isolated, indexed, deterministic, and bounded;
- Retention evidence remains PII-minimized;
- no BunkFy policy or domain type is added to GMA.

## Verification

- `eng/verify.ps1 -SkipRestore` passed solution synchronization, source-package
  guards, a zero-warning build, and migration drift. Its one documentation-index
  failure was fixed, then the failed architecture and remaining test tail passed.
- The complete non-Docker test set passed.
- The focused PostgreSQL
  `WorkspaceStaffOnboardingExpiryPersistenceTests` proof passed.
