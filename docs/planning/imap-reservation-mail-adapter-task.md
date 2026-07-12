# IMAP Reservation Mail Adapter Task

Status: implemented

Security follow-up: [IMAP Reservation Mail Authentication Task](imap-reservation-mail-authentication-task.md)

Rotation follow-up: [IMAP Reservation Mail Key Rotation Task](imap-reservation-mail-key-rotation-task.md)

## Goal

Implement the first real network source connector as a polling IMAP adapter. The adapter reads reservation observations from strict JSON email attachments and submits the existing `reservation.v1` contract. It proves mailbox authentication, TLS, MIME acquisition, poison-message progress, replay, and checkpoint recovery without adding email concepts to Ingestion or product modules.

This is an integration-mailbox contract, not a Booking.com or other vendor parser. Provider-specific subject/body/HTML parsing remains a separate adapter once representative source fixtures and change semantics are available.

## Ownership And Composition

- `BunkFy.Adapters.ImapReservationMail` owns IMAP, MIME, authentication, attachment extraction, and mailbox cursor semantics.
- `BunkFy.Adapter.Abstractions` remains transport neutral and is unchanged unless a genuinely shared protocol requirement appears.
- Ingestion continues to own connections, durable receipts, raw evidence, normalization, proposals, retention, and product dispatch.
- Reservations and Inventory remain unaware of email and IMAP.
- The descriptor is composed in every Ingestion control-plane host; the runner is composed only in Worker and `BunkFy.AdapterHost`.
- MailKit is an adapter implementation dependency. It does not enter shared runtime, Ingestion, or GMA.

## Mail Contract

Adapter type: `imap.reservation-json`.

Each supported message contains exactly one configured JSON attachment with this strict version 1 envelope:

```json
{
  "schemaVersion": 1,
  "externalRecordId": "booking-123",
  "sourceRevision": "4",
  "sourceUpdatedAtUtc": "2026-07-12T09:30:00Z",
  "payload": {
    "operation": "upsert",
    "sourceSequence": 4,
    "arrival": "2026-08-01",
    "departure": "2026-08-04",
    "inventoryUnitIds": ["00000000-0000-0000-0000-000000000001"],
    "primaryGuestName": "Example Guest",
    "guestCount": 1
  }
}
```

The adapter authenticates decoded attachment bytes, validates envelope shape and shared observation limits, but does not duplicate Reservations normalization rules. Valid signed attachments become `reservation.v1` observations using the envelope source identity. Signed malformed or future envelopes become replayable `mail.unparsed.v1`; missing, ambiguous, unsigned, or incorrectly signed attachments become non-reprocessable `mail.untrusted.v1`. Both contain the bounded original RFC822 message. Messages too large to retrieve safely become small `mail.oversized.v1` evidence records containing only mailbox UID facts and byte size.

Unsupported evidence is expected to be terminally rejected by the current Ingestion dispatcher after durable receipt. Its raw object remains governed by normal retention and legal-hold rules. This lets a poison message stop neither later mailbox acquisition nor checkpoint progress, while the read-only source mailbox remains available for a future corrected parser or manual replay.

## Configuration And Secrets

Configuration is strict schema version 3 JSON and includes:

- host, port, mailbox, and attachment filename;
- transport security: TLS-on-connect or STARTTLS;
- bounded network timeout, messages per run, and message/attachment sizes;
- an explicit loopback-only plaintext switch for protocol tests and local development.

Secret material is strict JSON containing exactly one authentication mode plus a bounded attachment-signing key ring:

- username and password; or
- username and OAuth 2 bearer token.
- one through four unique lowercase key IDs with unique 32-64 byte base64 HMAC-SHA256 keys.

Credential values are resolved per run through the existing ephemeral material boundary, never placed in connection read models, options, status, payloads, checkpoints, or logs. The adapter disables protocol logging, does not weaken certificate validation, and does not include exception messages in host status.

## Checkpoint Contract

The checkpoint is compact strict JSON containing schema version, a non-reversible mailbox identity hash, IMAP UIDVALIDITY, and the highest durably acknowledged UID. The hash binds host, port, folder, and username without exposing credentials; changing that identity fails closed and requires an explicit checkpoint reset.

