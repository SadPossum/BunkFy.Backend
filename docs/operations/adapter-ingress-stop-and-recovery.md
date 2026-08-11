# Adapter ingress stop and recovery

This runbook covers BunkFy's third-party HTTP adapter boundary. Internal
polling, retained-evidence reprocessing, and ordinary staff APIs are separate
paths.

## Control order

Use the narrowest effective control:

1. Revoke one credential for a compromised or retired adapter instance.
2. Suspend tenant ingress for a workspace incident.
3. Stop global ingress only for a platform incident or provider-wide threat.

Every control requires a bounded reason code such as `security.review` or
`incident.active`. Do not put guest data, provider secrets, or free text in a
reason code.

## Tenant control

The customer API requires `ingestion.ingress-control.manage` at tenant scope:

- `GET /api/ingestion/adapter-ingress-control`
- `POST /api/ingestion/adapter-ingress-control/suspend`
- `POST /api/ingestion/adapter-ingress-control/resume`

Suspend and resume bodies contain the status response's current
`expectedVersion` and a `reasonCode`. A stale version returns a conflict.
Tenant suspension remains immediately available for incident containment.
Resume additionally requires the public host's configured recent-authentication
assurance because it releases the tenant-wide stop.

## Global control

The Admin CLI control is global, confirmation-gated, and audit recorded:

```powershell
dotnet run --project src/BunkFy.Host.AdminCli -- ingestion ingress-control status --actor operator --output json
dotnet run --project src/BunkFy.Host.AdminCli -- ingestion ingress-control stop --actor operator --expected-version 0 --reason-code incident.active --yes
dotnet run --project src/BunkFy.Host.AdminCli -- ingestion ingress-control resume --actor operator --expected-version 1 --reason-code incident.resolved --yes
```

The Admin API exposes the same status, stop, and resume operations at
`/api/admin/ingestion/adapter-ingress-control`. Stop and resume require explicit
confirmation.

## Expected behavior

- Invalid, expired, revoked, cross-tenant, cross-connection, or
  disabled-connection credentials return an authentication rejection.
- Tenant suspension, global stop, and quota/provider rejection do not write raw
  payloads or receipts.
- Observation batches return a per-record stable rejection code. Remote lease
  control calls return `429` with `Retry-After` for quota rejection or `503`
  for policy/provider unavailability.
- Redis/provider loss denies third-party ingress. It must not make ordinary
  authenticated staff APIs unavailable.
- Restore global control before tenant control when both are active, then prove
  a fresh observation. Do not reuse the drill operation id.

## Alert contract

The metric is
`bunkfy.ingestion.adapter_ingress.decisions`, with only `operation` and
`outcome` dimensions. Scrape-name normalization is exporter-owned and must be
verified in the target environment.

Deployment should route:

- any sustained `provider-unavailable` outcome as critical;
- a quota-rejected ratio above the deployment's approved threshold as warning;
- unexpected sustained `policy-rejected` traffic for operator review.

Alert labels and notifications must not contain tenant ids, credential ids,
external record ids, payload hashes, or guest data. Final thresholds, paging
destinations, and maintenance silences are deployment-owned launch evidence.

## Automated drill

The Docker test
`IngestionOperationsIntegrationTests.Management_api_scopes_connection_lifecycle_and_operational_reads`
uses PostgreSQL, Redis, NATS, and MinIO to prove:

- canonical acceptance and immutable receipt provenance;
- replay without a second receipt;
- credential revocation while another credential and tenant remain active;
- tenant suspension while another tenant remains active;
- global stop across both tenants and reverse-order recovery;
- provider unavailability while the staff management API remains healthy;
- no secret token in read models or admin audit.

Run it through the final slice gate, not after every edit:

```powershell
./eng/test-docker.ps1
```
