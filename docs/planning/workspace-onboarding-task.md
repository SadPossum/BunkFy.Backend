# Workspace And Staff Onboarding Task

Status: core delivered; expansion backlog retained below
Date: 2026-07-14

## Delivered Core

- global public account registration with client-side password confirmation;
- reusable Organizations ownership of workspace catalog, memberships, invitations, enrollment links, lifecycle, concurrency, and provider-specific migrations;
- workspace creation, selection, rename, member inspection, membership suspension/resume, and ownership transfer;
- single-use recipient-bound invite links plus bounded reusable QR enrollment links;
- invitation-aware registration with explicit post-registration acceptance and projection-readiness handling;
- BunkFy Staff identity projection, self-service profile editing, and tenant-scoped owner governance access;
- deny-by-default product access, permission-filtered navigation, and a constrained front-desk baseline installed only after Staff provisioning succeeds;
- API, worker, migration, Admin API/CLI, OpenAPI, docs, and local preview composition.

The secure applicant-supplied Staff enrollment baseline is implemented by [Secure Staff Enrollment](secure-staff-enrollment-task.md). The phases below remain the durable direction. Configurable BunkFy access profiles, manager-pre-created Staff/property assignment plans, offboarding orchestration, production migration tooling, and deployed multi-account smoke are follow-on slices, not hidden claims of this delivery.

## Goal

Deliver production-shaped public account registration, workspace creation and management, multi-workspace membership, and staff invitation/onboarding without weakening BunkFy's management-only product boundary.

A person can register once, create a workspace, become its first owner, create the first property, invite staff through a link or QR, and later switch among every workspace they actively belong to. Existing accounts can accept invitations without duplication. New invited users can register without losing the invitation, review their Staff profile, and complete acceptance in the same flow.

The durable architecture is defined in [Workspaces, Identity, And Staff Onboarding](../architecture/workspaces-and-onboarding.md).

## Long-Term Completion Criterion

This task is complete only when:

- GMA can compose global Auth identity with active tenant-scoped product modules in one host;
- a reusable Organizations module owns organizations, memberships, invitations, and enrollment links;
- required GMA extensions preserve module independence and default-deny failure behavior;
- BunkFy enables public registration but exposes no PMS data to accounts without active membership and permissions;
- workspace create, list, select, update, suspend/archive, owner transfer, and member management are operational;
- Staff invitation links and one-person QR flows provision Staff correlation, property assignments, and constrained access profiles;
- reusable enrollment QR behavior is separately governed and cannot grant owner/high privilege automatically;
- coordinated suspension/departure removes workspace access without disabling unrelated global identity access;
- current tenant-scoped Auth data has a documented and tested migration/reconciliation path;
- public API, Admin API, Admin CLI, worker/process recovery, OpenAPI, migrations, docs, guardrails, and web UI agree;
- focused, all-up, Docker-backed, security, migration, and browser verification is green.

## Delivery Rules

- Finish one reusable/domain slice before opening the next.
- Do not add BunkFy semantics to GMA Framework, Auth, Tenancy, AccessControl, or Organizations.
- Reusable modules reference no other reusable module implementation. Cross-module behavior lives in GMA Extensions.
- BunkFy product coordination lives in a BunkFy extension/process package and references public contracts only.
- Every state-changing workflow is optimistic-concurrency controlled, idempotent, auditable, and recoverable after process restart.
- No step is considered complete from a build alone; prove its runtime, isolation, denial, and failure behavior.

## Phase 1: GMA Scope And Identity Foundation

### Required behavior

- Preserve `AuthProfile.ScopeAware()` for projects that want scope-local accounts.
- Make `AuthProfile.Global()` composable in a host that also enables Tenancy for other modules.
- Separate Auth's fixed identity-store partition from the active product tenant scope without global mutable scope overrides.
- Keep global Auth register/login/refresh/OIDC endpoints independent of workspace selection.
- Issue stable global subject ids and sessions. Tenant/workspace ids are not embedded as permanent identity authority.
- Keep account-security notifications and member administration addressable in the global identity scope.
- Add composition metadata/guards that reject ambiguous or unsupported profile combinations.

### Proof

- focused Auth and Framework unit/architecture tests;
- a composed test host with Tenancy enabled, Auth global, and a tenant-scoped sample module;
- two workspaces can use the same global subject without duplicate Auth members;
- selecting another tenant cannot read data without grants;
- password, refresh, OIDC handoff/link, email verification, disablement, and security notifications still work;
- scope-aware Auth compatibility tests remain green.

## Phase 2: Reusable Organizations Module

Create a dedicated GMA source repository mounted as `gma/modules/organizations` with package-local source roots, solution, CI, migrations, docs, and tests matching existing reusable-module standards.

### Domain model

