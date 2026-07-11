# BunkFy Backend Development Guidelines

Status: working planning note
Date: 2026-07-09

These guidelines keep early backend work focused while the product model is still forming.

## Module Focus

Develop no more than one new product module at the same time.

It is okay for the current slice to touch existing modules, shared contracts, SharedKernel, host composition, persistence adapters, tests, GMA extension points, and documentation when that work is needed to finish the active module cleanly.

Avoid opening a second new product domain while the active module still lacks its core model, persistence path, public/admin surface, tests, and boundary documentation.

Preserve the GMA skeleton goal: modules should stay separate, optional, replaceable, and easy to reason about. Prefer explicit, boring code over framework cleverness.

## Current Product Flow

Let the product evolve naturally through focused backend slices. Do not force a public demo milestone before the system has enough useful operational depth, likely after Reservations and possibly after Staff/Access or Data Providers/Ingestion.

Early modules may still include local seed data, admin CLI helpers, and internal verification endpoints when they make development easier.

## Where Things Go

Use the GMA skeleton project shape for BunkFy product modules:

- public request/response DTOs and integration event contracts go in `<Module>.Contracts`;
- aggregates, entities, value objects, and domain events go in `<Module>.Domain`;
- commands, queries, handlers, and validators go in `<Module>.Application`;
- adapter interfaces needed by the domain go in `<Module>.Domain.Services`;
- adapter implementations go in `<Module>.Infrastructure`;
- DbContext, EF configuration, and runtime persistence registration go in `<Module>.Persistence`;
- provider-specific migrations go in `<Module>.Persistence.PostgreSqlMigrations` and any other retained provider migration project;
- public HTTP routes go in `<Module>.Api`;
- typed admin permission constants go in `<Module>.Admin.Contracts`;
- admin CLI commands go in `<Module>.AdminCli`;
- admin HTTP routes go in `<Module>.AdminApi`;
- boundary tests go in `tests/Architecture.Tests`.

Do not create folders or projects before they help navigation or preserve a real boundary.

## Adding A Feature

For normal module work:

1. Add or update contract DTOs only if the public API/event contract changes.
2. Add domain behavior to an aggregate, entity, value object, or domain service.
3. Add a command/query and handler.
4. Add a validator for request-shape checks.
5. Add persistence changes and migrations if needed.
6. Map or update the public/admin/CLI front door.
7. Add unit tests.
8. Add integration tests when persistence, tenancy, authorization, messaging, files, notifications, or task behavior changes.
9. Update module docs and glossary terms.

## Adding A Module

Use `eng/new-module.ps1` as the starting point when possible, then manually decide:

- whether the module needs persistence;
- whether it publishes integration events and needs an outbox;
- whether it consumes integration events and needs an inbox;
- whether it needs admin CLI or admin API front doors;
- whether it needs cache-aside reads;
- whether it is tenant-scoped and property-scoped;
- whether the public API host should register it by default;
- whether worker/task registration is needed now or later.

Optional modules must stay explicit host decisions. The scaffolder creates shape; it does not invent domain behavior, aggregate models, commands, queries, or host registration choices.

## Language And Boundaries

Use the product domain map as the source of current domain language. When a new term appears in code or contracts, add it to the glossary or deliberately map it to an existing term.

Keep module ownership explicit:

- a module owns its own persistence;
- cross-module data is copied through local projections;
- provider/import adapters update product modules through commands/events;
- product modules stay agnostic to database provider and external data source details.

## Dependency Rules

Do:

- depend on `Gma.Framework.*` abstractions;
- depend on the module's own projects;
- depend on another module's `.Contracts` project only when truly needed.

Do not:

- reference another module's Domain, Application, Infrastructure, Persistence, Api, AdminApi, or AdminCli project;
- expose EF entities through contracts;
- make application projects depend on `IHostApplicationBuilder` or `Microsoft.Extensions.Hosting`;
- publish directly to NATS from domain or application code;
- reference SignalR, SSE, NATS, Redis, Prometheus, OpenTelemetry exporters, Serilog sinks, or cache-backend packages from module projects;
- call `Guid.NewGuid()`, `DateTimeOffset.UtcNow`, or `DateTime.UtcNow` from feature-module code; use GMA runtime abstractions;
- put business rules in endpoints, admin routes, or CLI mapping code.

## Request And Error Rules

Use GMA `Result`/`Result<T>` for expected failures. Error codes are stable public contracts and should use dotted identifiers such as `Inventory.HoldConflict` or `Reservations.ReservationNotFound`.

