# AdapterHost Production Admission

`BunkFy.AdapterHost` is an independently deployable, single-connection adapter
daemon. Production startup is fail closed until its release, target service,
coordination mode, and local status exposure are explicitly approved.

## Required Runtime Shape

- use `AdapterHost:CoordinationMode=server-lease` with a stable, non-empty
  `WorkerId`;
- use HTTPS for `ServiceBaseAddress` and keep
  `AllowInsecureLoopback=false`;
- provide exactly one ingress-token source and readable, bounded adapter
  configuration/secret material;
- bind `ListenUrl` to an HTTP(S) origin without a route base, user information,
  query, or fragment;
- choose `Disabled` status exposure for network listeners, or
  `LoopbackOnly` with a loopback listener.

`/health/live` and `/health/ready` contain only a state label. `/status` is not
mapped when exposure is `Disabled`; when `LoopbackOnly` is approved it retains
the bounded factual snapshot described by the adapter runtime docs.

## Approval Configuration

Use deployment configuration or environment variables, not a committed live
approval. The equivalent JSON shape is:

```json
{
  "AdapterHost": {
    "ProductionAdmission": {
      "ApprovalState": "Approved",
      "ApprovalReference": "ops/adapter-host/change-1234",
      "DeploymentProfile": "Hosted",
      "Runtime": "Container",
      "SourceCommitSha": "0123456789abcdef0123456789abcdef01234567",
      "ContainerImageDigest": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "ApprovedAdapterType": "imap.reservation-json",
      "ApprovedServiceBaseAddress": "https://api.example.test/",
      "StatusEndpointExposure": "Disabled"
    }
  }
}
```

For a process deployment use `Runtime=Process`; an image digest is then not
required. `SourceCommitSha` is always the exact lowercase 40-character commit.
A container digest is the exact lowercase immutable OCI `sha256:` digest.

`ApprovedAdapterType` must match `AdapterHost:AdapterType`.
`ApprovedServiceBaseAddress` must match the complete HTTPS runtime base address,
including any path base, after normal trailing-slash URI normalization.
`ApprovalReference` is a non-secret pointer to reviewed deployment evidence.

## Startup Evidence

After validation the host logs one structured, payload-free approval record
containing the evidence reference, deployment/release identity, adapter type,
approved service base, coordination mode, and status exposure. It does not log
tenant, property, connection, token, checkpoint, provider material, or paths.

Readiness remains `503 starting` until:

1. exactly one configured runner is selected and supports the chosen polling
   mode;
2. the current ingress token can be acquired and validated;
3. current configuration and optional secret material can be read for the
   runner's schema and immediately disposed;
4. local-file Development mode also acquires its checkpoint lease.

Provider reachability and a successful source poll are not readiness
requirements. Cycle failures remain visible through bounded status state where
status is approved, and through logs/telemetry supplied by the deployment.

## Activation Checklist

1. Create or rotate the connection-bound ingress credential and place it in the
   selected secret source.
2. Verify the Ingestion connection is `RemotePolling`, enabled, and matches the
   configured tenant, property, connection, and adapter type.
3. Record the reviewed commit, immutable image digest when applicable, exact
   service base, adapter type, and status exposure in deployment evidence.
4. Set approval to `Approved`, deploy, and confirm the payload-free approval log.
5. Confirm `/health/live` and `/health/ready`; confirm `/status` is absent or
   reachable only through the approved loopback path.
6. Exercise lease claim, renewal, observation acknowledgement, checkpoint
   advance, credential revocation, and graceful replacement on the target
   environment.

Changing the release, adapter type, service base, runtime kind, or status
exposure requires a new approval. Secret rotation does not require changing the
approval because token and material sources reload at runtime.
