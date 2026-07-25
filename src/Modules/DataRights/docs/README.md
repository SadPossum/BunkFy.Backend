# Data Rights

Data Rights coordinates controller-managed privacy and tenant-termination work
without taking ownership of another module's records. The module's executable
personal-data contract is
[`personal-data-catalog.v1.json`](personal-data-catalog.v1.json), with a
deterministically generated
[`personal-data-inventory.v1.md`](personal-data-inventory.v1.md).

## Current Slice

- tenant- and property-scoped, optimistic-concurrency case lifecycle;
- explicit requester-verification and controller-routing gates before discovery;
- PII-minimal case storage with authenticated staff actor attribution;
- bounded, exact-coordinate discovery delegated to authoritative owner modules;
- Guests-owned discovery with property-history visibility, masked contact hints,
  and owner revalidation before an opaque coordinate can be selected;
- no-store discovery responses and no lookup criteria persisted in the case;
- bounded subject selection that must be non-empty before review can begin;
- explicit review, decision-pending, approved or denied transitions with
  bounded reason codes and immutable decision revision/attribution;
- anonymisation approval is allowed only as a standalone operation and freezes
  server-resolved property, country-policy, retention-policy and digest
  evidence into the immutable decision revision;
- destructive approval requires an active, processing-enabled local Properties
  projection and the current country-policy pack to allow the erasure surface;
- the executor identity is rechecked against the approval revision and must be
  distinct from the deciding actor;
- starting anonymisation requires both the tenant-scoped erase permission and
  routing-property read permission, plus the configured destructive-operation
  authentication assurance;
- an idempotent execution transition freezes the exact approval, operation,
  selected owner coordinate, record version, policy digest and executor into a
  PII-minimal work item before any owner module can be invoked;
- durable, retry-safe worker dispatch invokes a versioned owner contributor,
  records the immutable owner proof and resumes safely after process failure;
- an append-only, HMAC-pseudonymised processing ledger plus an externally
  protected encrypted replay delta preserve deletion proof outside the
  application database;
- bounded restore reconciliation recreates missing ledger entries, invokes
  owner tombstone replay and advances a tenant checkpoint only after exact
  owner proof succeeds;
- API, Admin API, Admin CLI and Worker startup remain blocked until the
  protected recovery-scope snapshot is stable and fully reconciled;
- a PII-free, fail-closed owner-module approval gate that matches the exact
  tenant, property, operation, approved revision and selected record version;
- restriction approvals bind an explicit apply or release directive, so one
  approved intent cannot authorize the opposite transition;
- resumable selected-coordinate reads behind the sensitive discovery
  permission, while ordinary case DTOs expose only a count;
- a PII-carrying owner-export envelope that is explicitly catalogued as a
  subject-scoped, cross-module, one-hour transient fragment;
- a neutral streaming contributor/sink contract; callers must discard partial
  fragments unless the owner returns success;
- scoped permissions that are not granted to ordinary seeded roles;
- public management API plus empty Admin API and Admin CLI composition shells;
- a DataRights-owned Properties policy projection populated only through
  versioned Properties events or the bounded projection-rebuild contract;
- PostgreSQL persistence, inbox/outbox infrastructure and focused architecture,
  privacy, domain, persistence, migration and authorization tests.

The worker can rebuild the Properties policy projection for one tenant:

```powershell
tasks runs enqueue --tenant <tenant-id> --module data-rights --task rebuild-data-rights-properties --worker-group projection-workers --payload-json '{"projectionVersion":1,"batchSize":100,"dryRun":false}'
```

The case does not contain guest names, contacts, documents, search criteria,
provider payloads or free text. It stores only the selected owner's opaque
record coordinate and selection audit attribution. Guest data remains owned by
its source module. The export contract prepares owner fragments only; DataRights
does not persist fragments or expose download artifacts. Guest anonymisation
now reaches immutable owner and processing-ledger proof, with protected
pre-readiness replay after database restore. Existing reservation facts remain
owned by Reservations; only its local Guest-link eligibility projection is
updated from the PII-free Guests event.
