# Staff Owner Identity Bootstrap Hardening Task

Status: complete
Date: 2026-08-11

## Goal

Make workspace-owner Staff bootstrap monotonic and replay-safe. An authoritative
owner-joined event may create a missing Staff identity, but delayed delivery or
redelivery must never change an existing profile, resume employment, recreate
restricted data, or act after Organizations access is no longer active.

## Confirmed Gap

`OrganizationOwnerStaffBootstrapHandler` currently trusts the historical event
without checking current Organizations access. Its Staff contract also carries
an `IsActive` desired state, and the Staff handler suspends or resumes an existing
member to match it. A delayed owner-joined event can therefore undo a later Staff
suspension, while an event delivered after membership removal can still create a
new Staff profile.

This contradicts the Staff ownership rule that employment lifecycle is
independent from Organizations membership lifecycle.

## Ownership

- Organizations owns current organization and membership access. The existing
  `IOrganizationAccessDecisionReader` Contracts capability is authoritative.
- The Workspaces extension owns interpretation of the owner-joined event and
  distinguishes current denial from unavailable authority.
- Staff owns identity existence, profile data, employment lifecycle, creation
  serialization, restriction, and anonymisation behavior.
- GMA requires no change. Its Organizations access reader and Staff's existing
  transaction key lock already provide the needed generic primitives.

## Invariants

- Only a joined, active owner event is a bootstrap candidate.
- Current Organizations access must still be `Allowed` before Auth contact data
  is read or Staff is called.
- Current organization or membership denial acknowledges a stale event without
  mutation; unknown or unavailable authority fails for durable retry.
- The public Staff capability is bootstrap-only and exposes no membership or
  employment desired state.
- A non-empty source event id serializes the Staff creation operation and becomes
  the new Staff member id when creation is needed.
- Replaying the same source event for the same Auth subject is a no-op.
- Reusing that source event id for another Auth subject conflicts.
- Any existing Staff identity for the Auth subject, including suspended,
  departed, restricted, or anonymised data, makes bootstrap a successful no-op.
- Bootstrap never updates profile fields and never advances an existing Staff
  version.

## Delivery

1. Replace the lifecycle-shaped reconciliation contract and command with an
   explicitly bootstrap-only capability carrying the source operation id.
2. Admit the owner event against the current Organizations access decision before
   reading verified contact data.
3. Reuse the Staff creation-operation lock and safety-visible lookup; create only
   when neither the operation id nor Auth subject already owns a Staff identity.
4. Remove the obsolete cross-module lifecycle mutation path and align executable
   personal-data metadata.
5. Prove stale denial, unavailable-authority retry, exact replay, changed-subject
   conflict, hidden/existing lifecycle preservation, and concurrent persistence.

## Verification Cadence

- Use focused Staff and Workspaces-extension tests while editing.
- Run the complete affected suites, architecture guards, solution build, and one
  targeted PostgreSQL concurrency test at the slice boundary.
- No migration drift or full Docker matrix is needed because the persistence
  model and provider SQL shape do not change.

## Outcome

- The Workspaces extension now re-admits an owner-joined event against current
  Organizations access before reading Auth contact data or invoking Staff.
  Authoritative denial acknowledges stale work, while unknown or unavailable
  authority remains retryable.
- Staff exposes an explicitly bootstrap-only Contracts capability. It carries the
  source event id but no desired employment state, lifecycle reason, or update
  semantics.
- Bootstrap serializes the source operation, uses safety-visible identity lookup,
  and creates a Staff member only when neither the operation id nor Auth subject
  already exists. Existing, suspended, departed, restricted, and anonymised
  identities remain untouched.
- Exact replay and competing source operations converge through the existing
  transaction lock, scoped Auth-subject uniqueness, and persistence retry
  pipeline. No new receipt table or migration was required.
- The Staff personal-data catalog, generated inventory, data-rights export, and
  tenant-termination manifest now agree on catalog version 16, with a regression
  assertion preventing future version drift.
- GMA required no change because Organizations already owns the authoritative
  access reader and the framework already supplies the required transactional
  lock and retry primitives.

## Evidence

- `dotnet build BunkFy.slnx --no-restore -m:1`: succeeded with 0 warnings and
  0 errors.
- Staff module suite: 244 passed.
- Workspaces extension suite: 90 passed.
- Architecture suite: 102 passed.
- Targeted PostgreSQL exact-replay and competing-operation convergence test:
  1 passed.
- `eng/update-solutions.ps1 -Check`: backend solution synchronized.
- `git diff --check`: passed; all GMA submodule worktrees remained clean.

## Deferred

- Public multi-account invitation, QR/link, provider redirect, broker delivery,
  and process-restart evidence remains the workspace-onboarding deployment gate.