- no checkpoint starts at the first currently available UID;
- UIDs are processed in ascending order, with deleted gaps allowed;
- the current read-only folder may use bounded sequence-index lookup to find the next UID, but sequence numbers are never persisted or treated as identity;
- the mailbox is opened read-only and messages are never deleted, moved, flagged, or marked read;
- a changed UIDVALIDITY fails closed without fetching or advancing so an operator can investigate and explicitly reset the disabled connection;
- a batch checkpoint is proposed only for the last included UID;
- every valid, unparsed, or oversized message in that range must receive `Accepted` or `Duplicate` before the checkpoint advances;
- transient connect, authentication, list, fetch, parse-infrastructure, submission, or acknowledgement failures retain the previous checkpoint and replay safely;
- stable operation ids include connection, UIDVALIDITY, UID, record identity, and content hash.

The adapter never treats Message-Id, subject, sender, received date, sequence number, or read state as a durable mailbox cursor.

## Reliability And Security Invariants

- source bytes, decoded attachments, configuration, and secret material are bounded before use;
- aggregate submissions remain within the shared record and byte limits;
- MIME parsing is strict enough to reject ambiguous matching attachments;
- local parser failures carry stable error codes without leaking message content;
- password/OAuth authentication is explicit; opportunistic downgrade and automatic insecure modes are rejected;
- cancellation applies to connect, authenticate, open, list, fetch, submit, and disconnect operations;
- no provider retry loop is hidden inside the runner; TaskRuntime or the standalone host owns retry/backoff;
- a mailbox message is never considered consumed from source state until Ingestion durably acknowledges its observation;
- the adapter has no product-module, persistence, TaskRuntime, host, or GMA dependency.

## Verification

- focused tests cover strict material parsing, TLS policy, password/OAuth selection, valid MIME extraction, malformed and oversized evidence, stable operation ids, batching, acknowledgement mismatch/rejection, replay, and UIDVALIDITY reset protection;
- a real GreenMail Docker test sends SMTP mail, reads it through MailKit IMAP, submits a reservation observation, advances the UID checkpoint, and proves exact replay plus poison-message progress;
- architecture tests guard the adapter dependency direction and keep MailKit confined to its adapter package;
- build, package vulnerability audit, migration drift, fast tests, complete Docker tests, solution/JSON checks, and submodule checks pass.

## Deferred

- vendor-specific Booking.com/OTA email templates and HTML parsing;
- provider-specific DKIM/DMARC trust policy, sender allowlists, and provider-specific anti-spoofing rules beyond the implemented signed integration envelope;
- OAuth token refresh flows beyond consuming a rotated access token from the secret provider;
- IMAP IDLE/continuous mode, QRESYNC, distributed mailbox leases, and horizontal failover;
- provider-template parsers beyond the installed strict reservation-mail replay parser;
- mailbox provisioning, credential issuance, and provider setup workflows.

## Completion

Implemented `BunkFy.Adapters.ImapReservationMail` with MailKit 4.17, strict schema-versioned configuration/secret parsing, TLS-on-connect/STARTTLS enforcement, explicit password or OAuth 2 authentication, key-addressed HMAC-authenticated reservation attachments, read-only mailbox access, bounded MIME extraction, and acknowledgement-gated UID checkpoints. Checkpoints bind a non-reversible host/port/folder/username identity hash plus UIDVALIDITY and UID, so mailbox changes, signing-ring changes, and UID-generation resets cannot silently skip data.

Valid signed strict attachments emit the existing `reservation.v1` contract. Signed malformed/future envelopes emit replayable `mail.unparsed.v1`; missing, ambiguous, unsigned, and incorrectly signed messages emit bounded raw `mail.untrusted.v1`; oversized messages emit content-free `mail.oversized.v1`. All can advance after durable receipt, leaving the source mailbox unchanged and preventing poison mail from blocking later UIDs.

Descriptor metadata is composed in API, Admin API, and Admin CLI; executable code is confined to Worker and `BunkFy.AdapterHost`. Architecture guards confine MailKit and signed-mail protocol mechanics to adapter/parser ownership and verify the host split. Focused tests cover material, transport auth, attachment authentication, TLS, MIME, batching, replay, acknowledgement, trusted/untrusted evidence, checkpoint identity, key rotation/clearing, UIDVALIDITY, and DI behavior. A pinned GreenMail 2.1.9 Docker test proves real SMTP delivery, signed production MailKit IMAP retrieval, replay, and unsigned poison-message progress. The adapter package has no known vulnerable direct or transitive packages from the current NuGet audit; source-package/submodule checks, the zero-warning build, migration drift, 1,496 non-Docker tests, and 31 Docker tests pass.
