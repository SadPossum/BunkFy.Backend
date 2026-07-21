# Workspace Staff Access Lifecycle Task

Status: planned; implementation follows workspace access-profile migration
Date: 2026-07-21

## Goal

Make Staff suspension, resumption, and departure coordinate workspace membership and access profiles without privilege windows, lost custom roles, or changes to unrelated workspaces and the global Auth account.

## Ownership

- Staff owns the Staff profile, employment status, property-assignment history, and lifecycle command validation.
- Organizations owns ordinary membership state and owner protection.
- AccessControl owns tenant-scoped profile assignments and authorization history.
- Workspaces owns the BunkFy process that coordinates those authorities and stores the exact reversible access plan.
- GMA stays product-neutral. The existing Organizations membership lifecycle and AccessControl profile facades are sufficient.

## Required Invariants

- Suspension and departure deny active workspace membership before Staff becomes suspended or departed.
- A failed later Staff write may leave access denied, but must never leave broader access than requested.
- Suspension records the exact active profile ids before denial. Resume restores that set, not a guessed default.
- Departure is terminal for this process and never restores access automatically.
- Owners are protected. An owner Staff profile cannot be suspended or departed through this flow until ownership is transferred through Organizations.
- Unlinked Staff profiles do not mutate Organizations or Auth.
- Only the exact workspace membership and profile assignments are touched. Other workspaces, platform administration, sessions, and the Auth account remain intact.
- Every transition is idempotent, version-aware, retryable, and operator-visible.

## Process Shape

Workspaces persists one lifecycle process per workspace and Staff member. It records Staff version, subject id, requested transition, captured profile ids, membership outcome, retry state, actor, and non-sensitive failure code.

For suspension or departure:

1. Staff invokes a Contracts policy seam before committing a linked lifecycle transition.
2. Workspaces creates or resumes the durable process and captures the exact profile assignment set.
3. Workspaces asks Organizations to suspend or remove the ordinary membership.
4. The membership event removes the marker, legacy compatibility grant, and active profiles.
5. Only after membership denial succeeds may Staff commit its lifecycle state.
6. The Staff lifecycle event marks the process complete. If Staff fails, the process remains denied and retryable.

For resume:

1. Workspaces records a resume intent while membership remains denied.
2. Staff becomes active first; this does not grant workspace authority.
3. The Staff lifecycle event restores ordinary membership, the permission-free marker, and the captured profile set.
4. Missing or archived profiles fail closed and require an explicit replacement plan.

## Staff Seam

Staff Contracts exposes a product-neutral lifecycle policy context containing workspace id, Staff id/version, optional Auth subject id, current/desired status, actor, and event id. Staff Application runs every registered policy before suspension, resume, or departure, so public API, Admin API, and CLI cannot bypass the coordinator. BunkFy host composition must register the Workspaces policy and guard that registration architecturally.

## Verification

- concurrent duplicate lifecycle commands have one durable effect;
- owner protection blocks both Staff and membership mutation;
- Staff failure after membership denial remains retryable and never reopens access;
- suspension/resume restores multiple custom profiles exactly;
- archived/missing restore targets remain denied;
- departure removes membership and profiles without touching another workspace;
- direct API, Admin API, and CLI all execute the same policy;
- PostgreSQL restart and broker-redelivery tests prove recovery.

## Not In This Slice

- Auth account disabling or global session revocation;
- ownership transfer UX;
- scheduled future departure execution;
- invitation profile/property plans;
- workspace role-management UI.
