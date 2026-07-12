# BunkFy Adapters

Source adapters live outside product modules. They depend on `BunkFy.Adapter.Abstractions` and submit observations through the Ingestion-owned sink; they do not receive product repositories, DbContexts, or caller-selected tenant/property authority.

Standalone push adapters use `BunkFy.Adapters.Http`. `AdapterHttpIngressClient` implements `IAdapterPushObservationSink`, sends the shared observation envelope to the connection-bound Ingestion endpoint, and returns one durable result per operation id. It requires HTTPS except for explicitly enabled loopback development, validates protocol limits before secret access, fetches its token through `IAdapterIngressTokenProvider` for every attempt, and retries only bounded transient transport/status failures. Tokens are request-local and must never be placed in configuration objects, URIs, payloads, default headers, or logs.

The built-in `StaticAdapterIngressTokenProvider` is suitable for simple controlled processes. `BunkFy.AdapterHost` uses a reloadable environment-variable or file token source so credential rotation is visible without recreating the client. The current HTTP client remains push-only at the server boundary.

`BunkFy.Adapter.Runtime` can execute an existing polling `IAdapterRunner` in a standalone process while delivering through that push client. It translates local submissions into HTTP batches, persists a proposed checkpoint before acknowledging it to the runner, and treats accepted plus duplicate results as durable. If a remote acknowledgement succeeds but local checkpoint persistence fails, the cycle fails and replays safely through stable operation ids.

`BunkFy.AdapterHost` provides the runnable single-connection daemon. It holds an exclusive lock on a versioned atomic checkpoint file for its lifetime, reloads bounded configuration/secret files each cycle, runs one cycle at a time with timeout and bounded retry, and exposes loopback status at `/health/live`, `/health/ready`, and `/status`. Status never includes tenant, property, checkpoint text, paths, token, material, or payloads. `fake.http`, `json.file-drop`, and `imap.reservation-json` descriptors support both BunkFy-owned polling and standalone push delivery.

Configure a server connection in `Push` mode, issue an ingress credential, place the token in the configured deployment secret environment variable or file, and provide absolute material/checkpoint paths. Run the process with:

```powershell
.\eng\run-adapter.ps1
```

Only one host may own a connection's checkpoint volume. Horizontally scaled remote workers still require a future server-owned assignment and distributed lease protocol; the node-local lock is deliberately not represented as one.

`imap.reservation-json` is the first real network source connector. It opens a configured IMAP folder read-only through MailKit, locates messages after a UIDVALIDITY-aware UID checkpoint, and extracts exactly one configured `application/json` attachment. A strict version 1 mail envelope supplies external identity, source revision/time, and the existing `reservation.v1` payload. The mailbox is never mutated, and the checkpoint advances only after every selected message is durably accepted or recognized as a duplicate.

Configuration material is schema version 3 JSON:

```json
{
  "host": "imap.example.com",
  "port": 993,
  "mailbox": "INBOX",
  "attachmentFileName": "reservation.json",
  "transportSecurity": "tls",
  "allowInsecureLoopback": false,
  "networkTimeoutSeconds": 30,
  "maximumMessagesPerRun": 25,
  "maximumMessageBytes": 4194304,
  "maximumAttachmentBytes": 1048576
}
```

Use `"starttls"` when the provider requires explicit STARTTLS. Plaintext `"none"` is rejected unless it is explicitly enabled for a literal loopback endpoint. Secret material contains either password or OAuth 2 credentials plus one through four uniquely identified 32-64 byte base64 attachment-signing keys and is re-resolved for every run:

```json
{
  "authentication": "password",
  "username": "adapter@example.com",
  "credential": "<password>",
  "observationSigningKeys": [
    { "keyId": "2026-q3", "key": "<base64-current-key>" },
    { "keyId": "2026-q2", "key": "<base64-previous-key>" }
  ]
}
```

