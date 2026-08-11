# Guests Sensitive-Mutation Assurance Task

Status: completed
Date: 2026-08-11

## Goal

Require recent or stronger authentication for the public Guests mutations that
execute approved privacy changes or remove a data-preservation control, without
adding authentication concerns to the Guests domain model.

## Confirmed Boundary

- Guests owns correction execution, processing-restriction transitions, Guest
  data holds, their permissions, and their HTTP routes.
- GMA Security owns the reusable authentication-assurance requirement,
  evaluation, challenge, and endpoint metadata.
- The BunkFy public host owns deployment policy and maps its configured
  privileged-operation requirement to each Guests operation class.
- DataRights approval, exact revisions, idempotency, confirmation, permissions,
  and authentication assurance are independent controls; none replaces another.
- No GMA framework or reusable module change is required.

## Invariants

1. Applying a Guest correction requires `data-rights.execute`, the existing
   property scope, approved revision evidence, and configured correction
   assurance.
2. Applying or releasing a Guest processing restriction requires
   `data-rights.restrict`, the existing property scope and approval evidence,
   and configured restriction assurance.
3. Releasing a Guest data hold requires the dedicated hold permission,
   explicit confirmation, optimistic versions, and configured hold-release
   assurance.
4. Reading restrictions or holds and placing a hold do not require step-up;
   preserving data must remain available during an incident or investigation.
5. Ordinary Guest create, update, archive, directory, detail, and stay-history
   behavior does not change.
6. Internal DataRights contributor execution remains contract-driven and is
   not coupled to HTTP authentication metadata.
7. Assurance defaults remain optional for hosts embedding the module. BunkFy's
   public host configures every protected operation with its privileged
   operation requirement.

## Delivery

1. Add Guests API security options and register them with module composition.
2. Attach assurance metadata to the four protected public mutation routes.
3. Configure the options in the BunkFy public host.
4. Add route-metadata and host-composition guards proving the exact boundary.
5. Update the concise module documentation and run focused checks followed by
   one consolidated non-Docker backend gate.

## Verification Cadence

No Docker scenario is required because this slice changes no schema,
transaction boundary, broker behavior, provider behavior, or domain contract.
Focused Guests and architecture tests may run while editing; the complete
non-Docker backend gate runs once when the slice is coherent.

Completed evidence on 2026-08-11:

- `eng/verify.ps1 -SkipRestore` passed deterministic solution and
  source-package checks, a zero-warning full solution build, every migration
  drift check, and all non-Docker suites. The affected Guests, Architecture,
  and integration suites passed 168, 102, and 60 tests respectively.
- `pnpm contracts:check` confirmed the OpenAPI snapshot and generated
  TypeScript client are current.
- Focused Guests route-metadata tests, Architecture host-composition guards,
  and the public-host Release build passed before the consolidated gate.
- The GMA framework and reusable module checkouts remained unchanged. No
  Docker scenario was run because this slice changes no schema, transaction
  boundary, broker behavior, or provider-specific implementation.

## Deferred

- Product UI for Guest hold operations remains deferred until those operations
  have a staff-facing workflow. The standard API challenge is already usable by
  future clients.
- Different assurance strengths per operation can be configured later without
  changing Guests; the baseline deliberately maps all three option classes to
  BunkFy's privileged-operation requirement.
