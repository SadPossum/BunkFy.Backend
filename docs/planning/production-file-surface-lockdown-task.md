# Production File Surface Lockdown Task

Status: implemented and verified
Date: 2026-07-21

## Goal

Close the unsafe generic upload surface before BunkFy accepts real customer data, while preserving the private object-storage capability required by Ingestion.

This is the P0 BunkFy boundary of company-readiness item SP-004. It does not claim that a future document workflow, content inspector, or all adapter-ingress controls are complete.

## Audit Finding

The public API composed GMA Files and therefore exposed authenticated scope-aware `POST`, `GET`, and `DELETE /api/files` endpoints without a BunkFy product purpose, permission, retention policy, or legal-hold workflow. Its production-base configuration allowed text, images, PDFs, JSON, and raw email while content inspection was disabled.

Ingestion separately requires `Gma.Framework.FileManagement` as private infrastructure for bounded raw payloads. Removing object storage would break that owned workflow; exposing the generic Files API is not required for it.

## Ownership Boundary

- GMA Files remains an optional generic private-user file front door. BunkFy does not compose it until a product-owned workflow can justify and govern it.
- GMA Framework continues to own the provider-neutral storage contract and MinIO adapter.
- BunkFy Ingestion owns raw payload purpose, protocol validation, retention, legal holds, and object references.
- BunkFy production policy owns which declared media types its hosts permit.
- Trusted type detection, inspector capability/health contracts, and reusable quarantine orchestration belong in GMA rather than in a BunkFy-specific abstraction.
- A future guest document feature must own dedicated permissions, lifecycle, deletion, audit, and country/purpose approval rather than reuse a generic attachment endpoint.

## Implemented Slice

1. Remove `Gma.Modules.Files.Api` from the BunkFy public API composition and project graph.
2. Keep MinIO storage registered for Ingestion.
3. Limit every production-base host configuration to `application/json`.
4. Permit `message/rfc822` only in API and Worker Development settings so the local IMAP reservation adapter remains testable without silently enabling raw-email storage in production.
5. Add architecture guards that prove the generic endpoint cannot return through composition drift and that production media-type policy stays narrow.

## Security Invariants

- BunkFy exposes no generic customer upload/download/delete endpoint.
- Production configuration does not permit text, image, PDF, or raw-email objects.
- Development exceptions are explicit and do not alter production-base policy.
- Internal object storage remains private and is not a substitute for a product file domain.
- A declared `application/json` type is not treated as trusted content; adapter schema and prohibited-data enforcement remains mandatory under SP-005.

## Verification

- focused architecture tests pass;
- the public API builds without a GMA Files API reference;
- full backend verification passes, including source-package ownership, solution build, migration drift, architecture tests, and fast suites;
- Docker verification remains unchanged because this slice removes an HTTP surface without changing the storage adapter contract.

## Deferred

- GMA trusted content-type detection and spoofed-MIME handling;
- explicit reusable inspector availability and production startup validation;
- persistent quarantine/catalog behavior for an approved document workflow;
- EICAR, polyglot, and scanner-outage integration tests once a real inspector adapter is selected;
- SP-005 canonical adapter schemas, production adapter enablement guards, and prohibited-content enforcement;
- complete object, derivative, cache, and backup deletion evidence for future document features.
