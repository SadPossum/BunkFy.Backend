# Workspace Invitation Access Plans Task

Status: planned after Staff access lifecycle orchestration
Date: 2026-07-21

## Goal

Let an authorized workspace manager attach a server-owned Staff/property/access plan to a named invitation or reusable enrollment link. Applicants may review the plan but can never author or broaden authority-bearing fields.

## Ownership

- Organizations owns invitation/enrollment token security, recipient constraints, acceptance, approval, expiry, revocation, rotation, and membership creation.
- AccessControl owns profiles and exact assignment reconciliation.
- Staff owns Staff profiles and property assignments.
- Workspaces owns BunkFy plan state, product validation, provisioning order, retries, and API DTOs.
- GMA Organizations needs a narrow Contracts issuance facade for generic invitation/enrollment creation. It must contain no Staff, property, profile, hostel, or BunkFy vocabulary.

## Plan Rules

- A plan is tenant-scoped and bound to one Organizations source id.
- It stores one active delegable profile id and zero or more active property ids. Reusable links are restricted to configured low-privilege profiles.
- The manager actor must currently possess every delegated permission and property-management authority.
- Applicants receive a sanitized read-only summary. Submission accepts only personal Staff fields.
- Missing, superseded, archived, cross-workspace, or no-longer-delegable plans fail closed.
- Acceptance without a valid plan may create Organizations membership, but membership alone grants no BunkFy operation. Provisioning remains blocked and recoverable.
- Ordinary invitations and reusable links can never grant owner or platform-administration authority.

## Delivery Shape

1. Add the provider-neutral Organizations issuance facade with idempotency, recipient/expiry/approval constraints, and one-time token return semantics.
2. Add Staff Contracts provisioning for an exact property-assignment plan.
3. Persist Workspaces invitation/enrollment plans and bind them to Organizations source ids.
4. Extend Staff onboarding to apply Staff data, property assignments, the membership marker, and the selected access profile in a durable process.
5. Add owner-facing create/list/revoke/rotate/retry endpoints and web flows.
6. Add sanitized applicant preview and approval summaries.

## Verification

- cross-workspace profile/property ids are rejected;
- actor anti-escalation is rechecked at creation and provisioning time;
- token issuance or plan persistence failure cannot grant product access;
- retry and broker redelivery do not duplicate Staff/property/profile assignments;
- revoked, rotated, expired, and superseded sources cannot provision;
- reusable-link maximum-use and approval races retain one authoritative outcome;
- deployed owner/applicant browser smoke covers password and external registration plus link and QR entry.

## Not In This Slice

- custom role editor UX;
- ownership invitations;
- public guest access;
- adapter/service credentials;
- global Auth account administration.
