# Tenant Termination Phase Ordering Task

Status: completed
Date: 2026-08-04

## Goal

Make owner ordering explicit per tenant-termination phase before product-domain
destruction is implemented.

## Problem

The current contributor descriptor combines a set of supported phases with one
global dependency list. That was sufficient while most product owners exposed
only tenant export. It becomes unsafe when the same owners add destruction:
the deterministic parent-to-child export chain would also delete Properties
before Inventory and Reservations.

Filtering a global topological order after sorting does not solve this. It can
also let a phase depend on an owner that does not participate in that phase,
making the apparent dependency impossible to satisfy at execution time.

## Design

Replace the two independent descriptor fields with explicit phase plans:

```text
PhasePlan
  Phase
  DependsOnOwnerKeys
```

Every contributor declares one plan for each supported phase. Data Rights
validates and orders only the owners participating in the selected phase.

Validation fails closed for:

- unknown or duplicate phases;
- malformed, duplicate, self, or excessive dependencies;
- dependencies missing from the contributor catalogue;
- dependencies that do not support the same phase;
- a cycle in any declared phase graph; and
- a requested phase with no contributor.

The execution request/result contract remains version 1 because its serialized
shape and semantics do not change. Owner catalogue versions and digests change
when an owner later adds a new phase or changes that phase's dependency plan.

## Initial Ordering Direction

Existing phase behavior is preserved by representing today's dependencies in
each current plan. Destruction plans are added owner by owner only with their
destruction policy. The intended final direction is:

- freeze establishes the Workspaces fence;
- export retains the current deterministic owner order;
- destroy removes source and booking state before dependent identity,
  inventory, topology, generic access, and task state;
- Workspaces closes the final owner path only after generic cleanup; and
- restore and verify use their own explicit graphs rather than inheriting
  either export or destroy ordering.

## Acceptance

- every production and test contributor uses explicit phase plans;
- ordering is stable by owner key among ready nodes;
- each phase can have a different valid dependency graph;
- cross-phase dependencies are rejected rather than silently ignored;
- production-catalog validation checks every declared phase graph; and
- focused Data Rights and contributor-composition tests pass.

This is a BunkFy Data Rights orchestration contract. No GMA change is needed.

## Evidence

- all production and test contributors now declare explicit phase plans;
- seven focused catalogue tests cover stable ordering, independent export and
  destroy graphs, missing and cross-phase dependencies, cycles, duplicate
  owners, malformed phases, and exact Production owner membership;
- the Worker composition builds with zero warnings and errors;
- the affected Workspaces, Retention, Operations Notifications,
  Organizations, Access Control, and Task Runtime contributor tests pass; and
- all 323 Data Rights fast tests pass.
