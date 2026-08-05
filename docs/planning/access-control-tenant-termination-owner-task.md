# Access Control Tenant-Termination Owner

Status: complete
Date: 2026-08-04

## Goal

Compose GMA Access Control's generic scope lifecycle into BunkFy's frozen
workspace termination flow without moving workspace, policy, or portability
semantics into GMA.

## Boundary

- GMA Access Control owns scoped role assignments, profiles, assignment
  history, closure admission, bounded destruction, principals that become
  globally orphaned, transport suppression, and durable lifecycle proof.
- BunkFy maps the canonical workspace id to
  `WorkspaceAccessScopes.Create(workspaceId)` and uses that same canonical id
  as the transport scope coordinate.
- BunkFy owns the frozen Workspace fence, owner dependency order, typed export
  schema, personal-data catalogue, result codes, and production composition.
- The adapter references `Gma.Modules.AccessControl.Contracts` only. Auth
  accounts and sessions remain global subject-owned identity data and are not
  part of this owner.

## Slice

1. Add the mandatory `access-control` export/destroy contributor after the
   `organizations` owner.
2. Validate every generic page, cursor, scope, record/store pairing, progress,
   receipt, selected revision, and frozen Workspace fence.
3. Export role assignments, profiles, profile assignments, and profile change
   history through an explicit field-level portability catalogue.
4. Wire API and Worker complete topology, owner-composition checks, closed
   output guards, and focused adapter tests.

## Acceptance

- a missing scope exports stable revision zero and can be destroyed/replayed
  even when its resulting global revision is greater than one;
- cross-workspace or malformed records fail closed;
- stale pages and active generic work remain retryable without terminal proof;
- completion is accepted only for the exact workspace coordinate,
  idempotency key, selected revision, batch size, proof shape, and fence; and
- BunkFy architecture guards prove the adapter consumes only GMA Contracts.

## Evidence

- all 140 fast GMA Access Control tests pass, including the selected-revision
  regression for a missing scope after unrelated global revisions;
- all 16 focused BunkFy adapter tests and four complete-topology composition
  tests pass;
- 50 focused architecture and personal-data output guards pass;
- PostgreSQL and SQL Server migration models are drift-free; and
- the exact PostgreSQL lifecycle scenario proves migration, bounded
  destruction, write closure, late-message suppression, and exact replay.
