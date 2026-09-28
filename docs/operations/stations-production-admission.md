# Shared-station PIN admission

## 28 September 2026 candidate boundary

This is an admission contract, not an approval. P3 first-job source checkpoint
`746c75fe60d4600cf9f1e5fff173da9730514081` is published on
`codex/staff-pin-first-job-20260928`. P4 HTTP/UI composition is a later local
candidate. Its 17 passing local host/HTTP/PostgreSQL tests do not establish
exact-commit CI, hosted migrations, operator usability, or production admission.

The first station job is due-arrival inspection and attributed check-in of an
existing reservation. Pairing, personal PIN establishment, private supervised
setup for station-only staff, lock/switch and expiry are separate steps. A
station-only actor is not a primary Auth principal. Full workspace navigation,
new reservations, checkout, cleaning/payment modules and remote setup are not
part of this station release; do not expose them as working station actions.

## Configuration and activation

- Keep API `Stations:Http:Enabled=false` until admission. Select the same flag
  explicitly in the migration executable to include `StationsDbContext`, while
  leaving serving APIs disabled. Include Reservations station attribution in
  the exact migration catalogue. PostgreSQL is the supported provider.
- Require same-origin HTTPS, an exact `Stations:Http:AllowedOrigins` allowlist,
  and the Secure/HttpOnly/SameSite=Strict runtime cookie scoped to
  `/api/station-runtime`. Verify edge forwarding and mixed-primary-credential
  rejection through the deployed origin, not only loopback HTTP tests.
- Configure the real primary authentication scheme, Auth scope and recent
  destructive-operation assurance. Verify the manager's exact original session
  is inactive before staff admission; optimistic local logout is not evidence.
- Supply deployment-owned `Stations:PepperKeys`, selected
  `Stations:Core:PepperVersion`, positive `Stations:Core:ExternalEpoch`, and a
  persistent protected Data Protection key ring. Do not use schema-export or
  disposable-test keys in an operational environment.
- Never enable the legacy `Station:Enabled` experiment alongside this module.
  Verify named `station-read` and `station-write` policies at the intended
  limiter topology, including cross-replica and provider-outage behavior.

Required order: exact source/CI and image provenance -> deploy disabled ->
approved migration Plan/Apply and post-check -> disabled smoke -> isolated
HTTPS role/recovery journeys -> one-property canary -> bounded rollout.
Record image digests, configuration fingerprint, catalogue/target hashes,
backup/restore receipt, reviewer verdicts, canary owner and rollback decision.
Configuration declarations and local tests alone satisfy none of the hosted
evidence requirements.

## Inventory currentness and existing data

Station arrivals and check-in require exact agreement between Inventory's current
availability version and the Reservations projection. Availability mutations now
enqueue the existing unit-definition event at the post-mutation version in the
same Inventory transaction as allocation/block changes. They do not touch the
version a second time. Retirement can subsequently publish a higher version with
final sellability; duplicate or out-of-order delivery must not resurrect sales.
During convergence, return no partial arrivals. The UI keeps the admitted station
lockable and offers a local retry for the typed Incomplete job response; actual
actor, session or CSRF conflicts still conceal/revalidate authority.

Earlier allocations may have advanced versions without publishing definitions.
Waiting or replaying the original allocation cannot repair these old gaps.
Reconcile through the Inventory owner, preserving audit/history and checking
exact owner/projected versions before admitting station work. The current general
availability rebuild export omits active retirement drains, so it is NOT approved
as an unrestricted production repair. A no-retirement synthetic preview fixture
can use its bounded owner path after explicit precheck; production reconciliation
requires drain-aware exports or separately reviewed guarded owner publication.
Do not edit projection rows, fabricate events or relax equality. Binary rollback
to code that omits publication requires disabling station entry points until
currentness is restored. Queue lag, failed publication and persistent Incomplete
responses need monitored, payload-free signals and a named support owner.

## Recovery and support

Persist only bounded non-secret operation coordinates in browser recovery
storage. Never store a PIN, cookie credential, setup grant, CSRF/bearer token or
guest payload. Register may retain its normalized station label in tab storage
for two hours for same-operation retry; expiration redacts the label, not the
unresolved operation. Re-entry must retain the original operation ID.

Manager recovery rechecks the exact original session under the same manager's
account, revokes only that session if still active, verifies the result, then
closes the recovery session. Lost Register/Pair responses do not redisclose a
device credential: explicitly pair the browser again. PIN setup is entered
privately by staff, never chosen or retrieved by support.

An uncertain check-in retains its original actor, generation, reservation,
version and operation. A new actor cannot replay it as their own. Quarantine
only that reservation; permit unrelated work using at most one additional
persisted in-flight attempt. Never overwrite an unresolved operation. A second
unknown result stops further writes. Timeout, NotFound, disappearance from the
arrival list and a new reservation version are not proof of an applied result.
Clear only a server-confirmed exact outcome. Support must use scoped audited
manager actions; no station-side "manager reviewed" bypass is authorized.

The changed-actor `/api/station-runtime/check-in/outcome` endpoint is a
guest-free historical confirmation read. Preserve POST/CSRF, exact current
paired-device admission and bounded rate limits. It returns state only and
never dispatches or retries a reservation mutation. Pending is not absence
proof; Conflict requires manager review; only exact Applied clears recovery.
Keep operation and station-attribution owner records together throughout the
supported recovery window. Define owner-approved cleanup ordering and the
unavailable-outcome behavior when lawful cleanup removes historical evidence;
no production retention period is approved here. Exercise the endpoint in
deployed edge/mixed-credential tests. Count outcome reads separately from
mutations, and keep their security/personal coordinates out of metric labels,
routine logs, support screenshots and guest exports.

For suspected device compromise, revoke the station and affected sessions,
preserve audit references and follow the incident runbook. Never ask for PINs,
setup grants or credentials in support messages, URLs, screenshots or logs.

## Data lifecycle and operational evidence still required

Catalogue registrations, staff credentials/digests, grants, setup grants,
station/browser/actor sessions, issuer coordinates, operation/activity receipts,
and Reservations attribution as security/personal data. Bind subject-rights,
retention, cleanup/hold, tenant export/destruction and backup disposition to the
actual schemas. Staff attribution must not leak into guest reservation exports.
Production periods and legal approval remain external, unproved requirements.

Retain payload-free signals for failed/throttled PIN attempts, setup abuse,
pairing/revocation, authority loss, unknown/stale/conflict outcomes and provider
failure. Name dashboard/alert/support owners, use bounded metric dimensions,
and verify delivery in the canary. Monitoring prose is not deployed proof.

Restore drills must advance the deployment-owned external epoch and show old
browser/PIN authority cannot resurrect. Pepper rotation needs a declared overlap
or re-enrollment policy; do not drop a version still referenced by credentials.
Prove key-ring continuity and cross-replica behavior separately.

## Rollback and evidence separation

Disable runtime and web entry points first, retain additive schemas and audit
attribution, then forward-fix. Do not run destructive Down against a used station
database or erase receipts to repair a UI. A binary rollback does not undo guest
check-ins. Restore requires the approved backup, epoch and recovery procedure.

Track implementation/local checks, published source/exact CI, immutable images,
deployed edge/migrations and operational/legal admission separately. SP-013
invitation/link enrollment and human operator/assistive-technology testing remain
separate open work; this document does not mark SP-001–SP-013 complete.
