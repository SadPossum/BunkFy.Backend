# Pre-Module Readiness

Status: completed historical checkpoint
Date: 2026-07-09

This note records the preparation checkpoint that preceded Properties, the first BunkFy product module. Properties, Inventory, and the first Reservations slice are now complete.

## Ready Now

- Backend host defaults use `PostgreSql` as the persistence provider.
- Backend and root Aspire AppHosts include PostgreSQL in the normal graph and make SQL Server opt-in through `AppHost:SqlServer:Enabled`.
- MinIO remains the default file-storage path for local development.
- NATS, Redis, Admin API, and Worker surfaces stay explicit composition choices.
- Architecture guardrails cover solution wiring, project naming, package centralization, docs links/indexing, local ignore rules, module boundaries, and host composition.
- A manual Docker-backed CI lane exists for integration tests that need containers.
- Product planning has a glossary, module boundary map, development guidelines, and one-new-module-at-a-time rule.

## Deferred At The Checkpoint

These items were intentionally deferred before the first product module:

- build Inventory before the Properties topology and policy contracts are stable;
- remove SQL Server packages or copied example SQL Server migrations;
- decide permanent PostgreSQL-only versus dual-provider product migrations;
- add heavy reflection/catalog guardrails for BunkFy product module metadata;
- design Staff/Access, JWT behavior, or custom policy pipelines beyond the generic GMA-first rule;
- scaffold Data Providers/Ingestion or adapter runners;
- create demo milestones or demo-specific workflows;
- design production deployment, backup/restore, or self-hosting packaging in detail.

## First Module Entry Criteria (Met)

Properties started after:

- PostgreSQL-first runtime defaults are green in focused tests;
- the product-domain glossary is treated as the naming source;
- the module starts as one active new product module;
- any changes to existing modules are required by the active slice, not parallel product expansion.
