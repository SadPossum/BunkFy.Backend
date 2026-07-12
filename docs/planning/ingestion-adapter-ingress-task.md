# Ingestion Adapter Ingress Task

Status: implemented and verified

## Goal

Allow an adapter process to submit observations to exactly one configured Ingestion connection without receiving a staff JWT, management permission, database credential, provider secret, or authority over another tenant/property/connection.

This task introduced push/direct ingress. Remote polling now reuses the same connection credential lifecycle through the separately specified [remote adapter lease](remote-adapter-lease-task.md) protocol; assignment, renewal, fencing, and checkpoint ownership remain Ingestion concerns rather than credential concerns.

## Ownership Decision

GMA AccessControl already models `service` subjects, while the current GMA Auth module owns member/session JWTs and does not issue machine credentials. Ingress credentials remain Ingestion-owned because their authority and lifecycle are connection-specific product concepts:

- one credential is bound to one tenant and one adapter connection;
- it can submit observations only, not call management or Admin APIs;
- disabling the connection stops submission even when the credential remains active;
- credentials can overlap during rotation and are revoked independently;
- credential history is retained as operational evidence.

Do not add a generic GMA machine-auth abstraction until at least one other module needs materially the same issuance, rotation, verification, and audit behavior. If that happens, extract the cryptographic token mechanics and service-principal authentication plumbing; keep connection assignment and submission policy in Ingestion.

## Threat Model And Invariants

- Generate at least 256 bits of cryptographic randomness and reveal the complete token once.
- Store only a versioned SHA-256 digest. High-entropy random tokens are not password-equivalent and do not need an intentionally slow password hash; fixed-time comparison is mandatory.
- Include a random credential id in the token so verification performs one bounded lookup and never scans hashes.
- Require TLS outside local development. Never accept credentials in query strings, route values, logs, audit payloads, or persisted command data.
- Bind verification to the normalized tenant id from `X-Tenant-Id` and the route connection id. A valid token cannot choose another scope.
- Return the same unauthorized response for unknown, malformed, expired, revoked, cross-tenant, and cross-connection credentials.
- Limit active credentials per connection and require bounded expiry. Five explicit slots plus a filtered unique index make the cap race-safe; elapsed credentials transition to historical `Expired` state when a new slot is requested. Rotation is create-new, deploy-new, revoke-old.
- Keep last-authenticated telemetry outside the credential concurrency version so normal traffic cannot make revocation preconditions stale.
- Keep ingress under the bounded global IP rate limit, but do not share the login-specific ten-request partition. Authenticated polling, heartbeat, and submission traffic must not lose leases because unrelated adapters share a NAT address. Credential entropy, exact connection binding, body limits, and bounded retries remain the primary controls.
- Validate per-record and aggregate payload limits before persistence. A batch may partially succeed, with one durable result per operation id.
- Replayed operation ids and source identities use the existing receipt deduplication rules. Authentication does not weaken idempotency.
- Push submission cannot advance a polling checkpoint and cannot claim a TaskRuntime run or lease.

## Contracts

Management surfaces:

- list connection credentials without hashes or token material;
- create a named credential with an optional bounded expiry and return its token once;
- revoke a credential with an expected version and explicit confirmation on Admin surfaces.

Ingress surface:

- `POST /api/ingestion/adapter-ingress/connections/{connectionId}/observations`;
- `X-Tenant-Id` plus `Authorization: BunkFy-Adapter <token>`;
- a bounded collection of observation records using the shared adapter protocol limits;
- HTTP success with accepted, duplicate, or rejected result per operation after authentication;
- no user JWT or AccessControl grant is accepted as a substitute for the adapter credential.

## Persistence

An Ingestion-owned credential stores tenant, connection, label, digest algorithm/version, digest, status, expiry, create/revoke actor and timestamps, last authentication timestamp, and optimistic management version. Foreign keys and query filters preserve tenant ownership. Indexes support connection listing, active-count enforcement, expiry checks, and credential-id authentication.

## Acceptance Checks

- credential token material is visible once and absent from all subsequent reads;
- hashes, tokens, and Authorization values are absent from logs/audit contracts;
- malformed, unknown, expired, revoked, wrong-tenant, and wrong-connection credentials all fail closed;
- a valid credential submits an observation without a member JWT;
- exact replay returns `Duplicate` without a second raw payload or product effect;
- disabled connections reject otherwise valid credentials at the existing command boundary;
- rotation supports two active credentials and revoking one does not disable the other;
- active-credential limits, expiry bounds, batch count, aggregate bytes, and request bytes are enforced;
- management API, Admin API, and Admin CLI use a dedicated credential-management permission;
- migration drift, fast tests, focused real PostgreSQL/MinIO tests, and the complete Docker suite pass.

## Deferred

- generic GMA service credential issuance;
- OAuth client credentials, workload identity federation, and mTLS;
- fleet-wide remote polling discovery and claim-any scheduling;
- credential-specific network allowlists;
- provider webhooks that require provider-origin signature verification in addition to BunkFy credentials;
- distributed credential verification caches, added only after measured database pressure.
