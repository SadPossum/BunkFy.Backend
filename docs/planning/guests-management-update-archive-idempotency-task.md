# Guests Management Update And Archive Idempotency Task

Status: completed

## Goal

Make ordinary Guest profile updates and archive requests safe to replay after a
lost or interrupted HTTP response. A caller must be able to retry one logical
operation without producing another Guest version/event, while reuse of the
same operation id for different input fails closed.

## Boundary Decision

- Guests owns the operation semantics, request fingerprint, replay receipt,
  persistence, privacy lifecycle, and tenant portability.
- GMA continues to own transactional command dispatch and transient
  persistence retry. This slice needs no new generic idempotency abstraction or
  framework vocabulary.
- The public API, Admin API, Admin CLI, and web client supply a stable operation
  id. Authentication and actor attribution remain server-resolved on HTTP
  surfaces.

## Invariants

1. An operation id is scoped to one tenant and Guest. The Guest mutation lock
   serializes the first execution and any replay.
2. Update replay matches operation kind, property, Guest, expected version, and
   a canonical SHA-256 fingerprint of the normalized requested profile. The
   journal never stores the profile payload.
3. Archive replay matches operation kind, property, Guest, and expected
   version. Reusing an archive id for an update, or vice versa, conflicts.
4. The first successful mutation and its operation receipt commit in the same
   Guests transaction and outbox boundary. A failed mutation records nothing.
5. Replay returns the original status, version, and completion timestamp even
   if the Guest changed later. It does not emit another domain event.
6. Current permission and country-policy admission still run before an update
   replay. An operation receipt is not an authorization bypass.
7. The request fingerprint is pseudonymous personal data. It is catalogued,
   included in subject and tenant export, destroyed with the tenant, and
   deleted when the Guest is anonymised by either Data Rights or retention.

## Implementation

- Add `OperationId` to update/archive commands and API request contracts, plus
  `--operation-id` to the corresponding Admin CLI commands.
- Add a Guests-owned management-operation record/repository with update and
  archive kinds, request fingerprint, expected version, immutable result
  receipt, and tenant/property/Guest coordinates.
- Extend the existing Guest mutation coordinator so update/archive lock and
  reload through one path before reading or writing operation receipts.
- Normalize create, update, and archive success timestamps to PostgreSQL's
  microsecond precision before returning the first receipt, so a persisted
  replay remains byte-for-byte equivalent.
- Generate a PostgreSQL migration with tenant-first query indexes and a Guest
  foreign key. Preserve the persisted numeric terminal tenant-destroy stage
  while inserting journal cleanup into its non-linear stage progression. Keep
  the domain and application layers provider-agnostic.
- Add the operation record to Guests subject export, tenant export/destruction,
  the executable personal-data catalogue, and both anonymisation paths.
- Keep one stable operation id in the web form/archive confirmation while the
  same normalized request is being retried; allocate a new id when the request
  changes or succeeds.

## Verification

- `eng/verify.ps1 -SkipRestore` passed the full build, migration-drift checks,
  architecture checks, and non-Docker test suite with zero warnings or errors.
- `pnpm verify` passed 184 web tests, lint, type checking, and the production
  build; `pnpm contracts:check` also passed.
- The consolidated Docker batch ran 105 tests. It passed 103 and exposed two
  slice-local defects: PostgreSQL timestamp precision on replay and a tenant
  export fixture missing the new record type. After both corrections, the two
  failed tests passed together in one targeted Docker rerun; the unaffected 103
  tests were not rerun.
- Final focused checks passed all 15 create/update/archive idempotency tests and
  rebuilt `Integration.Tests` with zero warnings or errors.

## Deferred

- Durable browser storage across a fully closed tab. The current form lifetime
  covers retry after an ambiguous request while avoiding retention of Guest
  profile payloads in browser storage.
- A generic GMA operation-journal abstraction. Reconsider only after another
  module demonstrates the same stored semantics and lifecycle requirements.
