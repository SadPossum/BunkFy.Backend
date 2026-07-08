# Backend Architecture Overview

BunkFy Backend follows the GMA source-first modular-monolith direction.

Initial architecture rules:

- App-owned product code belongs in this repository.
- GMA framework and reusable modules are editable source dependencies.
- Product modules should be explicit host composition choices.
- Cross-module communication should use contracts and integration events.
- Keep provider choices, messaging, caching, observability, and tenancy as composition concerns rather than domain dependencies.

## Planned Source Layout

```text
src/
  BunkFy.Host.Api/
  BunkFy.Host.AdminApi/
  BunkFy.Host.AdminCli/
  BunkFy.Host.Worker/
  BunkFy.SharedKernel/
  Modules/
    Properties/
    Inventory/
    Reservations/
    Guests/
    Billing/
    Housekeeping/
tests/
  Architecture.Tests/
  Integration.Tests/
```

Only the initial API host and shared kernel are present for the foundation milestone. Additional hosts and product modules should be added when the product work requires them.
