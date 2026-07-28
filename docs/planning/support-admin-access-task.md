# Support And Admin Access Task

Status: local verification complete; exact candidate CI pending
Date: 2026-07-28

## Goal

Prepare BunkFy's public production boundary so company access can be temporary,
strongly authenticated, workspace or property scoped, payload-free in security
telemetry, and reviewable. The public repositories provide enforcement hooks
and evidence. They do not implement the private support desk or pretend that a
generic role grant is an approved support request.

## Audit Summary

- The Admin API is a separate host, requires a private network in the Hosted
  profile, authenticates through GMA Auth, and routes module operations through
  `AdminApiExecutor`.
- GMA Administration already records bounded actor, tenant, operation,
  permission, outcome, error-code, and timestamp audit facts without request or
  guest payload.
- GMA AccessControl now authorizes standing or finite role assignments,
  evaluates expiry in persisted reads, retains revocation history, and exposes
  a conjunctive product-policy seam.
- BunkFy rejects non-user scoped-profile assignments. Company operators
  therefore need a compatibility-role lease rather than a product staff
  profile.
- GMA Administration now accepts host-resolved resource-scope segments, sends
  them through exact AccessControl authorization, and preserves the canonical
  scope in its durable, filterable audit.
- The Admin API now applies one provider-neutral recent strong-authentication
  requirement before every operation and audits assurance denials.
- Data Rights admin projects expose no operations and are no longer composed
  into BunkFy's Admin API/CLI or their migration sets. Customer-facing export
  remains in the product API behind approved-case selection and step-up
  assurance.

## Architecture Boundary

### GMA Framework

- keep authentication context names and acceptable freshness product selected;
- expose one reusable assurance evaluation path for claims plus an injected
  clock;
- let GMA Administration apply an optional host-wide assurance requirement
  before an admin action while still recording the denied operation;
- preserve the RFC 9470 challenge used by endpoint assurance.

### GMA AccessControl

- implement the temporary role-assignment lease described in
  `docs/temporary-role-assignment-leases-task.md`;
- expose a generic assignment-policy contract;
- keep authorization expiry-aware and independent of a cleanup worker;
- retain bounded lifecycle evidence without support-specific fields.

### BunkFy Public Code

- require recent MFA or two-step assurance for every Admin API operation;
- register a role-assignment policy for company admin actors that denies global
  grants, standing grants, arbitrary roles, permission sets above BunkFy's
  company-support ceiling, and grants longer than the configured maximum;
- recognize only BunkFy's workspace/property access-scope grammar;
- add exact property authorization scope to property-addressed admin actions
  through a generic Administration resource-scope extension;
- emit payload-free requested/granted/denied/revoked/expired support-elevation
  signals only when the corresponding lifecycle fact exists;
- keep actor ids, guest data, request reasons, ticket text, and raw scope values
  out of signals and observability.

### Private Operator Plane

- authenticate named company identities and prohibit shared accounts;
- own ticket, reason, requester, approver, expiry, emergency review, customer
  history, and customer communication;
- request only public temporary grants and revoke them at workflow end;
- own alert routing and reconciliation of expired grants;
- prevent routine database and object-store access;
- require the customer to select and preview guest payload before any support
  export contains it.

## Delivery Order

1. [x] Add the reusable GMA assurance evaluator and optional Administration gate.
2. [x] Add AccessControl temporal role-assignment leases and product policy seam.
3. [x] Configure BunkFy Admin API assurance and workspace/property grant policy.
4. [x] Add generic resource-scoped admin authorization, then opt property-addressed
   BunkFy operations into exact property scopes.
5. [x] Add payload-free support-elevation signals and clock-controlled evidence.
6. [x] Audit DataRights/Admin export paths and deny company payload export until a
   private customer selection/preview authorization is presented.
7. [ ] Run one coherent non-Docker slice gate, then relational/Docker proof once,
   then exact candidate CI once.

## Acceptance Evidence

- stale, password-only, or missing-assurance Admin API tokens receive a
  standards-shaped challenge and the denial is audited;
- a company admin actor cannot receive a global, standing, expired, overlong,
  arbitrary-role, or above-ceiling BunkFy grant;
- the evaluated role-permission snapshot is rechecked atomically and an active
  temporary role cannot gain permissions;
- a valid lease authorizes only its exact workspace/property scope and stops at
  expiry without a sweep;
- cross-workspace and cross-property attempts fail before module data access;
- grant, denial, and revocation signals contain stable opaque correlation facts
  but no actor id, raw scope, guest payload, ticket text, or free-form reason;
- the finite grant and retained assignment history preserve the exact expiry,
  while authorization fails at the boundary without a sweep;
- administrative support export cannot return guest payload without the
  separate customer-controlled selection/preview proof;
- public documentation names the private controls still required and makes no
  hosted-readiness claim before they exist.

## Local Verification

- the GMA Framework solution builds without warnings and all 1,080 tests pass;
- Administration passes boundary, build, migration-drift, package, and 31 unit
  checks, plus all 10 PostgreSQL and SQL Server integration tests;
- AccessControl passes boundary, build, migration-drift, package, and unit
  checks; all 16 relational tests pass across both providers, including
  existing-schema upgrades, temporary leases, exact scope hashes, and
  concurrent final-owner protection with retained revocation history;
- BunkFy's synchronized 283-project composition builds without warnings, every
  migration drift check passes, all 70 architecture guards pass, and the
  remaining host integration assemblies pass;
- exact published-commit CI remains pending and is intentionally run once
  after dependency-order publication.

## Explicitly Deferred

- the private support request and approval application;
- customer-visible support history and communication;
- emergency/break-glass operational policy and review ownership;
- SIEM destinations and on-call routing;
- direct cloud IAM, database, object-store, and device controls;
- any production enablement of company data access.
- a standalone one-shot expired signal until AccessControl or private
  reconciliation owns a durable expiry-observation fact; emitting one from
  ordinary authorization reads would be duplicate-prone and misleading.
