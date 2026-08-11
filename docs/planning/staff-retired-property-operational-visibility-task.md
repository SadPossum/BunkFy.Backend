# Staff Retired-Property Operational Visibility Task

Status: complete
Date: 2026-08-11

## Goal

Keep Staff employment history intact when a property retires while ensuring
ordinary Staff directory surfaces describe only properties that remain
operational.

## Ownership

- Properties owns property lifecycle and publishes versioned lifecycle facts.
- Staff owns its local property projection, assignment history, directory
  semantics, and response shapes.
- GMA remains unchanged; this is BunkFy Staff policy over a BunkFy Properties
  contract.

## Invariants

- Property retirement never deletes or rewrites a Staff assignment record.
- Sensitive Staff history continues to expose the original assignment and its
  lifecycle fields for authorized audit and data-rights use cases.
- Tenant directory detail includes only current assignments whose projected
  property is active.
- `CurrentPropertyCount` counts only current assignments whose projected
  property is active.
- Property-scoped directory reads remain unavailable for retired properties.
- Notification audience behavior remains unchanged: property-retirement
  notifications may still address the previously assigned, currently active
  workforce after authoritative membership and permission filtering.

## Verification

1. Extend focused repository coverage with an assigned retired property.
2. Prove tenant directory detail and count exclude that assignment while the
   aggregate history still retains it.
3. Prove retired-property detail and list reads remain unavailable.
4. Run focused Staff tests while editing, then one consolidated Staff and
   architecture gate at the slice boundary.

## Outcome

- Ordinary Staff directory assignment lists and `CurrentPropertyCount` now
  require an active local Properties projection.
- A retired property's assignment remains in the Staff aggregate and sensitive
  history; retirement changes operational visibility rather than rewriting
  employment history.
- Property-scoped directory reads continue to fail closed for retired
  properties.
- No public contract, database migration, frontend, notification-audience, or
  GMA change was required.

## Evidence

- Focused Staff directory repository tests: 3 passed.
- Full Staff module suite: 242 passed.
- `Integration.Tests` single-node build: 0 warnings and 0 errors.
- Targeted PostgreSQL repository regression: 1 passed.
- Architecture guard: 102 passed.

## Deferred

- Automatically ending assignment history on property retirement is a separate
  employment-policy decision and is not implied by operational visibility.
- Browser and real-provider onboarding proof remains a private release gate and
  is not evidence supplied by this code-owned slice.
