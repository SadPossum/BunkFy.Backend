# Local Development

Current backend validation:

```powershell
.\eng\verify.ps1
```

The repository includes copied GMA skeleton examples under `src/Modules/Catalog`, `src/Modules/Ordering`, and `src/Modules/TaskSamples`. Use them as working references for module shape, contracts, persistence, migrations, front-door wiring, and tests. Future backend development should add product modules in focused slices under `src/Modules`.

The current product-domain map lives in [Product Domain Map](../planning/product-domain-map.md), development flow notes live in [Development Guidelines](../planning/development-guidelines.md), and the current pre-module status lives in [Pre-Module Readiness](../planning/pre-module-readiness.md). Expected early modules include:

- Properties.
- Inventory.
- Reservations.
- Guest records, managed only by operators/staff.
- Billing.
- Housekeeping.

Keep reusable framework changes in the appropriate nested GMA repository and update the backend submodule pointer deliberately after validation.

For backend-only composition, run:

```powershell
.\eng\run-aspire.ps1
```

## Backend Stack Development Note

The baseline backend stack is intentionally already wired before product modules start:

- `BunkFy.Host.Api` composes Tenancy, tenant-scoped Auth, Files, Notifications, MinIO file storage, NATS/JetStream messaging, caching hooks, OpenAPI, and service defaults.
- `BunkFy.Host.AdminApi` and `BunkFy.Host.AdminCli` compose Administration, tenant-scoped Auth admin surfaces, TaskRuntime controls, and the copied Catalog admin example.
- `BunkFy.Host.Worker` is present from the start. Enabling `AppHost:Worker:Enabled=true` runs the worker with NATS publishing, Auth support, TaskRuntime persistence, and the task worker loop.
- PostgreSQL is the default persistence provider and normal Aspire database. SQL Server remains opt-in through `AppHost:SqlServer:Enabled=true` while copied examples and dual-provider paths are still retained.
- MinIO is the default storage path for local and development environments. Local disk storage remains only as an adapter/test reference.
- BunkFy is management-only: operators and staff authenticate; guests are PMS records and do not access the system directly.

Keep first product work focused on real BunkFy modules, starting with Properties/Inventory-style setup and only adding task handlers when a concrete module needs background work.

