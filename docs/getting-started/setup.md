# Setup

## Prerequisites

- Windows PowerShell.
- .NET 10 SDK.
- Git.
- GMA source checkouts mounted under `gma/`, or the root BunkFy superproject bootstrap.

## First Run Inside Root Superproject

From the root `BunkFy` checkout:

```powershell
.\eng\bootstrap.ps1
.\eng\verify.ps1
```

The root bootstrap writes `apps/backend/Gma.SourceRoots.props`, pointing this backend repo at the root-owned GMA source submodules.

## Standalone Backend Run

For standalone backend work, mount GMA source repositories under this repository's `gma/` folder, then run:

```powershell
.\eng\gma-bootstrap.ps1 -Force
.\eng\verify.ps1
```

## Current Scope

This repository currently contains a backend foundation shell. Product PMS modules, migrations, admin hosts, worker hosts, and real API workflows are future implementation work.

