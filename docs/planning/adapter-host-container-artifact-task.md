# AdapterHost Container Artifact Task

Status: implemented and verified
Date: 2026-08-11

## Goal

Make container-mode `BunkFy.AdapterHost` deployable from the exact scanned and
attested BunkFy product candidate instead of requiring an unretained rebuild or
an unrelated process artifact.

## Finding

AdapterHost already has fail-closed Production admission for its source commit,
runtime kind, immutable image digest, service origin, adapter type, lease mode,
and status exposure. The product backend Dockerfile publishes API, Worker,
Admin API, Admin CLI, and migrations, but omits AdapterHost. As a result, the
retained backend OCI candidate cannot start the daemon whose image digest the
admission policy is intended to bind.

## Decision

Package AdapterHost inside the existing multi-host backend image at
`/opt/bunkfy/adapter-host`. This keeps one product backend digest and the
existing two-image promotion contract while making the admitted executable part
of the scanned bytes. The image already carries the adapter dependencies used
by API and Worker, so this adds the deployable entry point without introducing
a new product dependency graph.

This does not make AdapterHost a singleton service. Each process remains bound
to one Ingestion connection and must receive separate runtime identity,
credential, material, network, lease, and lifecycle configuration.

## Ownership

- BunkFy Backend owns the executable and backend image publish graph.
- The BunkFy product root owns candidate-image evidence and promotion policy.
- Ingestion continues to own connections, ingress credentials, leases,
  checkpoints, observations, and receipts.
- Deployment composition owns connection-scoped process instances and secrets.
- GMA has no responsibility here; no reusable framework change is required.

## Delivery

- [x] Publish `BunkFy.AdapterHost` into the backend Docker build output.
- [x] Guard all deployable backend hosts in architecture tests.
- [x] Guard the AdapterHost artifact in product image evidence policy.
- [x] Document the stable container entry point and one-connection topology.
- [x] Run focused checks and one end-of-slice backend image build.

## Verification Evidence

- The focused container-packaging architecture guard passed (`1/1`).
- The consolidated Architecture test suite passed (`103/103`).
- The root product-image evidence policy and OCI fixture passed.
- The backend image built as
  `sha256:372db6fd4332ddcfd69ad5c4ee0ccb81aac85ad445e290d92af9e333f261d2e9`
  (208,932,183 bytes), retained the non-root `app` user (UID `1654`), and
  contained the AdapterHost DLL, dependency manifest, and runtime manifest at
  `/opt/bunkfy/adapter-host`.
- The packaged AdapterHost DLL SHA-256 was
  `12dea333de005dfeb7b9e06961444227418e6d06c29a5bc1f2935b3ea360624e`.

No daemon, provider, lease, or deployed-admission claim is made by this local
artifact evidence; those remain in the follow-up composition slice.

## Verification Boundary

The slice is complete when the backend image builds once, contains the expected
AdapterHost DLL at the documented path, and all focused architecture and image
policy checks pass. That proves artifact availability, not a running adapter,
provider access, ingress credential policy, lease behavior, restart safety, or
deployed admission.

## Deferred

A follow-up product-composition slice will create a bounded, connection-scoped
Preview launcher and exercise the existing deployed AdapterHost verifier with
synthetic provider data. A dedicated third product image remains deferred until
its smaller attack surface justifies extending candidate, promotion, rollback,
and production-admission evidence together.
