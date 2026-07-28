# Adapter Ingress Trust Boundary Task

Status: implementation and local verification complete; exact-candidate CI pending
Date: 2026-07-28

## Goal

Close SP-005 without moving hospitality policy into GMA or pretending that a
node-local HTTP limiter is a multi-instance quota.

Third-party HTTP adapters may submit only approved canonical facts through a
customer-authorised connection. Every accepted fact must be attributable to an
immutable connection, credential, adapter protocol version, operation, and
receipt. Production ingress stays disabled until the distributed quota provider
and product controls are composed.

## Ownership

### BunkFy

BunkFy owns:

- allowed record families and canonical schemas;
- rejection of prohibited hospitality data and unsupported content;
- connection, credential, source-system, and customer-owner provenance;
- per-credential and per-tenant quota policy;
- customer tenant suspension and the operator all-ingress switch;
- receipt, quarantine, replay, retention, operator drill behavior, and bounded
  product admission metrics.

### GMA

GMA owns:

- a provider-neutral multi-partition rate-limit contract;
- atomic distributed acquisition semantics;
- the Redis provider and production composition validation;
- explicit acquired, rejected, and provider-unavailable outcomes.

GMA must not learn about adapters, reservations, providers, properties,
credentials, or BunkFy quota values.

### Private provider repositories

Private provider repositories own:

- scraping, OTA, mailbox, and partner-specific implementations;
- provider credentials, sessions, legal approval, and commercial certification;
- provider-specific anomaly interpretation.

The public SDK remains neutral. Public sample adapters are not production
provider approval.

## Current Evidence

Already implemented:

- connection-scoped, expiring, revocable, one-time-reveal credentials;
- fixed-time token verification and tenant/connection binding;
- replay and operation idempotency;
- payload and batch bounds plus immutable SHA-256 receipt hashes;
- strict `reservation.v1` JSON normalization with unknown members rejected;
- country-policy admission before a receipt is created;
- bounded raw-evidence retention and reprocessing quarantine;
- connection disable and remote lease fencing;
- remote lease descriptor matching for adapter, protocol, and configuration
  schema versions;
- node-local IP rate limiting at the HTTP edge.

Gaps confirmed by the source audit at task start:

- public HTTP ingress stores an unsupported record before downstream
  normalization rejects it;
- the public request envelope accepts unmapped JSON members;
- push receipts do not identify the authenticating credential;
- credentials do not snapshot adapter type, protocol/configuration schema
  versions, or source system;
- receipts do not retain an immutable adapter-version provenance snapshot;
- there is no distributed per-credential/per-tenant quota;
- there is no tenant-wide customer suspension or runtime all-ingress operator
  control;
- production configuration does not currently prove that third-party ingress
  is deliberately enabled with a distributed provider.

Implemented by this task:

- production ingress is disabled by default and startup rejects an enabled
  production surface without a distributed provider;
- the public envelope and canonical `reservation.v1` body reject unknown,
  malformed, prohibited, or unsupported data before receipt/payload storage;
- credentials and receipts retain immutable credential, adapter protocol,
  configuration schema, source-system, and customer-owner provenance;
- one GMA atomic request applies credential-minute, credential-hour,
  tenant-minute, and tenant-hour limits through Redis;
- tenant and global controls are durable, optimistic-concurrency protected,
  actor/reason attributed, separately permissioned, and fail closed;
- BunkFy emits only bounded operation/outcome admission dimensions.

## Invariants

1. Authentication never selects a tenant. The resolved tenant, route
   connection, credential, and connection scope must all agree.
2. A disabled connection or revoked/expired credential cannot authenticate.
3. Public HTTP ingress validates canonical schema and prohibited content before
   raw bytes are persisted.
4. Unknown record versions, content types, encodings, and JSON members fail
   closed.
5. One distributed decision atomically evaluates every configured partition.
   A request cannot consume one quota while failing another.
6. Provider failure is deny-by-default for third-party ingress.
7. Credential, tenant, and global controls are independent.
8. Quota keys and metrics never contain tenant ids, credential ids, external
   record ids, guest data, or payload hashes as metric tags.
9. Internal trusted acquisition and retained-evidence reprocessing keep their
   existing product-owned paths. Public HTTP admission is not a generic file or
   evidence-upload endpoint.
10. Existing receipts remain readable after schema migration. New provenance
    fields may be nullable only for pre-migration or non-HTTP trusted records.

## Slice 1 - Fail-Closed Public Ingress

