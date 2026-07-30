# Workspaces Module

Workspaces coordinates the BunkFy product boundary between GMA Organizations,
Auth, Access Control, and the Staff domain. It owns staff-onboarding applications,
access plans, and recoverable access-lifecycle processes; it does not take
ownership of organization claims, Auth identities, access profiles, properties,
or durable Staff profiles.

The executable [`personal-data-catalog.v1.json`](personal-data-catalog.v1.json)
classifies Workspaces persistence, public/application boundaries, one-time join
tokens, and the person-linked Organizations and Staff events consumed by this
module. The generated
[`personal-data-inventory.v1.md`](personal-data-inventory.v1.md) is checked by
reflection tests against every selected mapped member and public contract.

Copied applicant identity and contact fields are transient onboarding data.
Completion, rejection, supersession, invitation expiry, and claim expiry
redact those fields from the Workspaces record; the Staff module becomes
authoritative after successful provisioning. Unclaimed enrollment-link
staging is also reconciled by the tenant-scoped Retention schedule after the
source expires. Workspaces checks the exact Organizations claim before
redaction and reports an operational failure instead of deleting data when
the authoritative history window has lapsed. Sensitive API and Admin API
responses are explicitly non-cacheable.

Automatic Staff retention also asks Workspaces to close access and remove the
departed person's Auth subject from terminal onboarding and access history.
Workspaces blocks while a person-linked onboarding or access workflow is
active, replaces its remaining subject and actor references with one opaque
receipt-local pseudonym, and records an append-only canonical receipt. A retry
reasserts access denial before accepting that receipt as complete. This
irreversible scrub is not used by the reversible Data Rights anonymisation and
restore path.

For approved Staff Rights access cases, Workspaces now contributes its own
tenant-scoped subject discovery, stale-safe selection validation, and
catalogue-driven protected export. The owner exposes onboarding applications,
access processes, access plans, and immutable retention-correlation receipts
as separate versioned coordinates. Exact record or Auth account-subject
lookups are accepted; weak, mixed, property-scoped, cross-tenant, and retained
pseudonym lookups fail closed. Correction, restriction, and destructive
execution remain deferred until their owner-local authority and companion
record semantics are specified.

The engineering defaults live under
`Workspaces:StaffOnboardingRetention`: a two-hour source-expiry grace period,
a 20-hour authoritative-inspection ceiling, a 60-minute schedule interval,
and a batch size of 50. The authority ceiling must remain below
Organizations' minimum one-day enrollment-history guarantee.

The catalogue contains engineering defaults only. It does not approve legal
bases, retention periods, country operation, or data-subject exceptions.
