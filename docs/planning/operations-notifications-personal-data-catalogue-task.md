# Operations Notifications Personal-Data Catalogue Task

Status: published; local and exact-commit production proof complete

## Goal

Close the Operations Notifications portion of company-readiness control SP-001.
Operational inbox delivery must remain useful for navigation while preventing
free text, direct identity, stale membership, or unnecessary technical history
from being copied into every recipient's durable notification record.

## Audit Findings

- Manual inventory-block notifications copy the operator's free-text reason
  into every recipient payload.
- Several payloads retain actor ids, error codes, versions, allocation ids, and
  other values that the web application does not use for navigation.
- Property audiences are derived from Staff and AccessControl projections.
  Those projections are useful candidate sources, but membership revocation is
  authoritative in Organizations and projection cleanup is asynchronous.
- Anonymous JSON payload construction prevents reflection-based schema coverage.

## Ownership Boundary

- Source modules own durable business facts and their minimized integration
  events.
- BunkFy Operations Notifications owns product wording, candidate audience
  composition, initiating-user exclusion, navigation payloads, and notification
  data policy.
- GMA Organizations owns a generic bounded filter over authoritative active
  organization access. It does not know notification or BunkFy policy.
- GMA Notifications owns generic addressed requests, preferences, inbox state,
  retention mechanics, delivery, and realtime transport. It never selects a
  BunkFy audience.

## Delivery Contract

1. Combine active property-staff and workspace-owner candidates, deduplicate
   them, and exclude the initiating user.
2. Intersect every property and staff-self candidate with authoritative active
   organization access immediately before projection. Invalid BunkFy scope ids
   fail closed; authority outages remain retryable failures.
3. Replace anonymous JSON with a closed set of typed navigation payloads:
   reservation, provider attention, inventory block, room, property, and staff
   profile.
4. Store only values needed to understand or navigate the event. In particular,
   do not project block reasons, actor ids, provider/error text, concurrency
   versions, or duplicate business details.
5. Add a versioned executable catalogue and deterministic inventory for the
   addressed notification envelope and typed payloads.
6. Add guards that make new handlers, payload members, restricted content, or
   audience bypasses fail focused verification.

## Security And Efficiency Invariants

- A suspended or removed member receives no newly projected operational
  notification even if a Staff or AccessControl projection is stale.
- Membership filtering is candidate-bounded and batched; it does not enumerate
  the organization or issue one authority query per recipient.
- Direct identifiers, contact data, demographics, preferences, operator free
  text, source payloads, and arbitrary errors never enter titles, bodies,
  payloads, tags, logs, metrics, traces, or support bundles.
- Staff lifecycle/assignment notifications are addressed only to the linked
  staff account and still require active organization access.
- Notification ids remain deterministic per source event, recipient, and
  notification name so retries are idempotent.
- Tags and delivery policy remain static, bounded, and preference-respecting.

## Verification

- GMA Organizations focused and PostgreSQL tests for the candidate filter;
- focused Operations Notifications catalogue, payload, content, audience,
  registration, and idempotency tests;
- architecture and source-boundary checks;
- synchronized warning-free build, migration drift checks, complete non-Docker
  tests, Docker integration tests, and exact-commit CI before publication.

Current evidence:

- GMA Organizations `dec1732` publishes the bounded candidate filter; its exact
  validation and PostgreSQL workflows are green after the complete local
  boundary, build, migration, 68-unit-test, four-PostgreSQL-test, and
  vulnerability gates passed;
- the Operations Notifications catalogue declares 15 fields with 22 concrete
  bindings across the addressed envelope and all seven sealed payload types;
- all 21 focused extension tests and all 58 architecture tests pass;
- the Worker composition test resolves the Organizations authority and product
  notification bridge together;
- the synchronized warning-free build, all migration drift checks, all 2,323
  non-Docker tests, and all 33 Docker integration tests pass;
- published backend commit `df3c26ff1a0aa21c346020e41c11f1e2bf78432b`
  passed exact Windows and Ubuntu validation plus Docker workflow
  `29900396749`; validation workflow `29900396821` is also green.

## Deferred

- Final legal approval of notification retention and rights behavior.
- Product-wide SP-002 export/erasure orchestration and SP-003 retention evidence.
- Email, push, SMS, or other delivery wording and disclosure reviews when those
  product channels are enabled.
