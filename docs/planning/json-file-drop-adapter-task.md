# JSON File-Drop Adapter Task

Status: implemented and verified

## Goal

Add a second, materially different local adapter that polls a controlled filesystem inbox and submits strict JSON observation envelopes through the existing assignment-bound Ingestion sink. This validates that the adapter protocol is not coupled to HTTP acquisition and provides a useful integration path for controlled exports and bulk drops.

## Ownership

- `BunkFy.Adapters.JsonFileDrop` owns filesystem acquisition, envelope parsing, deterministic operation identity, and post-acknowledgement archival.
- `BunkFy.Adapter.Abstractions` remains transport-neutral and is unchanged unless a genuinely shared protocol invariant is discovered.
- Ingestion owns connection state, scheduling, durable receipts, deduplication, raw payload retention, normalization, and product dispatch.
- GMA TaskRuntime continues to own polling occurrences, retries, leases, cancellation, and worker execution.

Do not move file watching, file formats, or archive behavior into Ingestion or GMA. One HTTP poller and one filesystem poller are evidence for the BunkFy adapter boundary, not enough evidence for a generalized framework subsystem.

## Directory Contract

- A configured adapter root is host-owned, not supplied by observation data.
- Each connection uses `<root>/<connection-id>/pending`, `<root>/<connection-id>/processed`, and `<root>/<connection-id>/failed`.
- Producers write a temporary non-JSON file in `pending`, flush it, then atomically rename it to a `.json` filename.
- The adapter reads only top-level `.json` files, in ordinal filename order, with a maximum of 100 files per run.
- Directory names are derived only from the trusted connection id; envelope fields cannot select paths.
- Reparse-point/symlink input files are rejected.

## Version 1 Envelope

Each file contains exactly one strict JSON object:

```json
{
  "schemaVersion": 1,
  "recordType": "reservation.v1",
  "externalRecordId": "provider-reference",
  "sourceRevision": "optional-provider-revision",
  "sourceUpdatedAtUtc": "2026-07-12T12:00:00Z",
  "observedAtUtc": "2026-07-12T12:01:00Z",
  "payload": {}
}
```

Unknown fields, unsupported schema versions, missing identities/timestamps/payloads, oversized envelopes, and protocol-limit violations fail closed. The canonical payload bytes are the UTF-8 representation of the JSON `payload` value and use `application/json`.

## Reliability And Security Invariants

- Operation ids are deterministic from connection id, filename, record identity/revision, and canonical payload hash.
- Re-reading a file after an uncertain archive outcome therefore returns `Duplicate`, not a new product effect.
- A file moves to `processed` only when its exact result is `Accepted` or `Duplicate`.
- Rejected files stay in `pending`; successfully acknowledged siblings may archive independently.
- Missing, duplicate, foreign, or extra acknowledgement operation ids fail closed before any archive move.
- A proposed checkpoint is accepted only through the existing assignment-bound acknowledgement. File discovery itself does not skip pending files based on that checkpoint.
- Archive moves are same-volume and non-overwriting. An existing processed file is removed from pending only when its complete content hash matches; a different-content name collision fails closed.
- No payload, filesystem path, checkpoint value, or envelope content appears in adapter error messages.
- Cancellation is observed during discovery, reads, submission, and archival.
- Configuration and secret material follow the existing disposable adapter-material contract. Version 1 accepts only an empty JSON object and no secret.

## Acceptance Checks

- the adapter project references only `BunkFy.Adapter.Abstractions` plus dependency-injection abstractions;
- descriptor-only registration is available to API/Admin API/Admin CLI, while only Worker composes the runner;
- successful and duplicate observations archive, rejected observations remain pending, and acknowledgement mismatches move nothing;
- malformed, oversized, and symlinked inputs quarantine without blocking valid siblings; transient and archive-collision failures move nothing;
- deterministic replay produces the same operation id;
- capability discovery exposes both HTTP polling and JSON file-drop adapters;
- a composed Ingestion task-handler scenario reads a real file, stores a durable receipt, and archives only after acknowledgement;
- solution build, migration drift, fast tests, complete Docker tests, dependency guards, and submodule checks pass.

## Deferred

- network shares and distributed filesystem locking;
- recursive inboxes, provider-specific mapping DSLs, and multi-record files;
- centralized quarantine inventory and operator repair UI;
- S3/object-store drop adapters;
- remote polling assignment and lease APIs;
- extracting shared adapter runtime behavior into GMA.
