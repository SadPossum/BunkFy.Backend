# Organizations Tenant-Termination Owner Task

Status: complete
Date: 2026-08-04

## Goal

Adapt GMA Organizations' product-neutral scope lifecycle to BunkFy's frozen,
protected tenant-termination workflow without moving BunkFy policy into GMA or
copying Organizations persistence into a product module.

## Ownership

GMA Organizations remains authoritative for the organization, memberships,
invitations, enrollment links and enrollment claims, their transport journals,
scope revision, close state and destruction proof.

The BunkFy composition extension owns:

- the `organizations` termination owner descriptor and dependency ordering;
- validation of the canonical workspace/organization coordinate and frozen
  Workspaces fence;
- the protected tenant-portability schema and personal-data classification;
- mapping generic typed records into BunkFy export records; and
- BunkFy retry, failure and completion result semantics.

Data Rights remains authoritative for approval, orchestration, protected
fragments and terminal proof. The extension owns no database.

## Contract

- The BunkFy tenant id must be the exact lower-case `D` representation of the
  GMA organization id and must equal the ambient scope.
- Export selects one Organizations scope revision and rechecks both that
  revision and the exact frozen fence after all five stores are streamed.
- Invitation and enrollment token digests are never available through the GMA
  facade and therefore cannot enter the export.
- Destruction executes one bounded GMA batch per call, preserves durable
  progress, maps active transport leases to retry, and accepts only exact
  operation-bound completion proof.
- Missing and legacy revision-zero scopes remain valid exact proofs. Nullable,
  not zero, denotes absence of owner proof in Data Rights.
- The owner depends on both `operations-notifications` and `retention`, placing
  generic organization closure after the complete current BunkFy owner chain.

## Slices

1. [x] Make Data Rights accept exact non-negative owner proof revisions and add
   the PostgreSQL constraint migration.
2. [x] Add the dedicated BunkFy composition extension and typed export schema.
3. [x] Add the executable personal-data catalogue and deterministic inventory.
4. [x] Register the owner in API/Worker composition and solution discovery.
5. [x] Prove missing, legacy, stable export, stale revision, malformed record,
   bounded destruction, replay, busy and fence-failure behavior with focused
   tests.
6. [x] Run the fast extension/composition/privacy checks and migration drift
   check once at the coherent slice boundary.

## Evidence

- `BunkFy.Extensions.DataRights.Organizations.Tests`: 16 passed.
- focused owner composition: 3 passed, including resolution against
  `Gma.Modules.Organizations.Persistence`.
- focused host/privacy architecture guards: 2 passed.
- `BunkFy.Modules.DataRights.Tests`: 320 passed.
- Operations Notifications owner regression: 13 passed.
- EF reports no pending Data Rights PostgreSQL model changes.
- the exact PostgreSQL migration scenario passed: both proof constraints require
  revision `>= 1` before upgrade and accept exact revision `0` after upgrade.

## Deferred

- Auth remains global subject-owned identity data, and Tenancy remains scope
  plumbing rather than an authoritative termination owner. The generic Files
  owner is not part of BunkFy's current composition; its admission boundary is
  documented separately.
- Final cross-owner destruction orchestration and production route enablement.
- Counsel-approved retention exceptions and external backup expiry evidence.
