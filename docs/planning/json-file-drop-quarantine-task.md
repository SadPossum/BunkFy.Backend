# JSON File-Drop Quarantine Task

Status: implemented and verified

Retention follow-up: [JSON File-Drop Local Artifact Retention Task](json-file-drop-local-retention-task.md)

## Goal

Prevent one permanently invalid file from starving every later valid file in a connection inbox, while preserving fail-closed behavior for transient storage and acknowledgement failures.

## Ownership And Layout

`BunkFy.Adapters.JsonFileDrop` owns quarantine because it owns the source file format and local filesystem lifecycle. Ingestion records the resulting partial source-run outcome but does not store host-local paths or manipulate Worker files.

Each connection uses:

- `pending`: producer-completed `.json` inputs awaiting durable receipt;
- `processed`: exact inputs whose observations were acknowledged as `Accepted` or `Duplicate`;
- `failed`: permanently invalid inputs plus bounded failure metadata.

Quarantine does not create an Ingestion receipt because no trustworthy `AdapterObservedRecord` exists. The failed source run and local metadata are the audit evidence. A repaired file may be reintroduced only by an operator writing it through the normal temporary-file-and-rename producer contract; the adapter never auto-requeues failed input.

## Permanent Failure Taxonomy

The following stable codes quarantine the input and allow valid siblings to continue:

- `json-file-drop.symbolic-link`;
- `json-file-drop.envelope-too-large`;
- `json-file-drop.invalid-json`;
- `json-file-drop.unsupported-envelope`;
- `json-file-drop.protocol-invalid`.

The following remain retryable run failures and move nothing:

- filesystem permission, sharing, or transient read failures;
- pending/processed/failed directory reparse or availability failures;
- input content changing after submission;
- acknowledgement run/lease/operation mismatch;
- checkpoint rejection;
- processed archive collision or move failure;
- cancellation.

## Quarantine Metadata

For every moved input, write a strict version 1 sidecar beside it containing only:

- original filename;
- stable error code;
- quarantine timestamp in UTC.

Do not include the root path, payload, parser exception, provider data, or raw validation message. The raw failed file remains the source artifact. Sidecars use a temporary same-directory write and atomic rename; existing files are never overwritten. Name collisions receive a generated suffix rather than deleting either artifact.

## Run Semantics

- Read files in ordinal filename order within the existing record and aggregate payload limits.
- Quarantine each permanent input independently and continue collecting valid records.
- Submit valid records once as the existing assignment-bound batch.
- Archive only exact accepted/duplicate results.
- Leave protocol-rejected valid records pending.
- Return `PartiallySucceeded` when one or more inputs were quarantined, including when no valid observations remain.
- Include only the quarantine count in the run message; never include filenames or paths.
- A later clean run may succeed normally; prior partial runs remain in Ingestion history.

## Acceptance Checks

- an invalid first file cannot block a valid later file;
- all-invalid input completes as partial without calling the sink;
- failed raw files and sidecars are preserved without overwrite;
- metadata contains no payload, root path, or parser detail;
- symlink quarantine moves only the link, never reads or moves its target;
- transient read and archive failures quarantine nothing;
- repaired/reintroduced content receives normal deterministic processing;
- real queued Worker execution records a partial Ingestion run while archiving valid siblings and retaining failed evidence;
- build, migration drift, fast tests, complete Docker tests, dependency guards, and submodule checks pass.

## Deferred

- centralized quarantine inventory across multiple Worker nodes;
- Admin API/CLI download, repair, delete, or requeue operations;
- audited quarantine legal holds beyond the implemented adapter-local bounded retention policy;
- malware/content scanning;
- provider-specific error classification beyond the version 1 envelope.
