# BunkFy Backend

Backend foundation repository for BunkFy, an open-source hostel property management system.

This repository is a source-first GMA backend foundation:

- Host front doors live under `src/BunkFy.Host.*`.
- App-owned feature modules live under `src/Modules`.
- Reusable GMA framework and modules are mounted as source submodules under `gma/`.
- When this repo is mounted inside the root `BunkFy` superproject, the root bootstrap initializes backend submodules recursively and refreshes backend-local source-root files.

Selected GMA modules for the initial shell:

- `administration`
- `auth`
- `files`
- `notifications`
- `task-runtime`
- `tenancy`

Public API modules composed by `src/BunkFy.Host.Api`:

- `auth`
- `files`
- `notifications`
- `tenancy`
- `Properties`
- `Inventory`
- `Reservations`
- `Guests`
- `Staff`
- `Ingestion`

Admin API/CLI compose reusable administration, auth, and task-runtime modules plus the BunkFy product administration surfaces. The worker keeps product background module groups opt-in.

The Aspire host under `src/BunkFy.Host.AppHost` can run the backend stack standalone. It and the root full-stack AppHost use `src/Shared/BunkFy.AppHost.Composition`, keeping infrastructure, optional worker/admin resources, module switches, and MinIO wiring aligned.

## Documentation

Start with [docs/README.md](docs/README.md).

## Local Validation

From the root BunkFy superproject, use:

```powershell
.\eng\bootstrap.ps1
.\eng\verify.ps1
```

For standalone backend work, initialize the nested GMA submodules and run:

```powershell
.\eng\gma-update.ps1 -Init
.\eng\gma-bootstrap.ps1 -Force
.\eng\verify.ps1
```

## Development Notes

- PostgreSQL is the default product provider. Provider-specific migrations remain isolated from module domain and application code.
- MinIO is the default file-management provider so local and demo environments use the same S3-compatible storage shape.
- NATS/JetStream, Redis caching hooks, OpenAPI, Prometheus-compatible metrics, and the worker host are wired as composition concerns.
- TaskRuntime admin API/CLI controls are baseline admin surfaces. The worker enables TaskRuntime when `AppHost:Worker:Enabled=true`; product task handlers are added by BunkFy modules.
- Tenant-aware Auth is enabled because BunkFy is expected to support real multi-property/operator deployments.
- Browser clients use the `/api/auth/browser/*` session surface: refresh context is held in path-scoped HttpOnly cookies and responses expose only the short-lived access token. Bearer-oriented Auth endpoints remain available for non-browser clients.
- `/api/access/permissions/evaluate` provides bounded, tenant-scoped effective-permission decisions for client UX. It does not replace authorization on module endpoints.
- `eng/export-openapi.ps1` exports or checks the public Swagger snapshot consumed by the root `eng/update-web-contracts.ps1` workflow.
- BunkFy is a management-only PMS surface for operators and staff. Guests do not get accounts or direct access to this system.
- Keep product-specific PMS behavior in this repository, not in GMA modules, unless the behavior is reusable across products.
