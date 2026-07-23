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

This repository contains the backend foundation plus Properties, Inventory, Reservations, Ingestion/adapters, Guest Records, Staff Profiles, and the Data Rights case-lifecycle foundation. Public API, admin API/CLI, worker, service defaults, persisted access control, PostgreSQL migrations, architecture/integration tests, and copied examples are composed.

