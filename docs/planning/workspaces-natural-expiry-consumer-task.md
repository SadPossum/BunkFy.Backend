# Workspaces Natural Expiry Consumer Task

Status: complete
Date: 2026-07-28

## Goal

Consume Organizations-owned natural-expiry facts without duplicating deadline
authority in BunkFy. Workspaces must terminate and redact product onboarding
data at the correct boundary while preserving claims that were validly
submitted before their enrollment link expired.

## Ownership

Organizations owns invitation, enrollment-link, and enrollment-claim
deadlines, durable expiry transitions, and payload-minimal integration facts.

Workspaces owns staged staff profile data, access plans, product-facing
onboarding status, and the reaction to those facts. Framework and GMA
Extensions require no change.

## Product Rules

- Invitation expiry marks its active onboarding application and access plan
  `Expired` and redacts staged applicant data.
- Claim expiry finds the application by authoritative claim id, applies the
  claim version monotonically, marks it `Expired`, and redacts staged applicant
  data.
- A claim-expiry fact without its expected application fails inbox handling
  instead of being acknowledged and lost. The durable retry lets an older
  claim-change fact catch up because cross-subscription ordering is not assumed.
- Enrollment-link expiry does not cancel a claim submitted while the link was
  valid. Its access plan remains available while any onboarding application for
  that source is active.
- Link expiry also preserves an unbound `Submitted` application because its
  earlier claim-change fact may be lagging on another subscription. Global
  event ordering is not assumed.
- Enrollment-link expiry is recorded on its access plan without terminating
  valid pending onboarding. The plan becomes `Expired` only after the source
  expiry has been observed and no active onboarding remains.
- Claim expiry alone never retires a reusable access plan while its enrollment
  link remains active. Claim-expiry handling only finalizes a plan whose source
  expiry was already observed, so either event order converges safely.
- Duplicate and stale expiry facts are idempotent and cannot regress a later
  onboarding state.

## Runtime Composition

- The public API exposes claim-lifetime and bounded lifecycle settings with
  lifecycle processing disabled.
- The Worker exposes the same bounded lifecycle settings disabled by default.
- BunkFy's AppHost enables Organizations and its lifecycle worker together on
  the single maintenance process used for local composition.
- Production must apply the same two switches to exactly one maintenance
  replica.

## Acceptance Criteria

- all three dedicated Organizations expiry contracts are subscribed through
  the Workspaces module descriptor and normal inbox pipeline;
- applicant data is removed when an invitation or pending claim expires;
- link expiry preserves valid pending claims and their access plan;
- access plans expire once no active onboarding depends on them;
- public Workspaces contracts expose an additive `Expired` status;
- personal-data catalogue coverage remains complete;
- focused domain, handler, composition, and persistence tests pass;
- the final backend non-Docker gate and one relevant PostgreSQL/Docker gate
  pass before publishing.

## Follow-up Boundary

Abandoned pre-claim staging needs a Workspaces-owned bounded retention slice.
It must not be inferred synchronously from link expiry because that would race
an earlier valid claim fact. The follow-up should expire and redact staging
without adding BunkFy concepts to Organizations or relying on global event
ordering.

## Evidence

- focused domain and expiry-handler suites passed, including stale, duplicate,
  missing-predecessor, and cross-subscription ordering cases;
- privacy-catalogue, host-composition, and worker-composition checks passed;
- the repository verification gate passed with a zero-warning build, clean
  migration drift, and all non-Docker tests green;
- the focused PostgreSQL integration proof passed after applying the generated
  migration for the new terminal states and their redaction constraints.
