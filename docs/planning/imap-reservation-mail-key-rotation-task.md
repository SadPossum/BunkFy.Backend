# IMAP Reservation Mail Key Rotation Task

Status: implemented
Date: 2026-07-12

## Goal

Allow zero-downtime rotation of reservation-mail attachment signing keys. A deployment must be able to accept messages already queued under the previous key while producers move to a new key, without trying every key, resetting mailbox checkpoints, or retaining obsolete keys indefinitely.

## Protocol

Advance the signed integration-mail header to:

`X-BunkFy-Attachment-Signature: v2=<key-id>:<base64-hmac-sha256>`

The HMAC input is:

1. fixed ASCII domain separator `BunkFy.ImapReservationMail.Attachment.v2\n`;
2. the exact normalized ASCII key ID;
3. one newline byte;
4. the exact decoded attachment bytes.

Binding the key ID prevents a valid signature from being relabeled to another configured key. The adapter parses exactly one bounded header, resolves exactly one key by ordinal ID, and performs one fixed-time comparison. Unknown IDs, legacy `v1` headers, malformed values, duplicate headers, and invalid signatures are untrusted evidence; there is no fallback key trial or algorithm downgrade.

Key IDs are 1-64 lowercase ASCII characters. The first character is alphanumeric; remaining characters are alphanumeric, `.`, `_`, or `-`. IDs are operational metadata rather than secrets and must not contain provider, tenant, property, mailbox, or credential values.

## Secret Material

Advance IMAP material to schema version 3. Replace the single `observationSigningKey` with:

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

The ring contains one through four unique keys. Every key is strict base64 decoding to 32-64 bytes. The material parser rejects duplicate IDs, duplicate key bytes, unknown fields, whitespace/case normalization, malformed keys, and oversized rings. All decoded key buffers are cleared if any later item makes the whole material invalid, and the accepted ring is cleared when the adapter run ends.

The adapter does not choose a producer's current key. Deployment secret rotation controls which key producers use and which verification keys remain accepted:

1. add the new key alongside the old key;
2. deploy/reload adapter secret material;
3. move producers to the new key ID;
4. allow the mailbox delivery horizon to drain;
5. remove the old key and retain the secret-store audit externally.

Changing ring membership does not alter host/port/folder/username mailbox identity and never resets UIDVALIDITY/UID checkpoints.

## Ownership

- `BunkFy.Parsers.ReservationMail` owns the versioned header grammar, domain-separated signature helper, bounded key-ID validation, and single-key verification primitive.
- `BunkFy.Adapters.ImapReservationMail` owns strict key-ring material, exact key lookup, run-lifetime disposal, trust classification, and schema metadata.
- Ingestion remains unaware of cryptographic key IDs and continues to receive only trusted normalized observations or untrusted evidence.
- GMA remains unchanged because this is a BunkFy integration-mail protocol.

## Security Invariants

- key ID and attachment bytes are authenticated together;
- unknown or removed IDs never trigger trial verification against another key;
- legacy algorithm/header versions fail closed;
- duplicate IDs and duplicate key material fail configuration;
- key count, ID length, header length, key bytes, and attachment bytes are bounded before cryptographic work;
- malformed material clears every key decoded earlier in the same failed parse;
- key IDs may appear only in the source message header and deployment configuration, not run status, checkpoints, Ingestion errors, or credential rendering;
- accepted and rejected observations retain existing acknowledgement/checkpoint semantics.

## Verification

- unit tests cover both keys during overlap, current-only and previous-key operation, removed/unknown IDs, relabeling, legacy downgrade, duplicate IDs/keys, null entries, ring bounds, key-ID grammar, disposal, and checkpoint stability;
- the GreenMail Docker scenario sends valid messages under old and new IDs during overlap, then proves an unknown/removed ID becomes untrusted without blocking later UIDs;
- capability metadata reports schema version 3 in control-plane hosts;
- architecture guards keep protocol literals and key-ring mechanics in adapter/parser ownership;
- build, migration drift, fast tests, Docker tests, vulnerability audit, source-package checks, and submodule checks pass.

## Deferred

- automatic key issuance, producer distribution, and secret-store rotation workflows;
- validity windows or revocation audit inside BunkFy rather than the deployment secret store;
- asymmetric signatures, S/MIME, OpenPGP, DKIM/DMARC, and vendor-specific trust;
- generalized framework extraction.

## Completion

Implemented schema version 3 IMAP secret material with a bounded one-to-four-key ring, strict lowercase key IDs, duplicate ID/material rejection, exact ordinal lookup, redacted rendering, and run-lifetime cryptographic buffer clearing. The signed-mail protocol now uses `v2=<key-id>:<base64-hmac>` and authenticates the key ID together with the decoded attachment under a versioned domain separator. Unknown or removed IDs, legacy `v1` headers, relabeled signatures, malformed values, and invalid signatures fail closed as non-reprocessable untrusted evidence without fallback key trials.

Mailbox checkpoint identity remains independent of ring membership. The real GreenMail flow accepts current and previous producers during overlap, then proves that removing the previous key immediately classifies another old-key message as untrusted while later UIDs continue. Control-plane capability metadata reports schema version 3, architecture guards keep the protocol in BunkFy adapter/parser ownership, and GMA is unchanged.

The zero-warning build, all migration drift checks, 1,516 non-Docker tests, 31 Docker tests, vulnerability audit, source-package checks, recursive submodule cleanliness check, recorded-pointer check, and `origin/dev` head check pass.
