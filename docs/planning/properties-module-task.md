# Properties Module Task

Status: complete
Date: 2026-07-09

Build `Properties` as the first real BunkFy product module. Keep the slice focused on the physical accommodation model that later modules can project from.

The foundation, policy integration, topology lifecycle, producer contracts, and product-specific Docker authorization verification described here are complete. Later non-blocking product-model follow-ups remain in [Properties Follow-Up Notes](properties-follow-up-notes.md).

## Goal

Create a tenant-scoped Properties module that owns:

- properties/hostels;
- rooms inside a property;
- beds inside a room;
- basic setup lifecycle/status;
- admin CLI and admin API setup surfaces;
- PostgreSQL migrations and module-owned persistence;
- outbox-backed integration events for future Inventory projections.

This module should give Inventory a stable source of property, room, and bed facts without implementing availability or booking behavior.

## Non-Goals

Do not implement:

- inventory holds, allocations, or no-overbooking rules;
- availability calendars;
- out-of-service maintenance closures;
- reservations;
- guest records;
- pricing, billing, or payments;
- provider/import adapter state;
- housekeeping or maintenance workflows;
- staff profiles or custom policy engines.

## Module Shape

Use the GMA module layout:

- `BunkFy.Modules.Properties.Contracts`
- `BunkFy.Modules.Properties.Domain`
- `BunkFy.Modules.Properties.Application`
- `BunkFy.Modules.Properties.Persistence`
- `BunkFy.Modules.Properties.Persistence.PostgreSqlMigrations`
- `BunkFy.Modules.Properties.Api`
- `BunkFy.Modules.Properties.Admin.Contracts`
- `BunkFy.Modules.Properties.AdminCli`
- `BunkFy.Modules.Properties.AdminApi`

Use PostgreSQL-only migrations for this first product module. Keep runtime domain/application code database-provider agnostic.

Use outbox support from the start so future Inventory projections can consume Properties facts without direct database reads.

## Domain Model

First slice:

- `Property`
  - tenant id
  - property id
  - name
  - code/slug
  - timezone id
  - status
- `Room`
  - tenant id
  - property id
  - room id
  - name/number
  - optional building label
  - optional floor label
  - status
- `Bed`
  - tenant id
  - property id
  - room id
  - bed id
  - label/name
  - status

Do not create `Building` or `Floor` aggregates yet. Use labels until product rules require richer structure.

## Contract Rules

Public contract enums reserve `Unknown = 0` and reject unknown values before business decisions.

Expected integration events:

- property created;
- property updated;
- room created;
- room updated;
- room retired;
- bed added;
- bed updated;
- bed retired.

Events must include tenant/property scope and stable BunkFy-owned ids. Do not include external provider ids.

## Access And Admin

Declare operation-specific permission codes. Start small:

- `properties.read`
- `properties.properties.manage`
- `properties.rooms.manage`
- `properties.beds.manage`

Admin CLI and Admin API should call the same application commands/queries. Keep setup/bootstrap behavior in admin surfaces and keep business rules out of endpoints and CLI mapping.

## Implementation Sequence

1. Scaffold the module with persistence, PostgreSQL migrations, outbox, admin CLI, and admin API.
2. Replace scaffold stubs with Properties contracts, metadata, permissions, commands, queries, validators, domain model, repositories, and mappers.
3. Compose the module explicitly into the public API, admin API, and admin CLI hosts.
4. Add EF configuration and PostgreSQL migration.
5. Add focused unit tests for value objects, aggregate invariants, and contract metadata.
6. Add integration coverage for persistence/migrations and representative API/admin flows when practical.
7. Update architecture guardrails so the first product module is part of boundary and host-composition checks.

## Acceptance Checks

Before calling this slice done:

- `dotnet build BunkFy.slnx -m:1` passes;
- focused module tests pass;
- architecture tests pass;
- generated migrations target the `properties` schema;
- no module references another module's non-contract project;
- Properties is documented as the active first product module;
- Inventory remains a follow-up module, not hidden inside Properties.