- `Organization`: id, name, slug, status, version, created/changed provenance.
- `OrganizationMembership`: organization, subject, governance kind, status, version, joined/changed provenance.
- `OrganizationInvitation`: organization, inviter, optional recipient constraint, token digest/version, expiry, status, accepted subject, version.
- `OrganizationEnrollmentLink`: organization, token digest/version, expiry, maximum/current claims, approval mode, status, version.
- `OrganizationJoinRequest` when approval is required.

### Invariants

- organization ids are immutable normalized scope ids; slugs are mutable display/routing identifiers and never partition keys;
- one current membership per `(organization, subject)`;
- at least one active owner for every active organization;
- normal invitations cannot create owners;
- one invitation claim winner under concurrency;
- token plaintext is never persisted or emitted in events;
- expired/revoked/superseded artifacts cannot be accepted;
- reusable-link claim limits are race-safe;
- organization suspension blocks ordinary activation;
- cross-tenant membership discovery returns only the caller's memberships.

### Surfaces

- public authenticated organization creation/list/current/update;
- invitation preview, create/list/revoke/reissue/accept;
- member list, suspend/resume/remove and explicit owner transfer;
- enrollment-link create/list/disable/rotate and join-request approve/reject;
- Admin API and Admin CLI recovery/inspection through the same application behavior;
- export/rebuild seams only where another module needs a repairable projection.

### Persistence and events

- provider-neutral domain/application;
- PostgreSQL and retained supported-provider migrations following GMA policy;
- global catalog queries with explicit organization authorization;
- outbox/inbox and PII-minimized versioned integration events;
- bounded retention/redaction for expired invitation recipient data and audit facts.

## Phase 3: Reusable Extensions

### Auth and Organizations

- preserve invitation intent through password registration, login, and external provider redirects;
- accept invitations for existing accounts without creating duplicates;
- require verified matching email for recipient-bound invitations;
- expose no password/OAuth/token material to Organizations;
- make retries after account creation or callback loss safe;
- support open public registration followed by create-or-join onboarding;
- keep BunkFy/product access profile data out of the extension.

### Organizations and Tenancy

- validate organization existence/lifecycle before activating ordinary tenant endpoints;
- require active membership for user subjects;
- expose explicit service/system/admin policy hooks;
- ensure membership suspension/removal denies immediately even before grant cleanup;
- cache only safe, bounded membership decisions and invalidate them on lifecycle facts.

### Organizations and AccessControl

- create tenant-scoped owner governance assignments idempotently;
- prevent ordinary member/invitation flow from assigning owner wildcard access;
- remove/deactivate scoped assignments during membership removal according to policy;
- preserve at least one effective workspace owner;
- provide a constrained delegation seam that products can map to safe access profiles.

### Organizations and Notifications

- optional invitation delivery through generated URLs from allowlisted origins;
- membership/invitation security notifications without leaking token plaintext;
- delivery failures do not roll back invitation creation and remain retryable/observable.

## Phase 4: Skeleton And Tooling Alignment

- mount Organizations only when selected by the app generator;
- mount/register each extension only when all required source modules are selected;
- update source-root properties, bootstrap/update/status scripts, solution sync, package ownership checks, docs, and generator manifests;
- add architecture guards for mixed global/tenant composition and extension boundaries;
- generate and build a global-Auth plus Organizations/Tenancy sample app;
- generate and build a scope-aware Auth app to prove compatibility.

## Phase 5: BunkFy Product Integration

### Workspace policy

- BunkFy labels Organizations as workspaces;
- public verified accounts may create workspaces by default;
- deployment configuration may disable self-service creation;
- initial property creation is a resumable setup step after workspace provisioning;
- platform administration remains separate from workspace ownership.

### Staff onboarding process

Create a BunkFy-owned durable process module, not a second employment aggregate. The implemented baseline owns temporary applicant profile data and coordination state:

- organization and Organizations source id;
- authenticated applicant subject and verified contact email;
- proposed Staff profile until terminal completion/rejection;
- bound claim id/version when approval is required;
- provisioned Staff member id;
- step/version/error/retry/audit facts.

The process uses a narrow Staff provisioning contract and public AccessControl behavior through a constrained product policy. It never matches Staff automatically by email. Requested property assignments and named access profiles remain future server-owned plan fields, not browser authority.

### Access profiles and delegation

- define stable BunkFy profile codes separately from job titles;
- map profiles to explicit role/scope assignment plans;
- authorize which profiles the inviter may grant;
- require separate permission and confirmation for owner transfer/high privilege;
- reject stale property/profile plans rather than silently broadening access;
- keep permissions out of JWTs and invitation URLs.

### Offboarding

- Staff suspension/departure starts a durable access-removal process;
- membership denial takes effect before cleanup completion;
- global Auth account and other workspace memberships remain active;
- resume/rejoin is explicit and cannot replay superseded grants;
- owner departure requires prior transfer.

## Phase 6: Web Product Experience

### Authentication and onboarding

