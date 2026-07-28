# Hosted Production Foundation Task

Status: dependency candidates published; local and exact-commit gates passed
Date: 2026-07-28

## Goal

Close the public portion of company-readiness control SP-007 without presenting
the preview Compose stack as production infrastructure.

Every Production host must declare its deployment shape and fail closed when
that declaration contradicts HTTP, key persistence, object storage, management
surface, or release identity configuration. The public repositories provide
the reusable guards and a secure deployment contract. Actual cloud topology,
secrets, keys, addresses, artifact digests, and operational evidence remain
deployment inputs.

## Ownership

### GMA Framework

GMA owns reusable host-safety primitives:

- configured ASP.NET Core Data Protection composition with a stable application
  name and mandatory persistent key ring in Production;
- an explicit distributed HTTP rate-limit mode built on the provider-neutral
  atomic rate-limit contract;
- fail-closed behavior when a distributed limiter is unavailable;
- trusted forwarded-header proxy IP and CIDR configuration;
- production-safe MinIO defaults that reject plaintext transport or automatic
  bucket creation unless the application explicitly accepts those exceptions.

GMA must not know BunkFy deployment profiles, host roles, image names, object
storage accounts, public origins, or cloud topology.

### GMA-Skeleton

The Skeleton owns generated-app guidance and composition examples:

- consume the framework Data Protection composition instead of generating a
  private copy;
- expose the HTTP rate-limit mode and trusted proxy network settings;
- document that multi-replica applications must compose a distributed provider;
- keep provider credentials, proxy values, key protection, and topology
  application-owned.

### Public BunkFy

BunkFy owns product deployment invariants:

- an explicit Production deployment profile and API topology;
- an exact source commit and, for container deployments, an OCI image digest;
- direct-HTTPS versus trusted-reverse-proxy behavior;
- distributed public-API rate limiting for multi-replica or hosted operation;
- durable Data Protection keys plus an explicit at-rest protection declaration;
- dedicated object-storage credentials and strict hosted storage transport;
- private-network enforcement for the Admin API;
- secure web-edge headers and a reference deployment contract;
- startup and static guards that prevent preview defaults from being promoted
  silently.

### Private Hosted Deployment

Private infrastructure must supply and evidence:

- TLS termination, DNS, ingress and exact trusted proxy addresses or networks;
- secrets, dedicated service identities, KMS/certificate/volume key protection,
  and persistent shared key storage;
- digest-pinned images, reviewed IaC, private networks, managed stores, backup
  and restore automation, observability destinations, and alert ownership.

This task cannot prove those controls without a real environment. Missing
private inputs must keep Production startup or launch disabled.

## Audited Baseline

Already present:

- GMA rejects unrestricted Production host filtering and forwarded headers
  without a known proxy unless unknown proxies are explicitly allowed;
- GMA provides HTTPS redirection, HSTS, API security headers, request timeouts,
  private-network enforcement, and in-process HTTP limits;
- GMA now provides an atomic provider-neutral limiter and Redis provider with
  cross-instance tests;
- BunkFy Production API composition rejects an ephemeral Data Protection key
  ring;
- BunkFy production file policy exposes only canonical module object types;
- the preview stack uses an explicit `Preview` environment, loopback edge
  binding, named volumes, and documentation that it is not production;
- repository security workflows, CodeQL, dependency updates, SBOM evidence,
  and immutable GitHub Action pins are present.

Gaps confirmed by the audit:

- the general HTTP limiter is process-local even when the API has multiple
  replicas;
- forwarded-header trust accepts individual proxy IPs but not reviewed CIDR
  ranges;
- Data Protection composition is duplicated in BunkFy and generated Skeleton
  hosts;
- generic MinIO validation accepts plaintext Production transport and bucket
  auto-creation without an explicit exception;
- Production startup does not require an exact release identity or declared
  topology;
- the public API does not connect topology choice to forwarded headers and
  distributed limiting;
- the Admin API can be configured without a product-level Production exposure
  guard;
- the web Nginx reference lacks CSP, Permissions-Policy, and HSTS;
- no concise secure Production deployment contract ties these declarations to
  private operational evidence.

## Invariants

1. `Production` is not a synonym for preview. An unspecified or preview
   deployment profile fails startup.
2. Forwarded client identity is accepted only from configured proxy IPs or
   networks. Hosted operation cannot enable unknown proxies.
3. Multi-replica and hosted public APIs use the distributed limiter. Provider
   failure denies the request with a stable service-unavailable response.
4. Sensitive requests consume both the general client budget and the sensitive
   client budget atomically.
5. Rate-limit storage keys and metrics do not expose client addresses.
6. Production authentication state uses a persistent, shared key ring. The
   deployment declares how that ring is protected at rest; code does not claim
   to verify KMS or volume encryption.
7. Hosted object storage uses encrypted transport, a pre-provisioned bucket,
   and a declared dedicated service credential. A self-hoster may accept a
   narrowly documented private-network exception, but it is never implicit.
8. A Production process identifies the exact source commit. Container-hosted
   operation also identifies the immutable image digest supplied by the
   orchestrator.
9. The Admin API remains on an approved private-network boundary and is not a
   public recovery endpoint.
