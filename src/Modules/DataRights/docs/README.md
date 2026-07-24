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
- resumable selected-coordinate reads behind the sensitive discovery
  permission, while ordinary case DTOs expose only a count;
- scoped permissions that are not granted to ordinary seeded roles;
- public management API plus empty Admin API and Admin CLI composition shells;
- PostgreSQL persistence, inbox/outbox infrastructure and focused architecture,
  privacy, domain, persistence and authorization tests.

The case does not contain guest names, contacts, documents, search criteria,
provider payloads or free text. It stores only the selected owner's opaque
record coordinate and selection audit attribution. Guest data remains owned by
its source module. Decisions, owner work contracts, protected exports, receipts,
ledger/restore protection and destructive execution are later slices and must
remain fail closed until implemented.