Use `"oauth2"` with a currently valid access token for OAuth. Token refresh remains the secret provider's responsibility. Producers sign the exact decoded JSON attachment and its key ID with domain-separated HMAC-SHA256, then place `v2=<key-id>:<base64-signature>` in one `X-BunkFy-Attachment-Signature` header; a byte-preserving MIME encoding such as Base64 must be applied after signing. Rotation is add new key, reload consumers, shift producers, drain queued mail, then remove the old key. Unknown/removed IDs, legacy `v1` values, missing, ambiguous, unsigned, or incorrectly signed attachments are submitted as bounded, non-reprocessable `mail.untrusted.v1` RFC822 evidence. A correctly signed but malformed/future envelope becomes replayable `mail.unparsed.v1`; oversized messages produce non-content `mail.oversized.v1` evidence. These records are durably received and then terminally classified as unsupported, so poison mail does not block later UIDs and ordinary retention/legal-hold controls still apply. The host-composed `mail.reservation-json` parser can reprocess only retained trusted-unparsed evidence without reopening the source receipt. A UIDVALIDITY change fails closed and requires investigation plus an explicit disabled-connection checkpoint reset. See [IMAP Reservation Mail Adapter Task](../../../docs/planning/imap-reservation-mail-adapter-task.md), [IMAP Reservation Mail Authentication Task](../../../docs/planning/imap-reservation-mail-authentication-task.md), [IMAP Reservation Mail Key Rotation Task](../../../docs/planning/imap-reservation-mail-key-rotation-task.md), and [Ingestion Source Reprocessing Task](../../../docs/planning/ingestion-source-reprocessing-task.md).

`json.file-drop` is a local polling adapter for controlled exports and manual integration drops. Its host-owned root is configured at `Adapters:JsonFileDrop:RootPath`; each connection receives isolated `pending`, `processed`, and `failed` directories beneath its connection id. Producers must flush a temporary non-JSON file and atomically rename it to `.json`. The adapter reads strict version 1 single-record envelopes in filename order, derives a deterministic operation id, and archives only records acknowledged as `Accepted` or `Duplicate`. Protocol-rejected or conflicting valid files stay pending for retry.

Permanently malformed, unsupported, oversized, protocol-invalid, or symlinked inputs move to `failed` with a bounded `.failure.json` sidecar containing only the original filename, stable error code, and UTC timestamp. Valid siblings continue in the same run, which completes as partial. Transient reads, directory failures, content races, acknowledgement mismatches, checkpoint rejection, and archive failures quarantine nothing. To repair an input, inspect the failed raw artifact and produce a corrected file through the normal temporary-name/atomic-rename contract.

Active file-drop runs incrementally remove acknowledged `processed` archives after 7 days and valid raw/sidecar quarantine pairs after 30 days by default, with independent 100-artifact budgets. Worker config uses `Adapters:JsonFileDrop:{RetentionEnabled,ProcessedArchiveRetention,FailedQuarantineRetention,MaximumDeletesPerRun}`; standalone config uses the corresponding `AdapterHost:JsonFileDrop...` keys. Processed timestamps are adapter-owned, failed expiry comes only from strict sidecars, unknown/orphan files are preserved, and maintenance failures add only stable count evidence without blocking valid source work. Retention can be paused through deployment configuration for external evidence preservation, but this is not an audited legal hold.

The file-drop connection material is schema version 1, content type `application/json`, exactly `{}`, and has no secret. The envelope shape and operating invariants are documented in [JSON File-Drop Adapter Task](../../../docs/planning/json-file-drop-adapter-task.md), with permanent-input recovery in [JSON File-Drop Quarantine Task](../../../docs/planning/json-file-drop-quarantine-task.md) and local cleanup in [JSON File-Drop Local Artifact Retention Task](../../../docs/planning/json-file-drop-local-retention-task.md).

The first adapter is `fake.http`, a deterministic polling adapter used to prove replay, receipt, checkpoint, and standalone-host behavior before integrating a real provider. It accepts version 1 JSON configuration:

```json
{
  "endpoint": "https://provider.example/feed",
  "authorizationHeaderName": "Authorization"
}
```

Optional version 1 JSON secret material contains only the header value:

```json
{
  "authorizationHeaderValue": "Bearer <secret>"
}
```

The local material resolver accepts opaque `configuration://name` and `secret://name` references and reads them from `Adapters:Materials:Configurations` and `Adapters:Materials:Secrets`. It is for local development and controlled deployments; do not commit secret values to appsettings. Production secret stores should replace `IAdapterConfigurationMaterialResolver` while preserving the same reference-only connection model.

Adapter packages register an `IAdapterDescriptorProvider` independently from any local `IAdapterRunner`. Every control-plane and worker host composes the same descriptor providers, while only worker hosts compose executable runners. Descriptors declare protocol/configuration schema versions, supported execution modes, and optional polling interval guidance. Polling guidance is a provider limit/default hint; it does not create a schedule or imply that a connection is healthy.

`fake.http` remains a deterministic development adapter. Control-plane hosts compose descriptor metadata for every adapter, while only Worker and the standalone host compose executable runners. File-drop proves a local non-HTTP mechanism, IMAP proves authenticated real-network mailbox acquisition, and the HTTP client proves independently deployed push delivery. Ingestion owns connection administration, one-time ingress credential issuance/rotation, durable receipt storage, and separately authorized raw-payload operator access.
