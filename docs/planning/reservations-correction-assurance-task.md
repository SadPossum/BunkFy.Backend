# Reservations Correction Assurance Task

Status: complete
Date: 2026-08-11

## Goal

Require the configured recent-authentication assurance for direct execution of
an approved Reservations Data Rights correction. Preserve Reservations as an
optional module, keep routine front-desk mutations ergonomic, and reuse GMA's
existing provider-neutral assurance pipeline without adding framework policy.

## Audit Findings

- The central Data Rights correction workflow requires privileged-operation
  assurance before it opens or renews an execution claim.
- The Reservations owner endpoint is independently routable at
  `POST /api/reservations/properties/{propertyId}/data-rights-corrections`.
  It verifies the scoped `data-rights.execute` permission, approved case and
  revision, selected record version, execution identity, actor, country policy,
  and exact replay, but it does not currently enforce recent authentication.
- The equivalent Guests owner endpoint already enforces the same configured
  assurance. Leaving Reservations different creates an avoidable defense-in-
  depth gap for a privileged PII mutation.
- Reservation creation, guest/detail edits, inventory reassignment, guest
  linking, cancellation, check-in, no-show, and check-out are ordinary hotel
  operations with dedicated scoped permissions, actor provenance, and
  optimistic concurrency. They should not inherit this governance-only
  assurance requirement.
- GMA already owns optional `AuthenticationAssuranceRequirement` metadata and
  ASP.NET Core enforcement. No framework or reusable-module change is needed.

## Ownership

- Reservations API owns an optional correction-execution assurance setting and
  attaches it only to its public Data Rights correction endpoint.
- The BunkFy public API host binds that setting to the product's shared
  privileged-operation assurance requirement.
- Data Rights remains authoritative for case approval and the short-lived
  execution claim; Reservations keeps its owner-local gate and mutation.
- Admin API and Admin CLI remain governed by their separate operator boundary.
- GMA remains unchanged.

## Invariants

1. A standalone Reservations module with no configured requirement maps and
   runs without assurance metadata.
2. The BunkFy public API always configures Reservations correction execution
   with the same privileged-operation requirement used by the central Data
   Rights correction flow and the Guests owner endpoint.
3. Only the public Reservations Data Rights correction endpoint gains this
   requirement; routine Reservations endpoints remain unchanged.
4. Assurance is additive to scoped permission, tenant resolution, country
   policy, approved execution-claim validation, expected revisions, actor
   provenance, and idempotent replay.
5. Request and response contracts do not change, so generated clients and the
   existing correction editor require no update.
6. Module tests assert the exact endpoint metadata and the absence of assurance
   on routine mutations. Host architecture tests bind production composition to
   the module option.

## Delivery

1. Add `ReservationsApiSecurityOptions` with one optional
   `CorrectionExecutionAssurance` requirement.
2. Register and resolve the options in `ReservationsModule`, attaching GMA's
   assurance metadata only when configured.
3. Configure the option in `BunkFy.Host.Api` from the existing
   privileged-operation assurance requirement.
4. Extend Reservations API security tests and host-composition guards with
   exact endpoint and ownership assertions.
5. Update Reservations development documentation and run focused tests before
   one consolidated non-Docker slice gate.

## Deferred

- adding recent-authentication requirements to routine reservation lifecycle
  operations without product evidence that their current permission and audit
  controls are insufficient;
- changing Admin API or CLI assurance semantics;
- introducing a Reservations-specific authentication mechanism; and
- generalizing any BunkFy policy into GMA.

## Completion Criteria

- configured public-host correction requests carry GMA assurance metadata;
- unconfigured module composition remains valid;
- correction permission, response contract, and owner-local approval gate are
  unchanged;
- routine reservation mutations have no new assurance metadata;
- focused and consolidated non-Docker tests pass; and
- every GMA submodule remains clean.

## Verification

- focused Reservations tests passed: 263/263;
- focused architecture tests passed: 102/102;
- `eng/verify.ps1 -SkipRestore` passed with a zero-warning solution build,
  synchronized solution and source-package checks, clean migration drift, and
  every non-Docker suite green, including Operations Notifications 100/100 and
  Integration 60/60;
- `pnpm contracts:check` confirmed the OpenAPI snapshot and generated web API
  contracts are current; and
- every recursive GMA submodule remained clean and unchanged.

Docker integration tests were not repeated because this slice changes endpoint
metadata and public-host composition only; it does not change persistence,
messaging, migrations, or runtime data flow.
