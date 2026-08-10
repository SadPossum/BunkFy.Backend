# SMTP Email And Preview Onboarding Proof Task

Status: active
Date: 2026-08-10

## Goal

Add a replaceable BunkFy email transport behind GMA's existing `IEmailSender`
contract, then use a private preview mail sink to prove the deployed workspace
invitation and QR enrollment workflows with clean verified accounts.

This slice must not claim real-provider delivery from a Mailpit result and must
not obtain verification secrets by reading Auth tables, outbox rows, broker
messages, or module internals.

## Ownership

- GMA Framework continues to own only `IEmailSender`, bounded requests, and
  provider-neutral delivery outcomes.
- GMA Notifications continues to own durable delivery jobs, attempts, receipts,
  retry policy, destination resolution, and its optional email sink.
- BunkFy owns the concrete SMTP transport, its host composition, and the runtime
  declaration that email verification is available.
- The product root owns the Preview Mailpit service, loopback-only operator
  access, captured-message lifecycle, and deployed rehearsal tooling.
- A private deployment owns its real email provider, credentials, sender-domain
  authentication, suppression handling, delivery evidence, and alerts.

No GMA repository change is planned. A generic SMTP package should be extracted
only after another product needs the same implementation and the contract has
proved stable.

## Invariants

1. SMTP is opt-in and is registered only when explicitly enabled.
2. Ordinary certificate and hostname validation is never bypassed.
3. Production requires encrypted SMTP transport and authenticated submission
   unless a narrow, explicit deployment exception is accepted.
4. Host, port, security mode, credentials, sender identity, and timeouts are
   validated before startup completes.
5. Message content, addresses, credentials, verification codes, and raw provider
   responses are never logged or included in metrics.
6. The delivery idempotency key becomes a deterministic, non-reversible
   `Message-Id`; SMTP still remains at-least-once and does not claim provider
   deduplication.
7. Transient transport failures return bounded retry codes; permanent sender,
   recipient, authentication, and message failures return bounded rejection
   codes. Caller cancellation remains cancellation.
8. Preview mail capture is reachable only from the private Compose network and
   an optional loopback operator port. It is never routed through the public
   web edge.
9. Email-verification availability is a runtime deployment capability, not a
   build-time image claim.
10. Deployed evidence excludes passwords, bearer tokens, email addresses,
    verification codes, invitation secrets, QR secrets, and message bodies.
11. BunkFy's coarse public-IP sensitive-request allowance must accommodate the
    supported three-identity rehearsal and required projection retries. It is
    not a replacement for Auth's durable credential throttles or future
    tenant/actor invitation-delivery quotas.
12. A recognized terminal GMA join source may locate only the existing BunkFy
    Staff-onboarding application for the same currently admitted subject. It
    cannot create or mutate an application, reactivate an access plan, or
    restore applicant data after redaction.
13. Rehearsal cleanup follows BunkFy Staff lifecycle ownership. It must depart
    synthetic Staff through the product policy instead of bypassing that policy
    through direct GMA membership mutation.

## Delivery Slices

1. Add `BunkFy.Adapters.SmtpEmail` under `src/Adapters` with validated
   `Email:Smtp` options, MailKit transport, deterministic message identity, and
   focused success/retry/rejection/cancellation tests.
2. Compose the adapter in API and Worker hosts before the existing GMA
   Notifications email adapter. Add startup and architecture guards for
   disabled, Preview, and Production configurations.
3. Add bounded Mailpit capture to the root Preview contract, enable SMTP and
   notification email delivery through protected environment values, and guard
   its management API from public or non-loopback exposure.
4. Replace the web's compile-time email-verification switch with a runtime
   capability sourced from the composed product.
5. Add one preview rehearsal that creates dedicated identities and topology
   through public APIs, verifies addresses through the captured delivery path,
   invokes the existing deployed invitation and enrollment verifiers, records
   minimized evidence, and performs explicit best-effort cleanup.
6. Preserve same-subject application replay after GMA accepts or closes the
   source. Treat source-plus-subject as the stable application identity: a
   submitted draft remains editable while the source is active, while later
   retries return the current immutable outcome without retaining a PII-derived
   request fingerprint.

## Verification Cadence

- Use focused adapter, option, host-composition, and static Preview tests while
  editing.
- Run the complete non-Docker backend and root gates once after the coherent
  implementation is ready.
- Run SMTP/Mailpit and full Docker gates once at slice end, then batch-fix any
  failures.
- Publish exact dependency and product commits before building one fresh
  retained candidate and executing the deployed rehearsal.

## Done When

- disabled hosts do not require SMTP configuration or an `IEmailSender`;
- enabled hosts fail startup for contradictory or unsafe configuration;
- Preview captures verification mail without exposing Mailpit publicly;
- the invitation and QR verifiers pass against one exact candidate with
  distinct verified applicants and least-privilege property isolation;
- captured evidence states that Mailpit is not real-provider delivery proof;
- temporary join sources and identities have a reviewed cleanup outcome; and
- real-provider/browser delivery remains visibly pending until exercised by the
  private release process.
