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
  BunkFy.Host.AppHost/
  BunkFy.Host.Api/
  BunkFy.Host.AdminApi/
  BunkFy.Host.AdminCli/
  BunkFy.Host.Worker/
  BunkFy.Host.ServiceDefaults/
  Modules/
    Catalog/        # copied GMA skeleton example
    Ordering/       # copied GMA skeleton example
    TaskSamples/    # copied GMA skeleton example
    Properties/
    Inventory/
    Reservations/
    Guests/
    Billing/
    Housekeeping/
tests/
  Architecture.Tests/
  Integration.Tests/
  BunkFy.Host.ServiceDefaults.Tests/
```

The backend now carries the skeleton-style host set plus copied example modules so product work has concrete patterns for public API, admin API, admin CLI, worker, persistence, migrations, contracts, and module tests. BunkFy-specific PMS modules should follow the same layering but replace the example domain language with hostel/property-management concepts.
