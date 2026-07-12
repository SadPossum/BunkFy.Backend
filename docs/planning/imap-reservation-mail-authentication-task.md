# IMAP Reservation Mail Authentication Task

Status: implemented

Current rotation protocol: [IMAP Reservation Mail Key Rotation Task](imap-reservation-mail-key-rotation-task.md). That task supersedes this task's original single-key `v1` wire format and schema version 2 material while preserving its trust-classification decisions.
Date: 2026-07-12

## Goal

Prevent an arbitrary mailbox sender from creating or changing a reservation merely by attaching a structurally valid JSON envelope. The strict integration-mail adapter must authenticate the exact decoded attachment before it emits `reservation.v1` or replayable `mail.unparsed.v1` evidence.

This is authentication for the BunkFy integration-mail contract. It is not a substitute for a future provider adapter's DKIM, DMARC, sender-domain, webhook-signature, or vendor account controls.

## Decision

Require an HMAC-SHA256 signature in exactly one `X-BunkFy-Attachment-Signature` message header. The header value is `v1=<base64-signature>`.

The signed bytes are:

1. the fixed ASCII domain separator `BunkFy.ImapReservationMail.Attachment.v1\n`;
2. the exact decoded bytes of the one configured JSON attachment.

The signing key is 32 through 64 random bytes represented as strict base64 in the adapter secret document. It is resolved per run with the mailbox credential, never stored in connection metadata, checkpoints, status, receipts, logs, or configuration. Comparison is fixed-time and temporary key/signature buffers are cleared.

Producers must use a byte-preserving MIME transfer encoding such as Base64 after signing. The adapter verifies decoded attachment bytes, so transport-safe re-encoding is allowed while content canonicalization after signing is not.

The adapter configuration/secret schema advances to version 2. Existing version 1 material fails closed and must be deliberately replaced. Rotating the signing key does not change mailbox identity or reset the UID checkpoint; unprocessed mail must have been signed by the currently active key.

## Trust Classification

- exactly one matching attachment plus exactly one valid signature and a valid envelope emits `reservation.v1`;
- a valid signature over a malformed or future-format envelope emits `mail.unparsed.v1`, retaining the bounded RFC822 source for an explicitly installed parser upgrade;
- missing, duplicate, malformed, or invalid signatures, missing/ambiguous matching attachments, malformed MIME, and unsigned mail emit `mail.untrusted.v1`;
- messages exceeding the configured retrieval bound remain content-free `mail.oversized.v1` because their bytes cannot be authenticated safely;
- every evidence result is still durably acknowledged before the mailbox UID checkpoint advances, so hostile mail cannot block later messages.

Only `mail.unparsed.v1` is eligible for the current reservation-mail replay parser. `mail.untrusted.v1` cannot be promoted by reprocessing because no trusted source assertion exists. Historical `mail.unparsed.v1` receipts remain an explicit operator-authorized legacy replay path; this pre-deployment repository does not need a data rewrite.

## Ownership

- `BunkFy.Parsers.ReservationMail` owns bounded MIME attachment extraction and the transport-neutral HMAC verification helper shared by live acquisition tests and producers.
- `BunkFy.Adapters.ImapReservationMail` owns key material validation, trust classification, mailbox progress, and observation record types.
- Ingestion remains unaware of mail authentication mechanics. It stores the resulting evidence and applies its existing authorization, retention, legal-hold, reprocessing, and product-dispatch rules.
- GMA remains unchanged. This protocol is BunkFy-specific and has not been proven reusable across projects.

## Security Invariants

- no unsigned or incorrectly signed attachment reaches `reservation.v1`;
- a signature authenticates decoded attachment bytes, not mutable subject, sender, transfer encoding, or mailbox metadata;
- duplicate signature headers fail closed;
- attachment ambiguity fails closed before any envelope is trusted;
- HMAC keys meet a cryptographic minimum, are bounded, and do not participate in mailbox checkpoint identity;
- trust failures expose only stable record/error categories, never signatures, keys, addresses, message content, or parser exceptions;
- authenticated malformed evidence is distinguishable from unauthenticated evidence without adding provider concepts to Ingestion;
- replay, deduplication, proposal authority, retention, and source-reprocessing lineage remain unchanged.

## Verification

- unit tests cover valid signatures, wrong keys, tampering, missing/duplicate/malformed headers, ambiguous attachments, authenticated malformed envelopes, strict key material, and key rotation without checkpoint reset;
- the GreenMail Docker test sends signed valid mail, proves normal checkpointing, and proves unsigned poison mail becomes `mail.untrusted.v1` without blocking progress;
- parser tests continue to cover trusted retained `mail.unparsed.v1` evidence;
- architecture guards keep cryptographic mail mechanics in the adapter/parser packages and GMA untouched;
- build, migration drift, fast tests, Docker tests, vulnerability audit, source-package checks, and submodule checks pass.

## Deferred

- provider-specific DKIM/DMARC verification and trusted mail-gateway `Authentication-Results` policy;
- sender/domain allowlists used only as routing defense in depth, not cryptographic authentication;
- S/MIME, OpenPGP, vendor webhook signatures, and OTA-specific trust models;
- generalized framework extraction.

## Completion

Implemented schema version 2 IMAP material with a mandatory bounded signing key, redacted credential representation, fixed-time HMAC verification, and a single-pass bounded MIME extraction path. The live adapter now emits `reservation.v1` only for an authenticated strict envelope, keeps authenticated malformed/future envelopes replayable as `mail.unparsed.v1`, and isolates missing, ambiguous, malformed, unsigned, tampered, or incorrectly signed content as non-reprocessable `mail.untrusted.v1`. Signing-key rotation leaves mailbox checkpoint identity stable, key bytes are cleared after each run, and oversized decoded attachments fail to evidence without blocking UID progress.

Focused tests cover signatures, tampering, malformed and duplicate headers, attachment ambiguity, strict key material, key clearing, redacted disclosure, decode bounds, source-identity bounds, rotation, and parser exclusion. The real GreenMail path proves signed SMTP-to-IMAP acquisition and unsigned poison-message progress. Architecture guards keep protocol literals in adapter/parser ownership. The zero-warning build, migration drift checks, 1,496 non-Docker tests, 31 Docker tests, vulnerability audit, source-package checks, and GMA dev-head checks pass.
