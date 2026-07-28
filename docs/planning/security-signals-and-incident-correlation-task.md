# Security Signals And Incident Correlation Task

Status: implementation complete; public repository evidence is retained by CI
Date: 2026-07-28

## Goal

Implement the public portion of company-readiness control SP-009 without
pretending that a public repository can provide BunkFy's private incident
operation.

Security-relevant code paths must emit a small, payload-free record that can be
counted, correlated with existing traces, and routed to a private SIEM later.
The implementation must not add personal data to telemetry, create another
durable product ledger, or make business operations depend on an exporter.

## Ownership

### GMA Framework

GMA owns the reusable signal shape and default telemetry adapter:

- immutable, code-defined signal definitions with bounded code, category, and
  severity;
- a payload-free occurrence record and opaque incident correlation identifier;
- an optional recorder contract with a no-op default for applications that do
  not compose security observability;
- a logger and metric recorder that validates every emitted definition against
  the finite startup registry;
- one bounded counter tagged only by registered signal code, category, and
  severity;
- fail-open exporter behavior while treating unknown or duplicate definitions
  as programmer/configuration errors;
- generic access-control denial signals at the endpoint enforcement boundary.

GMA must not know BunkFy modules, hostel concepts, property/workspace
identifiers, guest data, alert destinations, on-call routes, or incident
classification decisions.

### GMA Auth Module

Auth owns authentication-abuse definitions and emission from durable
authentication controls, initially:

- password-proof attempt limiting;
- exhausted multi-factor challenges when the existing lifecycle exposes a
  reliable terminal transition.

Signals never contain usernames, email addresses, subject ids, target hashes,
IP addresses, user agents, tokens, or failure payloads.

### Public BunkFy

Each existing module owns its own product signal definitions and emission:

- Data Rights: denied approval/execution gates and durable export-action
  failures or sensitive completions;
- Ingestion: adapter ingress scope/quota rejection and provider/control-plane
  anomalies;
- Retention: failed or timed-out scheduled execution;
- product architecture guards: signal records have no arbitrary attributes and
  correlation ids never become metric dimensions.

The product host composes the GMA recorder explicitly. BunkFy does not create a
new Security module or a shared database projection for these records.

### GMA-Skeleton

The Skeleton owns generated-host composition and guidance:

- opt in to the default logger/metric recorder;
- show module-owned static definition registration;
- guard payload-free records and bounded dimensions;
- document that alert routing and retention remain deployment concerns.

### Private Operations

Private infrastructure owns:

- SIEM/log/metric destinations and retention;
- thresholds, deduplication, severity overrides, paging, and on-call ownership;
- evidence preservation, containment and communication playbooks;
- breach assessment/registers, subprocessor contacts, and regulatory/customer
  notification decisions.

Public code emits evidence. It does not decide that an occurrence is a
reportable personal-data breach.

## Audited Baseline

Already present:

- BunkFy rejects personal-data catalogue fields on log, metric, trace, and
  support-bundle surfaces;
- architecture tests reject sensitive/high-cardinality metric dimensions, raw
  exception logging, and unregistered product instruments;
- HTTP Problem Details and safe request logs expose an existing trace id
  without query strings, route values, request bodies, or exception text;
- GMA provides bounded command, query, messaging, task, notification, cache,
  and projection metrics;
- GMA Access Control has centralized endpoint filters and stable denial reason
  codes;
- Auth has durable per-scope authentication-attempt limiting;
- Data Rights stores a durable export audit ledger;
- Ingestion already counts bounded adapter ingress outcomes;
- Retention stores execution state and reports owner failure/timeout outcomes;
- the repository security baseline runs Trivy vulnerability, secret,
  misconfiguration, and licence scans and retains evidence;
- GitHub private vulnerability reporting is enabled for
  `SadPossum/BunkFy`, and the root `SECURITY.md` publishes the coordinated
  disclosure route.

Gaps confirmed by the audit:

- there is no shared payload-free signal envelope or finite runtime registry;
- access denials and auth throttling cannot be queried as explicit security
  signals;
