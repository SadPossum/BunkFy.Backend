# Personal-Data Output Sink Enforcement Task

Status: implemented and locally verified; hosted exporter evidence pending

## Objective

Complete the outbound-sink portion of company-readiness control SP-001. The
checked-in personal-data catalogues already classify BunkFy persistence and
contract surfaces. This slice must additionally make it difficult for
catalogued personal data, provider payloads, secrets, or exception text to
escape through logs, traces, metrics, operational notifications, or support
diagnostics. Generic retry and worker failure records must follow the same rule
because they are persisted and later exposed through administration surfaces.

This is a production-foundation task. It does not approve legal purposes,
retention periods, countries, telemetry vendors, or access to a hosted
observability system.

## Proven Baseline

Eight source catalogues currently cover Guests, Reservations, Ingestion,
Staff, Workspaces, Inventory, Properties, and Operations Notifications.

- No product field is allowed on `Log`, `Metric`, `Trace`, or `SupportBundle`.
- Only the fifteen bounded Operations Notifications fields are allowed on the
  `Notification` surface.
- BunkFy-owned metrics are emitted through GMA's bounded operation, result,
  provider, status, and error-code dimensions.
- BunkFy product code does not create custom activities or metric instruments.
- Operations Notifications uses sealed typed navigation payloads and excludes
  direct identity, contact, preference, demographic, and free-text fields.

The remaining gap is that catalogue validation describes intended surfaces;
it does not inspect emitted telemetry. Raw exception objects and automatic HTTP
instrumentation can still contain messages, route values, query strings, or
remote URLs that were never declared as output fields.

## Ownership

### GMA

- Generic framework and module infrastructure logs bounded operation metadata
  and exception type names, never raw exception objects, messages, stack
  traces, or `ToString()` output.
- Generic request diagnostics produce a useful failure signal without
  attaching an exception object.
- Generic task, inbox, and outbox failure records persist a stable failure code
  and bounded exception type instead of arbitrary exception or contributor
  text.
- Reusable architecture tests prevent raw exception logging from returning.
- Generic metrics continue to accept only normalized, bounded dimensions and
  never tenant, subject, user, resource, or correlation identifiers.

GMA must not know BunkFy field names, hospitality purposes, notification
payloads, country rules, or retention decisions.

### BunkFy

- The product-wide guard loads every authoritative source catalogue and
  enforces the closed sink policy.
- BunkFy source may log stable identifiers and bounded codes only where the
  owning task explicitly permits them; it may not log raw exception objects or
  exception text.
- Exported HTTP spans remove concrete paths, query strings, full URLs, and
  exception events. Route templates, methods, status, service identity, and
  trace correlation remain available.
- Product configuration fails closed when a production logging mode would
  re-enable raw request exceptions.
- Operations Notifications remains the only personal-data notification sink
  and retains its exact typed-payload tests.

### Deployment

- The telemetry backend, access policy, regional destination, retention,
  alert routing, and deletion process are deployment-owned choices.
- Production support bundles remain disabled until a separate scrubbed,
  customer-authorized workflow exists.
- Staging canary evidence must prove representative identity, contact,
  free-text, token, and provider-error values do not arrive at configured
  exporters.

## Delivery Slices

1. Replace raw exception logging in composed GMA and BunkFy paths with bounded
   exception type names and add repository guards.
2. Make generic request failure logging exception-safe and preserve bounded
   status, route-template, module, scope, duration, and trace signals.
3. Add a BunkFy OpenTelemetry processor and instrumentation configuration that
   removes concrete URL/path/query and exception details before export.
4. Add one product-wide catalogue guard that enforces the closed sink policy
   across every source catalogue and rejects unreviewed catalogues.
5. Add focused capture tests for logs, traces, metrics, and notification
   envelopes using unmistakable canary values.
6. Sanitize persisted task, inbox, and outbox failure state and prove handler
   or contributor canaries cannot reach administration-visible records.
7. Run focused, complete non-Docker, Docker, migration, package, source-set,
   and exact-commit CI proof before marking this slice published.

## Acceptance

- Every authoritative source catalogue is discovered exactly once by the
  product-wide guard.
- No catalogue field may use `Log`, `Metric`, `Trace`, or `SupportBundle`.
- Only the Operations Notifications catalogue may use `Notification`, and its
  members remain reflection-checked against the sealed payload/envelope set.
- Composed GMA and BunkFy production source contains no `ILogger` call that
  supplies an exception object, exception message, stack trace, or exception
  `ToString()` output.
- Failed CQRS, messaging, task, cache, notification, administration, and HTTP
  paths still emit bounded failure signals containing an exception type.
- Persisted task, inbox, and outbox failure records contain stable failure
  codes and bounded exception types, never exception messages or contributor
  error text.
- Exported server and client spans contain no concrete URL path, query string,
  full URL, or exception event details.
- Metrics remain bounded and contain no canary identity, contact, free-text,
  token, scope, subject, user, resource, or correlation value.
- Notification capture contains only the reviewed envelope and typed payload
  members and does not copy canary source text.
- Production startup rejects any configuration required by this policy that is
  missing or unsafe.

## Non-Goals

- Detecting arbitrary personal data by guessing from string contents.
- Treating redaction as permission to log request bodies, command objects,
  provider payloads, secrets, or guest/staff fields.
- Adding product-specific semantics to GMA.
- Building SP-002 rights workflows, SP-003 retention execution, SP-009 hosted
  incident operations, or SP-010 country activation in this slice.
- Claiming production readiness from local tests without deployed exporter and
  access-control evidence.

## Deferred Production Evidence

- Select and approve the hosted telemetry destination and region.
- Configure least-privilege observability access and audited support access.
- Choose telemetry retention and deletion periods.
- Run canary verification against the real collector, log store, metric store,
  trace store, alert payloads, and any crash-reporting integration.
- Repeat the check after collector, exporter, SDK, proxy, or hosting changes.

## Local Verification

- Final composed build passed with zero warnings and zero errors.
- Solution composition, source-package checks, and every PostgreSQL and SQL
  Server migration drift check passed.
- The complete non-Docker verification suite passed, including 1,018 GMA
  Framework tests and all BunkFy architecture, governance, module, and host
  tests.
- All 33 Docker-backed integration tests passed.
