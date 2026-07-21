# Production Authentication Admission And Assurance Task

Status: implemented and verified

## Objective

Close company-readiness item SP-006 without moving BunkFy product policy into GMA. A hosted deployment must declare who may create accounts and workspaces, verified identity must gate the configured product admissions, and privileged operations must require explicit authentication assurance.

This slice does not make guest-facing access possible. An account without an active workspace membership remains outside every tenant-scoped PMS surface.

## Current Reusable Baseline

The published GMA revisions already provide:

- enumeration-safe password recovery with single-use hashed challenges and session revocation;
- TOTP enrollment, recovery codes, MFA login challenges, and authenticator administration;
- persisted `acr`, `amr`, and `auth_time` session evidence plus password step-up;
- provider-neutral endpoint assurance requirements and RFC 9470 challenges;
- durable database-backed authentication-attempt limiting across API replicas;
- per-request organization-membership admission for tenant-scoped endpoints;
- invitation recipient verification through the Auth/Organizations extension.

Those capabilities remain in their current owners. No new Auth abstraction is justified by this task.

## Ownership

### GMA Framework and Auth

- Keep generic authentication evidence, step-up enforcement, credentials, authenticators, recovery, throttling, and sessions.
- Do not know BunkFy workspaces, hostel roles, adapter credentials, exports, or launch countries.

### GMA Organizations

- Keep the existing generic organization-admission policy seam.
- Add only a neutral identity-verification admission error that any product policy can return and the Organizations API maps to `403`.

### BunkFy

- Declare account-registration and workspace-creation modes for every Production API deployment.
- Verify that declared modes agree with the composed Auth and Organizations settings.
- Optionally require a verified Auth email before self-service workspace creation.
- Select authentication contexts and freshness for each privileged product operation.
- Prove membership suspension/removal denies tenant authorization immediately, before access-token expiry and independently of asynchronous AccessControl cleanup.

## Admission Configuration

`BunkFy:WorkspaceAdmission` owns these product choices:

| Setting | Values | Meaning |
| --- | --- | --- |
| `AccountRegistration` | `Unspecified`, `Public`, `Disabled` | Whether anonymous users may create a global Auth account. Registration alone grants no PMS access. |
| `WorkspaceCreation` | `Unspecified`, `SelfService`, `Disabled` | Whether an authenticated account may create its own workspace. Invitations and enrollment are separate flows. |
| `RequireVerifiedEmailForWorkspaceCreation` | Boolean | Whether self-service creation requires a verified active Auth email. |

Production rejects either `Unspecified` mode and rejects contradictions with `Auth:SelfRegistration` or `Organizations:SelfServiceCreationEnabled`. Development and preview must still declare their choices explicitly in their own configuration; preview remains unsuitable for real guest data while email delivery is disabled.

Invitation-linked registration remains possible when account registration is public. Email-addressed invitation acceptance already requires ownership of the matching verified address. Enrollment-link verification and applicant profile approval remain part of SP-013 rather than being hidden inside this task.

## Delivery Slices

1. Add the neutral Organizations admission error and focused contract/API tests.
2. Add BunkFy admission options, startup validation, verified-email workspace creation policy, configuration, and focused tests.
3. Define named BunkFy assurance requirements and apply them to workspace ownership/membership, access-profile administration, adapter credential control, and later privacy/export operations.
4. Add end-to-end proof for step-up challenges, successful fresh/MFA access, stale-token denial, and immediate membership offboarding denial.
5. Run provider migration checks, fast/all-up tests, Docker preview checks, OpenAPI drift checks, and security scans before publication.

## Acceptance

- A Production API cannot start with an undeclared or contradictory admission policy.
- Public account registration never grants tenant access by itself.
- When enabled, verified-email workspace creation denies missing, unverified, malformed, and cross-scope identities without revealing unrelated account state.
- Invitation registration and acceptance continue to work through their existing verified-recipient boundary.
- Every currently supported privileged operation is either protected by a documented assurance requirement or explicitly classified as ordinary operational work.
- Suspended or removed members cannot use an otherwise unexpired token against tenant endpoints.
- BunkFy consumes only published GMA commits and all repository/submodule guards pass.

## Implementation Record

Published reusable dependencies: GMA Organizations `5e7ce93`, GMA
AccessControl `3fd0583`, and GMA-Skeleton composition `85c575c`.

- Production startup now rejects unspecified or contradictory account-registration
  and workspace-creation policy. Development and preview declare their permissive
  local choices explicitly.
- BunkFy replaces the Organizations admission policy at composition time and can
  require a verified active Auth email before self-service workspace creation.
- GMA Organizations and AccessControl expose opt-in API assurance settings. BunkFy
  applies a ten-minute recent-auth requirement to workspace creation/governance,
  access-profile and assignment mutations, and adapter ingress credential issue/revoke.
- Ordinary reads, invitation acceptance, enrollment claims, and daily
  reservation/inventory work do not require step-up.
- Docker-backed tests prove that a stale token receives the RFC 9470 challenge,
  password step-up produces fresh credentials, and the privileged retry succeeds.
  A separate PostgreSQL scenario proves that suspending membership denies the same
  unexpired token immediately and resuming restores access.
- Organizations and AccessControl repository verification, BunkFy migration/build
  gates, 2,115 non-Docker tests, focused Docker assurance/offboarding tests, OpenAPI
  drift, and preview Compose validation pass locally.

## Deferred

- Passkeys, hardware-backed authenticators, provider-specific step-up, and hosted IdP policy.
- Enrollment-link applicant identity and approval workflow, owned by SP-013.
- Guest export/erasure endpoints that do not exist until SP-002.
- Private support JIT access, SIEM routing, and cloud identity configuration.