- existing product metrics do not provide an incident correlation record;
- Data Rights, Ingestion, and Retention do not emit one coherent signal shape;
- secret-scan status is visible in CI but no payload-free summary contract is
  retained for tabletop evidence;
- support elevation does not yet exist, so emitting a support-elevation signal
  now would create false assurance.

## Implementation Progress

Published reusable dependencies:

- GMA Framework `c313c97` added the closed signal contract, registry,
  logger/metric recorder, and Access Control emission; `df0c67e` kept
  category/severity semantics typed through the record boundary;
- GMA Auth `504452d` added the bounded password-proof rate-limit signal;
- GMA-Skeleton `64198f0` composes the real recorder in ServiceDefaults and in
  generated applications; `6e3e9de` added the closed, payload-free scanner
  summary and retained it with the existing security evidence.

Implemented in the BunkFy runtime candidate:

- Data Rights emits one bounded approval-denial fact and emits export
  generation completion/failure and successful-download facts only after the
  existing audit entry is durably saved;
- Ingestion maps the already-computed adapter admission decision to bounded
  scope, quota, provider, global-stop, and tenant-suspension facts without a
  second lookup or limiter call;
- Retention emits failed/timed-out execution facts only after the existing
  completion command records the owner result, reusing task correlation or run
  identity;
- shared ServiceDefaults composes the real recorder, the privacy log processor
  permits only the opaque incident correlation field, and architecture guards
  own the exact product signal catalogue and closed evidence shape;
- no product metric was added, so the product telemetry instrument catalogue
  remains unchanged; the counter is the GMA-owned reusable instrument.
- the complete non-Docker backend gate passes solution sync, a zero-warning
  build, migration drift checks, architecture guards, and all eligible tests;
- BunkFy root, Backend, and Web consume the exact GMA-Skeleton `ec0e134`
  security-action candidate, which includes the payload-free summary, bounded
  source/run provenance, protected detailed SARIF, and the existing SBOM;
- product security and release workflows retain the complete action output
  directory. Detailed findings remain workflow evidence and are never copied
  into the aggregate summary.

Exact workflow results are retained by GitHub Actions against the published
candidate commits rather than copied into this implementation plan.

## Invariants

1. A signal contains only definition code, category, severity, occurrence time,
   and an opaque incident correlation id.
2. There is no attributes dictionary, message/detail field, exception, scope,
   tenant, actor, subject, resource, route value, IP address, target hash, or
   product payload.
3. Metric tags are limited to registry-bounded definition code, category, and
   severity. Correlation ids appear only in event logs/evidence records.
4. Definitions are static module-owned facts registered at startup. Unknown or
   conflicting definitions fail validation instead of creating unbounded
   telemetry.
5. HTTP occurrences reuse the active trace id. Task occurrences reuse the task
   correlation id or run id. Other occurrences receive a generated opaque id.
6. Emission/export failures never roll back or change a domain result. Missing
   or duplicate definition registration remains a development/configuration
   error and is covered by startup tests.
7. Signals are telemetry, not domain truth. Existing audit ledgers, task state,
   access decisions, and retention receipts remain authoritative.
8. No new database table, projection, outbox event, or NATS subject is added for
   the first slice.
9. Signal records remain provider-neutral. OpenTelemetry-compatible
   logger/metric output is the public adapter; SIEM integration is private.
10. A support-elevation signal is added only with the SP-008 elevation
    operation that can emit a truthful requested/granted/expired/denied fact.

## Initial Signal Catalogue

| Owner | Signal | Category | Severity | Correlation |
| --- | --- | --- | --- | --- |
| GMA Access Control | subject missing at protected endpoint | authentication | warning | HTTP trace |
| GMA Access Control | scope resolution denied | authorization | warning | HTTP trace |
| GMA Access Control | permission denied | authorization | warning | HTTP trace |
| GMA Auth | password proof rate limited | authentication | warning | active trace or generated |
| BunkFy Data Rights | operation approval denied | privacy | warning | active trace or generated |
| BunkFy Data Rights | export generation failed | privacy | critical | active trace or generated |
| BunkFy Ingestion | adapter scope rejected | integration | warning | active trace or generated |
| BunkFy Ingestion | adapter quota rejected | integration | warning | active trace or generated |
| BunkFy Ingestion | adapter admission provider unavailable | integration | critical | active trace or generated |
| BunkFy Retention | scheduled execution failed/timed out | retention | critical | task correlation/run id |
| GMA security action | repository secret detected | supply-chain | critical | CI run identity |

