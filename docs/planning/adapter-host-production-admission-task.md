# Adapter Host Production Admission Task

Status: complete
Date: 2026-08-05

## Goal

Prevent a Production adapter daemon from starting with an unapproved release,
an unapproved BunkFy service base, weak local-only coordination, exposed
operational identity, or readiness that becomes healthy before required runtime
material is readable.

## Ownership

- BunkFy AdapterHost owns its process topology, release identity, approved
  service base, status exposure, and startup readiness contract.
- Ingestion owns adapter connections, ingress credentials, remote leases,
  checkpoints, observations, and server-side fencing.
- Adapter abstractions and runtime own provider-neutral runner, material,
  checkpoint, cancellation, and completion contracts.
- Individual adapters own provider configuration semantics and polling behavior.
- GMA does not own BunkFy adapter types, Ingestion endpoints, connection
  identities, or this executable's deployment approval.

## Audit Findings

- AdapterHost validates adapter identity, bounded timings, token-source
  exclusivity, material paths, and outbound HTTPS, but Production startup has no
  explicit approval or immutable release identity.
- Production can select the compatibility `local-file` checkpoint mode even
  though server leases provide the durable, server-fenced coordination required
  for independently deployed daemons.
- `/status` is always anonymous and includes the adapter type and connection
  identifier. `ListenUrl` can expose it beyond loopback over HTTP.
- readiness becomes healthy after runner selection and, in local mode, lock
  acquisition, before the ingress token and current configuration/secret
  material have been read.
- Existing cycle cancellation, bounded material reads, token rotation, remote
  lease fencing, and error normalization are already reusable and do not need a
  framework refactor.

## Invariants

- Production requires an approved, non-secret evidence reference and an exact
  source commit; container deployments also require an immutable image digest.
- The approved adapter type and complete BunkFy service base exactly match runtime
  configuration.
- Production uses `server-lease` coordination and never enables insecure
  loopback transport for the outbound ingress client.
- the anonymous status endpoint is either disabled or available only from a
  loopback listener; health endpoints remain payload-minimal.
- `ListenUrl` identifies an HTTP(S) origin and contains no route base, query,
  fragment, or user information.
- readiness remains false until the selected runner is valid and both the
  ingress token and current configuration/secret material can be acquired.
- Development and tests retain local-file compatibility and can run without a
  production approval.

## Delivery

1. [Completed] Add AdapterHost production-admission options, validation, and
   payload-free startup evidence.
2. [Completed] Gate status endpoint mapping and strengthen listener validation.
3. [Completed] Preflight token and runtime material before reporting readiness.
4. [Completed] Add focused validator/host tests, architecture coverage, and an
   operator activation note.
5. [Completed] Run focused checks, then one coherent non-Docker slice gate.

## Outcome

- Production AdapterHost startup now binds an approved release, adapter type,
  complete BunkFy service base, remote coordination mode, and local status
  exposure before the daemon can run.
- Production rejects local-file checkpoint authority and insecure loopback
  ingress. Ingestion's existing server lease remains the durable checkpoint and
  fencing owner.
- `/status` can be removed entirely from a network listener or retained only on
  an approved loopback listener; health responses remain payload-minimal.
- readiness stays false until runner selection, the current ingress token, and
  current configuration/secret material are available. Material is disposed
  immediately after the preflight and is still reloaded for each cycle.
- The admission log contains only reviewed deployment evidence and excludes
  tenant/property/connection identities, token, checkpoint, paths, material,
  and payloads.
- GMA required no change because this policy binds BunkFy Ingestion and adapter
  deployment concepts rather than a reusable framework primitive.

## Verification Evidence

- complete `BunkFy.Adapters.Tests`: 114 passed, including 20 AdapterHost tests;
- complete non-Docker architecture gate: 88 passed;
- complete non-Docker integration gate (`Category!=Docker`): 54 passed;
- `dotnet build BunkFy.slnx --no-restore -m:1 --verbosity:minimal`: succeeded
  with 0 warnings and 0 errors;
- backend solution synchronization completed and the focused production host
  test proved health remains available while `/status` returns `404`;
- no AdapterHost Docker scenario was required or used as acceptance evidence
  because the slice changes no schema, broker, database, or provider behavior.

## Deferred

- Real approval references, release identifiers, service origins, TLS material,
  and runtime selection remain deployment inputs.
- Orchestrator replica reconciliation, workload identity, OAuth or mTLS,
  artifact signing/attestation, and network policy remain environment work.
- Unix ownership/mode policy for token and provider-secret files needs a
  portable, documented deployment contract and is not inferred from one host.
- Adapter metrics, traces, centralized structured logging, provider-health
  probes, and alert routing are a separate observability slice.
- Fleet discovery, dynamic server-delivered material, multi-region locking, and
  generic extraction remain deferred until more than one product proves the
  reusable boundary.

## Verification Cadence

Use focused AdapterHost tests while editing. At the coherent boundary, run the
complete adapter test project, the non-Docker architecture and integration
gates, solution synchronization checks, and one zero-warning solution build.
No Docker scenario is required because this slice changes no schema, broker,
database, or provider implementation.
