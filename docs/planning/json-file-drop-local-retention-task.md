# JSON File-Drop Local Artifact Retention Task

Status: implemented
Date: 2026-07-12

## Goal

Bound disk growth from `json.file-drop` processed archives and malformed-input quarantine without moving node-local filesystem ownership into Ingestion. Retention must be incremental, fail visibly, preserve unknown artifacts, and never imply that Ingestion legal holds protect files they cannot address transactionally.

## Ownership

- `BunkFy.Adapters.JsonFileDrop` owns local `pending`, `processed`, and `failed` lifecycle under its configured root.
- Ingestion owns only observations it durably received. It does not store local paths, enumerate adapter nodes, or delete local files.
- Worker and `BunkFy.AdapterHost` provide deployment-specific retention settings when composing the adapter.
- GMA remains unchanged because this policy is tied to the BunkFy file-drop layout and acknowledgement contract.

## Policy

The adapter options require:

- processed archive retention, default 7 days;
- failed quarantine retention, default 30 days;
- a bounded maximum number of deletions per category per run, default 100.

Retention durations are at least one hour and at most ten years. This repository is pre-deployment, so the bounded defaults become the baseline rather than preserving an unbounded legacy mode.

Processed inputs are already represented by a durable Ingestion receipt. Immediately before a successfully acknowledged input is moved to `processed`, the adapter stamps the source file's last-write time with its own UTC clock. Only top-level regular `.json` files older than the processed cutoff are eligible. Reparse points, directories, temporary files, and unknown extensions are retained and reported as maintenance failures.

Failed inputs have no Ingestion receipt. Their strict `.failure.json` sidecar remains the authoritative quarantine timestamp. Only a valid version 1 sidecar older than the failed cutoff can authorize deletion of its exact adjacent raw artifact. The raw artifact is deleted first and the sidecar second, so a crash cannot leave an untracked raw file. A later run may remove an expired sidecar whose raw artifact is already absent. Invalid metadata, sidecars with unsafe names, standalone raw files, unrelated files, and temporary files are never automatically deleted.

The maintenance budget applies independently to processed and failed artifacts so one category cannot starve the other. Work is opportunistic on active adapter runs; a disabled connection creates no new artifacts but may require deployment-level cleanup if its node is being retired.

## Run Semantics

Maintenance runs after host-owned directories are validated and before pending input acquisition.

- successful deletion does not turn an otherwise successful source run into a warning;
- an individual unsafe or undeletable eligible artifact is retained and counted as a maintenance failure;
- source acquisition and durable submission continue despite maintenance failures;
- when the source outcome is otherwise successful, maintenance failure produces `PartiallySucceeded` with stable code `json-file-drop.retention-maintenance-failed` and a non-sensitive count-only message;
- when the source outcome already has a more important partial/failed code, preserve that code and append only a generic maintenance-failure count;
- cancellation and inability to validate/enumerate an owned working directory still fail the run normally;
- filenames, paths, payloads, timestamps, and filesystem exception text never enter run messages.

## Legal Hold Boundary

This policy is ordinary node-local retention, not a compliance legal hold. Property legal holds in Ingestion protect only Ingestion-addressable raw payloads and normalized history. A deployment that needs malformed local input preserved beyond its configured duration must stop file-drop retention for that node through deployment configuration and preserve/export the volume under an external evidence process. A future centralized quarantine service may add audited holds after it owns durable node inventory and artifact identity.

## Verification

- options reject unsafe roots, durations, and budgets while keeping path values out of `ToString()`;
- processed files use adapter-controlled archive time rather than producer timestamps;
- expired processed artifacts are removed incrementally while current, reparse, and unknown files remain;
- expired valid raw/sidecar pairs are removed raw-first, interrupted pairs recover, and malformed/orphan metadata is retained;
- one category cannot consume the other category's deletion budget;
- locked or unsafe artifacts do not block valid pending observations and surface only bounded maintenance evidence;
- Worker and standalone host configuration validate and compose the same policy;
- a Docker Worker scenario proves old quarantine cleanup alongside normal file-drop processing;
- build, migration drift, fast tests, Docker tests, vulnerability audit, source-package checks, and submodule checks pass.

## Deferred

- centralized inventory and cross-node search;
- Admin API/CLI download, export, repair, delete, requeue, or retention-pause operations;
- audited quarantine legal holds;
- malware/content scanning and provider-specific invalid-input triage;
- cleanup for a permanently disabled connection whose node never runs again.

## Completion

Implemented validated adapter options with 7-day processed and 30-day failed defaults, independent 100-item deletion budgets, and an explicit deployment retention pause. Worker and standalone host composition use the same bounded one-hour-through-ten-year policy. The adapter stamps acknowledged archives with its own clock, removes only eligible top-level processed JSON, and requires a strict adjacent version 1 sidecar with an allowed quarantine error code before raw failed evidence can expire. Raw failed artifacts are deleted before sidecars; interrupted sidecar-only cleanup resumes safely, while malformed metadata, orphan raw files, reparse points, unknown files, and temporary files are preserved.

Maintenance runs before acquisition but individual maintenance failures do not block valid pending observations. A clean source outcome becomes partial with `json-file-drop.retention-maintenance-failed`; an existing source error keeps its primary code and receives only a count-only maintenance suffix. Focused tests cover options, adapter-owned timestamps, both retention classes, independent budgets, interrupted pairs, malformed/orphan preservation, maintenance/source interaction, pause behavior, host validation, and path disclosure. The Docker Worker scenario purges expired processed and failed artifacts while processing valid and newly quarantined siblings through TaskRuntime.

The zero-warning build, migration drift checks, 1,507 non-Docker tests, 31 Docker tests, vulnerability audit, source-package checks, GMA dev-head checks, and diff checks pass. GMA remains unchanged.