The final code names are stable lowercase dotted identifiers. The table is a
review inventory, not permission to add dynamic labels.

## Slice 1 - GMA Signal Primitive

1. Add semantic category/severity types, a validated definition, occurrence
   record, receipt, definition-source contract, and recorder contract to
   `Gma.Framework.Observability`.
2. Add an idempotent core registration with a no-op recorder so optional GMA
   packages remain source-compatible.
3. Add the validated registry and default logger/metric recorder to
   `Gma.Framework.Observability.Infrastructure`.
4. Add startup validation, bounded meter/tag/log names, fail-open provider
   behavior, and focused tests.
5. Add generic access-control definitions and record the centralized HTTP
   denial outcomes without exposing subject or scope values.

## Slice 2 - Skeleton And Auth

1. Compose the real recorder in generated ServiceDefaults and update the
   architecture guidance/guards.
2. Register Auth-owned definitions in Auth Application.
3. Emit only threshold/terminal abuse facts, beginning with password-proof
   rate limiting. Do not emit every invalid credential attempt.
4. Add Auth unit tests proving emitted records contain no target material and
   preserve authentication behavior.
5. Publish Framework, Auth, and Skeleton in dependency order before updating
   BunkFy source pointers.

## Slice 3 - BunkFy Module Signals

Work through existing modules sequentially:

1. Data Rights: approval denial and export failure/sensitive completion facts.
2. Ingestion: adapter admission rejection/anomaly facts, reusing the current
   gate decision rather than performing another lookup.
3. Retention: task failure/timeout facts using the existing execution result and
   task correlation/run id.
4. Compose the recorder in all BunkFy hosts and add architecture tests for
   registry ownership, payload-free records, and bounded telemetry.
5. Extend the telemetry catalogue only for product-defined instruments. The
   reusable GMA counter remains framework-owned.

## Slice 4 - Repository Detection Evidence

1. Extend the reusable Skeleton security action with a payload-free summary
   containing scanner category, bounded severity/count, source commit, CI run
   correlation, and scanner status.
2. Keep detailed SARIF protected as CI evidence; never copy matched secret
   material into the summary.
3. Retain the summary with the existing security evidence artifact and verify
   BunkFy consumes the pinned exact action commit.
4. Keep the existing private vulnerability reporting route and security policy
   as the coordinated-disclosure front door.

## Verification Cadence

- Run focused framework, access-control, Auth, and one owning BunkFy module test
  project while implementing each mini-slice.
- Run architecture/static guards after the record shape or telemetry catalogue
  changes.
- Run each repository's complete non-Docker verifier once after its coherent
  candidate is ready.
- Run Docker suites once at the end only if runtime wiring or persistence was
  changed; this task should not introduce persistence.
- Publish exact candidates in dependency order and use GitHub Actions once for
  final evidence, batching any failures before rerunning the failed gate.

## Acceptance

- Duplicate, unknown, malformed, or undefined signal definitions fail focused
  validation.
- Records serialize with only the five allowed fields and never expose an
  extensible payload.
- Metrics contain only bounded registered tags and omit incident correlation.
- Logger failure does not change access, authentication, Data Rights,
  Ingestion, or Retention behavior.
- Scope denials and auth throttling produce explicit signals with trace-safe
  correlation.
- Each selected BunkFy module emits from its existing authoritative decision or
  receipt path without another query or projection.
- The security scan retains a payload-free secret-detection summary plus its
  protected detailed evidence.
- Focused redaction tests and complete final repository gates pass.

## Deferred

- SP-008 support elevation and its requested/granted/denied/expired signals.
- SIEM vendor adapters, alert thresholds, paging, on-call schedules, evidence
  retention, and operational dashboards.
- Breach determination, customer/regulator communication, breach register, and
  subprocessor response.
- Production detection SLO proof and the full tabletop until a hosted
  environment and private operations owner exist.