10. The web edge emits a restrictive, application-compatible CSP,
    Permissions-Policy, framing/type/referrer controls, and HSTS when delivered
    over HTTPS.
11. Startup declarations are necessary guardrails, not proof of a real
    environment. Launch evidence still requires external scans, IaC review,
    restore drills, and exact artifact records.

## Slice 1 - Reusable GMA Host Safety

1. Move configured Data Protection composition into
   `Gma.Framework.Api.Production` and cover stable-name, development, and
   Production persistence behavior.
2. Add `InProcess` and `Distributed` HTTP rate-limit modes.
3. In distributed mode, atomically apply general and sensitive client
   partitions through `IMultiPartitionRateLimiter`.
4. Return bounded `429` and fail-closed `503` Problem Details without exposing
   client or provider data.
5. Add trusted proxy CIDR support and validation.
6. Make MinIO Production plaintext and bucket creation explicit opt-ins.
7. Keep existing defaults source-compatible for single-process development.

## Slice 2 - GMA-Skeleton Alignment

1. Replace the generated host-local Data Protection helper with the framework
   composition.
2. Add generated settings for rate-limit mode and trusted proxy networks.
3. Update architecture guards, app generation, and production-readiness docs.
4. Keep Redis and other deployment adapters opt-in; generated applications must
   choose their provider deliberately.

## Slice 3 - BunkFy Production Contract

1. Add bounded BunkFy deployment options for profile, topology, edge mode,
   release identity, key protection declaration, storage credential profile,
   and narrowly scoped self-hosted exceptions.
2. Validate public API and Admin API roles before service composition.
3. Require hosted/multi-replica public APIs to use distributed limiting and
   trusted forwarded headers.
4. Require direct deployments to disable forwarded headers and terminate HTTPS
   at the application boundary.
5. Preserve the existing persistent Data Protection requirement and consume the
   new GMA helper.
6. Require private-network enforcement for Production Admin API.
7. Add focused startup tests and architecture guards. Do not test private cloud
   claims as if configuration booleans were operational proof.

## Slice 4 - Web Edge And Reference Operations

1. Add CSP, Permissions-Policy, HSTS, and related static header guards to the
   Nginx reference.
2. Keep same-origin API, SignalR, and authenticated download behavior working.
3. Publish a concise secure deployment contract covering required public and
   private inputs, failure behavior, rollout, rollback, and evidence.
4. Keep the preview runbook explicit about its non-production limits and remove
   stale operational instructions superseded by implemented controls.

## Verification Cadence

- Use focused unit, architecture, and static configuration checks while each
  slice is edited.
- Run complete non-Docker repository verification once after the coherent
  implementation is ready.
- Run Docker-backed tests once as the final local gate, batch-fix any failures,
  and repeat only the failed gate.
- Publish exact commits and use GitHub Actions once as candidate evidence rather
  than as an edit-by-edit test runner.

## Acceptance

- GMA focused tests prove proxy IP/CIDR trust, distributed HTTP decisions,
  provider-outage denial, and Production Data Protection/MinIO validation.
- Two application instances sharing Redis enforce one HTTP budget.
- BunkFy startup tests reject unspecified/preview Production profiles,
  contradictory edge/topology settings, ephemeral keys, unsafe hosted storage,
  mutable or missing release identity, and a non-private Admin API.
- Preview remains runnable only as `Preview` and is not renamed or documented as
  production.
- Web verification proves the required headers are present and compatible with
  the built application.
- Full local gates and exact-candidate CI pass in every changed repository.
- The final report names what is implemented publicly and what still requires a
  real private environment before hosted launch.

## Verification

- GMA Framework full build and test gate passed with `1046` tests and no
  warnings.
- GMA-Skeleton `eng/verify.ps1 -SkipRestore` passed across solution sync,
  selection generation, migration drift, architecture, integration, and host
  composition checks.
- BunkFy backend `eng/verify.ps1 -SkipRestore` passed with a clean build, all
  migration-drift checks, architecture guards, host contract tests, and
  non-Docker integrations.
- Web `pnpm verify` passed type checking, lint, `131` tests, and the production
  build. The runtime Nginx image also accepted `nginx.conf` with `nginx -t`.
- Final Docker gates passed with `20/20` GMA-Skeleton integrations and `64/64`
  BunkFy integrations. Disposable MinIO test hosts now declare Development
  explicitly so Production storage validation remains strict.
- Product-root security/bootstrap checks passed, and
  `eng/verify-operations.ps1` accepted the preview Compose contract. The
  unrelated user-edited root workspace solution was preserved rather than
  regenerated.
- Exact-candidate GitHub Actions passed for GMA Framework `730e62a`, GMA
  Skeleton `5a9e8f1` (validation, security baseline, and CodeQL), BunkFy
  Backend `5118f5a` (validation and Docker), and BunkFy Web `6ad7a32`.
  Product-root publication remains separate because its user-edited workspace
  solution was intentionally not staged or regenerated.

## Deferred

- Actual production IaC, accounts, networking, WAF/CDN, KMS, secrets, managed
  databases, object-store policy, SIEM, alert routes, and budgets.
- Container signing, release promotion, and external edge scans beyond the
  repository supply-chain baseline.
- Backup/restore and RPO/RTO evidence from a real hosted environment.
- Private support JIT access and incident operations under SP-008 and SP-009.
