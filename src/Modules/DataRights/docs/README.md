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
- PostgreSQL persistence, inbox/outbox infrastructure and focused architecture,
  privacy, domain, persistence and authorization tests.

The case does not contain guest names, contacts, documents, search criteria,
provider payloads or free text. It stores only the selected owner's opaque
record coordinate and selection audit attribution. Guest data remains owned by
its source module. The export contract prepares owner fragments only; DataRights
does not yet execute cases, persist fragments or expose download artifacts.
Protected export assembly, owner receipts, ledger/restore protection and
destructive execution are later slices and must remain fail closed until
implemented.
