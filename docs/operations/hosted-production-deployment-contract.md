# Hosted Production Deployment Contract

This note describes the configuration contract enforced by the BunkFy API
hosts. It is not production infrastructure or evidence that a deployment is
secure.

## Supported Shapes

`BunkFy:Deployment:Profile` must be one of:

- `SelfHosted`: infrastructure is operated by the customer or deployment
  owner.
- `Hosted`: BunkFy is operated as a hosted service.

`Preview` and `Unspecified` cannot run with
`DOTNET_ENVIRONMENT=Production`.

The public API supports:

- `DirectHttps`: ASP.NET Core terminates HTTPS and forwarded headers are
  disabled.
- `TrustedReverseProxy`: a proxy terminates the public edge and its exact IPs
  or CIDR networks are configured. Unknown proxies remain disabled.

Hosted public APIs must use `TrustedReverseProxy`. Hosted and multi-replica
public APIs must also use `Http:RateLimiting:Mode=Distributed` with the Redis
provider composed and available.

The Admin API must have `Http:PrivateNetwork:Enabled=true`. This startup flag
does not replace a private load balancer, firewall policy, VPN, or external
reachability test.

## Company Support Access

The public code enforces recent strong authentication for Admin API operations,
exact workspace/property authorization, finite compatibility-role leases, and
payload-free elevation signals. The Administration audit retains the canonical
resource scope used for each authorized or denied operation.

Company identities may receive only the dedicated
`bunkfy-company-support` role. Its product-owned permission ceiling excludes
owner access, Data Rights and export operations, raw ingestion payloads,
ingestion credentials and sensitive history, sensitive staff profiles, and
privacy or retention controls. The role is not provisioned automatically; the
private operator plane must provision a narrower role definition and the
evaluated definition cannot expand during an active lease.

Data Rights admin modules are deliberately not composed into BunkFy's Admin API
or CLI. Company support therefore has no public admin route or command that can
return guest export payload. The customer-facing Data Rights flow remains in
the product API behind its case approval, selected-subject, permission, and
authentication-assurance controls.

A hosted deployment must keep company data access disabled until a private
operator plane supplies named identities, ticket and reason ownership,
requester/approver separation, lease reconciliation, emergency review,
customer-visible history, and customer selection/preview proof for any future
support export. Direct routine database or object-store access is outside this
contract and must be prohibited by private infrastructure policy.

## Required Declaration

Each Production API process supplies:

```json
{
  "BunkFy": {
    "Deployment": {
      "Profile": "Hosted",
      "ApiTopology": "MultiReplica",
      "EdgeMode": "TrustedReverseProxy",
      "Runtime": "Container",
      "SourceCommitSha": "0000000000000000000000000000000000000000",
      "ContainerImageDigest": "sha256:0000000000000000000000000000000000000000000000000000000000000000",
      "DataProtectionKeyProtection": "Kms",
      "ObjectStorageCredentialProfile": "DedicatedServiceAccount"
    }
  }
}
```

The release pipeline replaces the deliberately invalid all-zero placeholders
with the exact lowercase candidate values. Startup rejects the placeholders.
Do not use a tag, branch, shortened commit, or mutable image reference as
release identity.

Allowed key-protection declarations are `EncryptedVolume`, `Certificate`,
`Kms`, `Hsm`, and `PlatformManaged`. They state the private deployment's
responsibility; BunkFy cannot prove the external control from configuration
alone.

## Hosted Public API

A hosted public API also requires:

```json
{
  "AllowedHosts": "api.example.com",
  "DataProtection": {
    "KeyRingPath": "/var/lib/bunkfy/data-protection",
    "ApplicationName": "bunkfy"
  },
  "Http": {
    "HttpsRedirectionEnabled": true,
    "HstsEnabled": true,
    "SecurityHeadersEnabled": true,
    "ForwardedHeaders": {
      "Enabled": true,
      "AllowUnknownProxies": false,
      "ForwardLimit": 1,
      "KnownProxies": [],
      "KnownNetworks": [ "10.20.0.0/16" ]
    },
    "RateLimiting": {
      "Enabled": true,
      "Mode": "Distributed"
    }
  }
}
```

The Data Protection path must be durable and shared by all public API replicas.
The private deployment must encrypt it at rest, restrict access to the API
identity, back it up, and prove that a replacement replica can decrypt existing
authentication state.