- remove Workspace ID from login and registration;
- enable password registration and enabled external providers globally;
- after registration route to email verification when needed;
- after verification show `Create workspace` and pending invitations;
- accounts with memberships land in the last valid workspace or a workspace picker;
- accounts without memberships cannot render PMS routes.

### Workspace setup and management

- create workspace with name and generated/editable slug;
- show durable provisioning/setup progress and retryable failures;
- create the first property as the next step;
- workspace switcher appears only when useful and uses tab-local/route state;
- workspace settings include profile, members, invitations, ownership, and lifecycle controls;
- rename the current property-selection `WorkspaceProvider` to `PropertyProvider`.

### Staff invitation

- invite from Staff using a pre-created/new profile;
- select properties and one delegable access profile;
- display invitation lifecycle and safe resend/revoke/reissue controls;
- render a one-person QR from the single-use invitation URL;
- show the exact signed-in account and workspace before acceptance;
- show provisioning progress instead of pretending eventual work completed synchronously.

### Reusable enrollment

- separate UI and warning language from named invitations;
- configure expiry, maximum uses, approval, and low-privilege profile;
- support QR rendering and rotation/disable;
- never auto-select owner/high privilege.

## Migration And Rollout

1. Add global identity composition and Organizations while current tenant-scoped login still operates behind compatibility configuration.
2. Add migration preflight/reporting for duplicate usernames and external identities across tenant scopes.
3. Reconcile collisions explicitly; never merge by email automatically.
4. Migrate eligible Auth rows to the global partition while preserving member ids and Staff links; revoke old sessions.
5. Backfill organizations/memberships for retained tenants and their intended owners through an explicit confirmed tool.
6. Enable global registration/onboarding endpoints and deploy the web flow.
7. Remove Workspace ID login only after membership discovery, workspace activation, and owner recovery paths are proven.
8. Retain a documented clean-reset path only for disposable local/preview deployments.

Every phase must support rollback or forward repair without losing product records. Database migrations never seed unknown owners or infer membership from an email match.

## Security Verification

- unauthorized accounts cannot enumerate workspaces, invitations, members, or tenant data;
- changing tenant header/route never bypasses membership and permission checks;
- invitation/enrollment tokens satisfy entropy, hashing, purpose separation, expiry, revocation, constant-time comparison, and redaction rules;
- token claim, resend, revoke, rotate, and maximum-use races have one valid winner;
- invite-bound email mismatch and unverified email fail closed;
- return URLs and link origins are allowlisted against open redirect/Host-header injection;
- owner and access-profile delegation cannot escalate privilege;
- membership removal blocks stale JWT sessions immediately at the workspace boundary;
- logs, traces, metrics, errors, events, notifications, task payloads, and audit data contain no credentials or token plaintext;
- CSRF, cookie, referrer, rate-limit, and browser-history behavior is tested on browser endpoints;
- cross-workspace and cross-property isolation is proven with real tokens and persisted grants.

## Performance And Operations Verification

- membership discovery is indexed by subject and paginated;
- tenant activation does not perform an unbounded query or one authorization call per row;
- membership checks are cacheable only with immediate lifecycle invalidation and short bounded fallback;
- organization/invitation cleanup is bounded, retryable task work;
- projections and process state have repair/replay paths;
- outbox/inbox retention remains bounded;
- concurrent create/accept/transfer/offboard paths are covered against real PostgreSQL;
- worker/broker/email outages preserve accepted work and recover without duplicate grants;
- metrics expose rates, failures, lag, retries, token abuse, and stuck provisioning without tenant/PII leakage.

## Repository-Wide Proof Gate

- package-local restore/build/tests for every changed GMA repository;
- GMA Framework, Auth, Organizations, Extensions, and Skeleton architecture/guard suites;
- generated-app composition tests for global and scope-aware Auth profiles;
- BunkFy deterministic solution generation and zero-warning all-up build;
- all provider migration drift checks and production-shaped upgrade tests;
- all non-Docker tests and the complete Docker suite;
- real PostgreSQL concurrency/isolation and NATS restart/recovery scenarios;
- transitive vulnerability and source-package ownership audits;
- OpenAPI generation and frontend contract regeneration drift checks;
- Playwright desktop/mobile onboarding, workspace switching, invitation, QR, denial, and offboarding checks;
- recursive submodule cleanliness, recorded-pointer, and `origin/dev` head checks;
- `git diff --check` in every changed repository.

## Explicitly Deferred Until The Core Is Proven

- enterprise SAML/SCIM and verified-domain auto-enrollment;
- billing/subscription gates for workspace creation;
- custom domains and workspace-specific identity providers;
- temporary elevation, approval chains, and break-glass access;
- bulk staff invitations/imports;
- legal retention/anonymization policy beyond bounded invitation cleanup;
- organization hierarchies, portfolios, franchises, and cross-workspace reporting;
- mobile deep-link/native QR handling.

These are deferred behavior, not permission to close the extension seams they will require.