Keep detailed diagnostics in logs, not in `Error.Message`. Domain invariants return domain errors; application validation returns application errors; front doors map expected errors to HTTP/admin/CLI shape at the edge.

Use GMA CQRS validators for request-shape checks. Keep deeper business invariants in aggregates and domain services. Do not add a parallel validation framework by default.

Normalize paging through GMA pagination helpers rather than copying local `Math.Max`/`Math.Clamp` rules into handlers or repositories.

Public contract enums and provider/configuration enums should reserve `Unknown = 0`, own stable wire names in the contract package, and reject unsupported input explicitly. Unknown input must not silently become a meaningful domain value.

When domain text or decimal values have persistence limits, expose named constants and validate before persistence. EF mappings should reference those constants instead of duplicating raw limits.

Guid-backed identity value objects should reject `Guid.Empty`.

## Tenancy And Access

Before writing tenant-scoped code, answer:

- Is this endpoint tenant-scoped?
- Is it also property-scoped?
- Does the aggregate store `TenantId` and/or `PropertyId`?
- Are unique indexes scoped correctly?
- Do queries preserve tenant/property filters?
- Are integration events tenant-scoped?
- Do cache keys include every access-scope dimension, or should that read path avoid caching?

Tenant-owned models should make ownership visible in code and tests. Do not hide tenancy behind shadow EF properties or host-side reflection.

Use GMA AccessControl for generic permission/scope decisions and persisted grants. Use GMA Administration for audited admin API/CLI execution through its explicit AccessControl bridge. Put product visibility rules in the owning BunkFy module, preferably in the domain or typed access scopes consumed by persistence. Missing scopes should fail deny-by-default.

Code requires permissions, not hard-coded roles. Do not put permissions in JWTs as the source of truth, and do not call access authorization once per row. Read granted scopes once and translate them into the owning module's SQL query shape.

## Persistence And Messaging

Each module owns its schema and provider-specific migrations. Keep EF design-time dependencies in migration projects, not runtime persistence projects.

Rules:

- no cross-module foreign keys;
- no `EnsureCreated` in integration tests;
- no automatic migration application at API startup;
- tenant-scoped read indexes should start with tenant id when queries filter by tenant;
- module outbox/inbox tables belong to the module that publishes or consumes the event;
- consumer handlers must update local module state or projections idempotently;
- NATS stays behind infrastructure.

Do not inject a bare `IOutboxWriter` into application code. Multiple modules can be composed in one host, so writer selection must be module-qualified.

## Notifications, Tasks, And Admin

Use notifications only for user/staff delivery, not module-to-module communication. Durable business facts belong in domain events, integration events, outbox, inbox, and projections.

Task payloads that are module metadata or externally enqueueable belong in the owning module `.Contracts` project. Keep task handlers and their registration extension in the owning module application project. Modules shared by API, admin, and worker hosts expose a separate `Add<Module>TaskHandlers()` extension, and only task-execution hosts call it. Use tenant-scoped task metadata only when the worker host actually runs tenant-scoped work.

Keep admin CLI and admin API optional. Route admin commands through the same application commands/queries as other use cases. Keep first-owner/bootstrap style operations in admin CLI unless a separate decision approves an HTTP bootstrap path.

Never log or audit passwords, tokens, token hashes, refresh tokens, or raw secrets.

## Guardrail Coverage

BunkFy owns composition and product guardrails:

- `DeveloperExperienceGuardTests` checks solution wiring, docs links, project conventions, package centralization, test project metadata, and local workspace ignore rules.
- `ModuleBoundaryTests` checks product module dependency boundaries.
- `HostCompositionGuardTests` checks BunkFy host composition choices.
- Integration tests check composed Auth, Administration, Files, Notifications, TaskRuntime, Worker, persistence, messaging, and storage behavior in the BunkFy host shape.

GMA framework and reusable module repositories already cover reusable primitives such as `Result`, paging, tenant id normalization, module composition, access-control primitives, admin value objects/executors, file-management option validation, messaging envelopes/outbox/inbox, projection rebuilds, task runtime contracts, notification contracts, and observability safety.

When the first real BunkFy product module becomes compiled code, add BunkFy-specific reflection/catalog guardrails for:

- module metadata descriptors matching contract constants;
- permission constants matching descriptor metadata;
- integration event, notification, and task metadata;
- command validators on non-list commands/queries;
- transactional commands for state-changing handlers;
- query handlers avoiding side-effect infrastructure;
- admin API routes using the shared executor for tenant enforcement;
- public contract APIs not exposing another module's non-contract types;
- module registration idempotency.
