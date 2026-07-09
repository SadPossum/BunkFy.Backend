# Setup

## Prerequisites

- Windows PowerShell.
- .NET 10 SDK.
- Git.
- GMA source submodules initialized under `gma/`, or the root BunkFy superproject bootstrap.

## First Run Inside Root Superproject

From the root `BunkFy` checkout:

```powershell
.\eng\bootstrap.ps1
.\eng\verify.ps1
```

The root bootstrap initializes this backend repository recursively, including backend-owned GMA submodules, then refreshes source-root files.

## Standalone Backend Run

For standalone backend work, initialize this repository's GMA submodules, then run:

```powershell
.\eng\gma-update.ps1 -Init
.\eng\gma-bootstrap.ps1 -Force
.\eng\verify.ps1
```

## Current Scope

This repository currently contains a skeleton-style backend foundation: public API, admin API, admin CLI, worker, service defaults, architecture/integration tests, and copied example modules. Product PMS modules and real hostel workflows are the next implementation layer.

