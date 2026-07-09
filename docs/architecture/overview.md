# Backend Architecture Overview

BunkFy Backend follows the GMA source-first modular-monolith direction.

Initial architecture rules:

- App-owned product code belongs in this repository.
- GMA framework and reusable modules are editable source dependencies.
- Product modules should be explicit host composition choices.
- Cross-module communication should use contracts and integration events.
- Modules own their persistence. Cross-module data is duplicated deliberately through local projections and projection rebuilds, not shared tables or cross-module foreign keys.
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
    GuestRecords/
    Billing/
    Housekeeping/
tests/
  Architecture.Tests/
  Integration.Tests/
  BunkFy.Host.ServiceDefaults.Tests/
```

The backend now carries the skeleton-style host set plus copied example modules so product work has concrete patterns for public API, admin API, admin CLI, worker, persistence, migrations, contracts, and module tests. BunkFy-specific PMS modules should follow the same layering but replace the example domain language with hostel/property-management concepts.

BunkFy is a staff/operator management system. Guest records may exist as PMS data for reservations, billing, and housekeeping, but guests do not authenticate into this backend or use a guest-facing API surface.

File management starts on MinIO rather than local disk. Local and development hosts should use the S3-compatible object-storage path from the beginning, with `Gma.Framework.FileManagement` staying as the app-facing storage contract.