Implement in BunkFy:

1. Add explicit adapter-ingress production options with a disabled production
   default and enabled development/test configuration.
2. Fail production startup when ingress is enabled without a registered
   distributed quota provider.
3. Reject public HTTP observations before command dispatch unless they are an
   approved record type and content type and pass the canonical validator.
4. Disallow unmapped request-envelope and observation fields.
5. Make disabled connections fail authentication instead of recording
   successful credential telemetry and returning a later shaped rejection.
6. Snapshot adapter type, protocol/configuration schema versions, source system,
   credential id, and customer owner on credentials and accepted receipts.
7. Add migrations and preserve old receipt readability.

This slice may ship while production ingress remains disabled.

## Slice 2 - Distributed Quotas And Kill Switches

Implement in GMA:

1. Add a small provider-neutral contract that accepts one request containing
   multiple bounded fixed-window partitions.
2. Add an in-memory provider for development and deterministic unit tests.
3. Add a Redis provider that checks and increments every partition atomically.
4. Expose retry-after and stable rejection/provider-unavailable outcomes.
5. Add a composition marker and fail-closed production validation.

Compose in BunkFy:

1. Apply credential and tenant partitions to every authenticated HTTP ingress
   operation, including remote lease control calls.
2. Persist a tenant-wide customer ingress state with optimistic concurrency,
   actor, reason code, and timestamps.
3. Add customer-authorized suspend/resume controls.
4. Add an operator all-ingress switch through Admin API and CLI, with
   confirmation and payload-free audit.
5. Emit bounded counters for allowed, quota-rejected, policy-rejected, and
   provider-unavailable outcomes.
6. Publish the deployment alert contract for aggregated outcomes; deployment
   alert routing remains environment-owned and must not place customer or
   record identifiers in labels or notifications.

## Adversarial Evidence

Focused tests must prove:

- a credential from another tenant or connection is indistinguishable from an
  invalid credential;
- expired, revoked, disabled-connection, suspended-tenant, and global-stop
  requests are denied;
- replay returns the original receipt and does not consume a second accepted
  record;
- oversized batches/bodies, invalid hashes, malformed UTF-8, unknown envelope
  members, unknown record versions, and unsupported content types are rejected
  before persistence;
- card, document, provider secret, and provider session fields cannot pass the
  canonical schema;
- concurrent multi-node quota exhaustion does not exceed either credential or
  tenant permits;
- Redis/provider loss denies third-party ingress without taking ordinary staff
  APIs down;
- one credential, one tenant, and all ingress can each be stopped while the
  other levels remain operational;
- an accepted canonical field can be traced through connection, credential,
  adapter version, operation, receipt, and content hash.

## Operational Drill

The Docker-backed
`IngestionOperationsIntegrationTests.Management_api_scopes_connection_lifecycle_and_operational_reads`
drill must:

1. accepts one canonical observation;
2. revokes only its credential and proves another tenant still works;
3. suspends one tenant and proves another tenant still works;
4. activates the operator switch and proves all third-party ingress stops;
5. restores controls in reverse order;
6. queries receipt provenance without reading raw guest payload.

## Verification

- GMA framework build and its 1,035 non-Docker tests pass.
- GMA Skeleton non-Docker verification passes, including the architecture
  guards, and its Docker verification passes 20 tests with no failures.
- BunkFy non-Docker verification passes, including a zero-warning build,
  migration drift, module tests, and 67 architecture tests.
- Focused Ingestion verification passes 245 tests, and the integration test
  assembly builds with zero warnings.
- The complete BunkFy Docker gate passed 63 of 64 tests. Its sole failure
  exposed the new tenant-control endpoint filter order; after correction, that
  exact Docker-backed operations and stop/recovery drill passes in isolation.
- Solution graph synchronization and `git diff --check` pass.
- Exact published-commit GitHub Actions evidence remains pending.

## Non-Goals

- provider-specific adapter implementations or approval;
- arbitrary raw files, documents, email, HTML, OCR, or scraping payloads over
  public HTTP ingress;
- dynamic adapter or parser code loading;
- field-level reservation merge policy;
- a GMA adapter-management module;
- customer-specific quota values in the public repository;
- claiming hosted production readiness before the private deployment and
  operational evidence exists.

## Completion

SP-005 is complete only when both slices, the adversarial suite, the stop drill,
and exact release-candidate CI evidence pass. Until then, production
third-party ingress remains disabled.
