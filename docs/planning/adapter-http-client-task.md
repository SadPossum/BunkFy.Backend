# Adapter HTTP Client Task

Status: implemented and verified

## Goal

Provide a dependency-light adapter-side SDK for standalone push adapters to submit `AdapterObservedRecord` batches through BunkFy's authenticated Ingestion ingress without referencing Ingestion application, persistence, or host projects.

## Ownership

- `BunkFy.Adapter.Abstractions` owns the transport-neutral push sink and versioned wire envelope because both server and adapter processes need them.
- `BunkFy.Adapters.Http` owns HTTP request construction, adapter-token acquisition, transient retry, response parsing, and transport options.
- Ingestion continues to own credential issuance, authorization, durable receipts, deduplication, and product dispatch.
- Provider adapters own acquisition/parsing only and receive an `IAdapterPushObservationSink`.

Do not move this client into GMA. Its route, authorization scheme, observation envelope, and acknowledgement semantics are BunkFy adapter protocol concerns. Generic HTTP resilience remains a platform/library concern only if multiple unrelated protocols prove identical behavior.

## Security And Reliability Invariants

- Accept credentials through an async token-provider interface so daemons can rotate secrets without rebuilding the client.
- Never put tokens in options, base addresses, query strings, payloads, logs, exception messages, or default `HttpClient` headers.
- Send `Authorization: BunkFy-Adapter <token>` per request and `X-Tenant-Id` from validated options.
- Require HTTPS. Permit HTTP only for an explicitly enabled loopback endpoint used by local tests/development.
- Reject base addresses containing user info, query, or fragment components.
- Reuse shared limits: 100 unique operation ids, 4 MiB per record, and 16 MiB decoded aggregate payload.
- Rebuild request/content for each attempt. Never reuse a sent `HttpRequestMessage`.
- Retry only transport failures, client-side timeouts, HTTP 408, 429, 502, 503, and 504.
- Respect bounded `Retry-After`; otherwise use bounded exponential backoff with optional jitter.
- Do not retry authentication, validation, payload-size, routing, or other permanent HTTP failures.
- Retrying is safe because every record has a stable operation id and Ingestion returns durable duplicates.
- Bound response buffering and reject missing, duplicate, foreign, or extra acknowledgement operation ids.
- Preserve per-record `Accepted`, `Duplicate`, and `Rejected` results. Do not automatically retry rejected records.
- Propagate caller cancellation immediately.

## Public API

- `IAdapterPushObservationSink.SubmitAsync(records, cancellationToken)` in the abstraction package.
- `IAdapterIngressTokenProvider.GetTokenAsync(cancellationToken)` in the HTTP package.
- immutable validated HTTP options containing service base address, tenant, connection, attempts, delays, jitter, and loopback policy;
- `AdapterHttpIngressClient`, usable directly or through `IAdapterPushObservationSink`;
- an explicit static token provider for simple/local processes, with a redacted string representation.

## Acceptance Checks

- the HTTP package references only `BunkFy.Adapter.Abstractions` and framework assemblies;
- request path, tenant header, authorization scheme, JSON envelope, and acknowledgement mapping are exact;
- credentials never appear in URI/body/exceptions and are fetched again for each retry attempt;
- transient statuses and transport exceptions retry within configured bounds;
- 401 and other permanent statuses fail once;
- malformed/oversized submissions fail before obtaining a token or sending a request;
- malformed or mismatched acknowledgements fail closed;
- exact retry after an uncertain server outcome returns duplicates rather than new effects;
- a real TestServer/PostgreSQL/MinIO scenario uses the SDK for accepted and duplicate ingress;
- solution build, dependency guards, fast tests, migration drift, and complete Docker tests pass.

Implemented in `BunkFy.Adapters.Http` with focused transport tests and a Docker-backed Ingestion operations proof. The client is intentionally dependency-light: its only project dependency is `BunkFy.Adapter.Abstractions`.

## Deferred

- fleet-wide remote polling discovery and claim-any scheduling;
- OAuth client credentials, workload federation, and mTLS token providers;
- streaming/multipart payload transfer above the bounded inline protocol;
- distributed circuit breakers and metrics until real adapter traffic provides operating thresholds;
- provider-specific webhook signature verification.
