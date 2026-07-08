# BunkFy Backend

Backend foundation repository for BunkFy, an open-source hostel property management system.

This repository is a source-first GMA backend foundation:

- App-owned backend code belongs under `src/`.
- PMS feature modules will live under `src/Modules`.
- Reusable GMA framework and modules are mounted as source under `gma/` during standalone backend work.
- When this repo is mounted inside the root `BunkFy` superproject, the root bootstrap writes `Gma.SourceRoots.props` so backend project references resolve to the root-owned `gma/` checkout.

Selected GMA modules for the initial shell:

- `administration`
- `auth`
- `files`
- `notifications`
- `task-runtime`
- `tenancy`

Public API modules currently composed by `src/BunkFy.Host.Api`:

- `auth`
- `files`
- `notifications`
- `tenancy`

Admin CLI/API and worker-only surfaces stay explicit app choices and should be added when BunkFy has product behavior that needs them.

## Documentation

Start with [docs/README.md](docs/README.md).

## Local Validation

From the root BunkFy superproject, use:

```powershell
.\eng\bootstrap.ps1
.\eng\verify.ps1
```

For standalone backend work, mount or clone GMA source repositories under `gma/`, then run:

```powershell
.\eng\gma-bootstrap.ps1 -Force
.\eng\verify.ps1
```

## Development Notes

- Default persistence is PostgreSQL for the product shell.
- Tenant-aware Auth is enabled because BunkFy is expected to support real multi-property/operator deployments.
- Keep product-specific PMS behavior in this repository, not in GMA modules, unless the behavior is reusable across products.
