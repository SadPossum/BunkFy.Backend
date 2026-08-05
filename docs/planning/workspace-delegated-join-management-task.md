# Workspace Delegated Join Management Task

Status: complete
Date: 2026-08-04

## Goal

Allow an active workspace member with BunkFy's `staff.manage` permission to
operate the Staff invitation, enrollment-link, and approval workflows without
granting Organizations ownership or weakening the BunkFy access-plan boundary.

## Ownership

- GMA Organizations owns active-membership and owner authorization. It may
  consult optional, domain-neutral operator authorization policies for
  join-source operations; no policy means the existing owner-only behavior.
- BunkFy Workspaces owns the `staff.manage` decision, operational workspace
  admission, prepared and active Staff access plans, and claim-to-onboarding
  correlation.
- Organizations remains authoritative for tokens, source lifecycle, claims,
  approval races, membership creation, and owner governance.
- BunkFy product policy does not enter GMA, and Workspaces never impersonates
  an owner or bypasses Organizations application behavior.

## Invariants

- Suspended, removed, or cross-workspace members are denied before extension
  policy is consulted.
- Owners retain current behavior even when no delegated policy is composed.
- A non-owner needs one explicit allow decision; deny, unavailable, unknown,
  and policy exceptions fail closed.
- Delegated issuance is allowed only for an exact active Workspaces access
  plan. Direct Organizations issuance cannot create a usable BunkFy join path.
- Generic reissue and rotation remain owner-only. BunkFy replacement stays
  deny-first and creates the replacement through its plan-bound facade.
- Claim approval or rejection requires a Workspaces onboarding application
  bound to the exact claim; Organizations still applies its join-admission
  policy before membership creation.

## Delivery

1. Add the generic Organizations authorization contract and owner-or-delegated
   evaluator with default-deny composition semantics.
2. Apply it only to invitation, enrollment-link, and join-request handlers.
3. Add the BunkFy Workspaces policy and register it through the existing
   module composition boundary.
4. Prove owner compatibility, inactive-member denial, policy failure behavior,
   plan-bound issuance, claim-bound decisions, and non-owner product flows.
5. Align the existing invitation/access-plan planning note after verification.

## Verification

- focused Workspaces delegated-authorization and join-flow tests: 45 passed;
- full Workspaces module suite: 284 passed;
- full GMA Organizations module suite: 177 passed;
- backend architecture guards: 83 passed;
- complete Integration Tests host graph build: succeeded with zero warnings and
  zero errors.

No Docker rerun was needed: the slice changes authorization composition and
handler context propagation without changing persistence or broker behavior.

## Verification Cadence

Use focused Organizations and Workspaces tests while editing. Run each complete
module suite and the architecture guards once after the slice is coherent. No
Docker rerun is required unless the non-Docker proof exposes a persistence or
broker-specific risk.
