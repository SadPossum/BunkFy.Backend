# Properties Sensitive-Control Assurance Task

Status: completed
Date: 2026-08-11

## Goal

Require recent authentication for the public Properties operations that enable
or rebind personal-data processing and terminally retire a property, without
delaying protective suspension or changing Properties lifecycle semantics.

## Confirmed Boundary

- Properties owns processing-policy activation, property retirement,
  confirmation, actor attribution, optimistic versions, idempotent receipts,
  permissions, and public request contracts.
- GMA Security owns the reusable authentication-assurance requirement,
  evaluation, challenge, and endpoint metadata.
- The BunkFy public host owns deployment policy and maps its configured
  privileged-operation requirement to each protected Properties operation.
- The browser owns understandable recent-authentication recovery and retries
  the exact failed operation after successful step-up.
- Inventory remains the authority for reservation-safe room and bed retirement.
  The direct Properties retirement commands remain fail-closed.
- No GMA framework or reusable module change is required.

## Invariants

1. Processing activation or policy rebinding requires the existing property
   management permission, property scope, explicit confirmation, resolved
   actor, policy acknowledgements, optimistic version, and configured
   activation assurance.
2. Property retirement requires the existing property management permission,
   property scope, explicit confirmation, resolved actor, optimistic version,
   active-room precondition, and configured retirement assurance.
3. Confirmation remains enforced at the application command boundary before
   aggregate acquisition, journal access, policy evaluation, topology reads,
   mutation, or event publication.
4. Confirmation and authentication assurance are admission facts rather than
   mutation identity. They do not enter the existing lifecycle fingerprint, so
   a browser can retry the same operation id after step-up.
5. Processing suspension remains available without step-up so an operator can
   stop new property-scoped personal-data processing immediately.
6. Ordinary property create/update/read operations and room/bed setup do not
   gain assurance in this slice. Inventory-owned topology retirement is audited
   separately.
7. Browser recovery preserves the mutation variables and operation id, retries
   through the same mutation path, and rotates intent only under the existing
   lifecycle-attempt rules.
8. Assurance options remain optional for embedding hosts. BunkFy's public host
   explicitly configures both protected operation classes.

## Delivery

1. Add Properties API assurance options and register them with module
   composition.
2. Attach assurance metadata only to processing activation and property
   retirement, then configure both options in the public host.
3. Add route-metadata and host-composition guards proving protected and
   intentionally unprotected boundaries.
4. Reuse the browser's recent-authentication prompt for exact activation and
   property-retirement retries while leaving ordinary errors visible.
5. Update concise module documentation and run focused checks followed by one
   consolidated non-Docker backend and web gate.

## Verification Cadence

No Docker scenario is required because this slice changes no schema,
transaction boundary, broker behavior, policy registry, or persistence
contract. Focused Properties, architecture, and browser tests may run while
editing; the complete non-Docker backend and web gates run once when the slice
is coherent.

## Completed Evidence

- Focused backend verification passed: all 176 Properties tests, all 32 host
  composition guards, and a public-host build with zero warnings and zero
  errors.
- `eng/verify.ps1 -SkipRestore` passed: solution synchronization, source/package
  guards, a zero-warning build, every migration-drift check, and all non-Docker
  framework, extension, module, architecture, migration-host,
  service-default, and integration test projects. The affected Properties,
  architecture, and host integration suites passed 176, 102, and 60 tests.
- Focused browser verification passed: the property-lifecycle-attempt and
  authentication-assurance files passed four tests, followed by TypeScript
  checking and targeted linting.
- `pnpm verify` passed: TypeScript checking, linting, 52 test files with 264
  tests, and the production build.
- `pnpm contracts:check` confirmed that the public OpenAPI snapshot and
  generated TypeScript contracts remain current.
- Docker verification was intentionally omitted under the cadence above. GMA
  framework and reusable-module worktrees remained unchanged.

## Deferred

- Inventory-owned room and bed retirement assurance, if justified by that
  domain's separate frequency, containment, and recovery audit.
- External-provider or MFA step-up adapters for accounts without a password;
  those belong to the reusable authentication product flow.
- Stronger per-operation assurance can be selected later through host policy
  without changing Properties contracts.