The configured proxy list must contain only the actual ingress hops. A broad
network is not a substitute for reviewed topology.

## Object Storage

When file management is enabled, the deployment declares
`DedicatedServiceAccount` and supplies credentials through the secret store.
For hosted operation:

- `FileManagement:Minio:Endpoint` uses `https://`;
- `CreateBucketIfMissing=false`;
- `AllowInsecureTransportInProduction=false`;
- `AllowBucketCreationInProduction=false`;
- the bucket and least-privilege policy are provisioned before startup.

Credentials, endpoints, and bucket policy evidence must not be committed to the
repository.

## Worker And Durable Runtime

Production Workers declare the same immutable release identity as API
processes, without HTTP-only edge or API-topology settings. A Worker that
composes Ingestion also satisfies the hosted object-storage rules above.

One explicitly single-replica Worker process profile owns message-journal and
TaskRuntime cleanup. Every other long-running process keeps those cleanup
services disabled. Approved replay and retention windows, task/NATS composition
constraints, and activation evidence are defined in
[Durable Runtime Production Admission](durable-runtime-production-admission.md).

## External Adapter Daemons

Every Production `BunkFy.AdapterHost` deployment declares its exact release,
approved adapter type and BunkFy service base, uses Ingestion-owned server lease
coordination, and makes its operational status either unavailable or
loopback-only. Readiness does not turn healthy until the current ingress token
and adapter material are readable. Configuration, activation, and replacement
evidence are defined in
[AdapterHost Production Admission](adapter-host-production-admission.md).

## Identity Maintenance

Auth retention and Organizations natural expiry/retention each have one
independently declared Worker owner. Public API, Admin API, and non-owner Worker
processes keep their maintenance services disabled. Production startup requires
approved, runtime-matching history windows and explicit acknowledgement of how
those windows apply to existing records. Owner topology, required switches, and
activation evidence are defined in
[Identity Maintenance Production Admission](identity-maintenance-production-admission.md).

## Database Migrations

Run the dedicated migrations executable in `Plan` mode against the intended
database before candidate activation. Production `Apply` binds the same
immutable release to the plan's database-target and target-catalogue hashes,
requires non-secret approval/backup/recovery references, and serializes the
complete module catalogue under one bounded PostgreSQL advisory lock.

An interrupted run resumes only with the same approved release and an exact
compatible migration-history prefix. Unknown or down-level history fails before
mutation. Configuration and recovery evidence are defined in
[Migrations Host Production Safety](migrations-host-production-safety.md).

## Failure Behavior

Production startup fails before a host serves requests or executes background
work when deployment identity, runtime maintenance, topology, edge trust, key
persistence, storage policy, or Admin API network declarations are missing or
contradictory.

In distributed HTTP mode, Redis/provider unavailability fails closed with a
bounded `503` response. An exceeded budget returns `429`; neither response
includes client addresses or provider details.

## Rollout And Rollback

The root `Product Image Evidence` workflow can produce candidate-only backend
and web digests, SBOMs, scans, and build metadata. It does not publish an image,
configure a registry, deploy an environment, or satisfy the private evidence
listed below.

1. Build once and record the source commit, immutable image digest, SBOM, and
   successful repository gates.
2. Provision the bucket, key ring, secrets, trusted proxy entries, private
   Admin API route, and distributed limiter before starting the candidate.
3. Start the candidate with its exact declaration and verify readiness through
   the intended edge.
4. Verify public headers, host filtering, forwarded client identity, Admin API
   non-reachability, shared limits, object access, and key-ring continuity.
5. Promote the same digest. Do not rebuild between environments.
6. Roll back to a recorded prior digest while preserving compatible database,
   object-store, and Data Protection state.

## Evidence Still Required

Before a hosted launch, private operations must retain:

- reviewed infrastructure configuration and exact ingress networks;
- TLS and external header scans;
- secret and service-account policy evidence;
- key-protection and replacement-replica continuity evidence;
- database and object-store backup/restore results;
- Admin API reachability tests from allowed and denied networks;
- shared rate-limit behavior across replicas and provider-outage behavior;
- observability destinations, alert ownership, and rollback records.

The public repository intentionally contains none of the real addresses,
credentials, keys, or cloud account details.
