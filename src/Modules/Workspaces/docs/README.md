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
Completion, rejection, and supersession redact those fields from the Workspaces
record; the Staff module becomes authoritative after successful provisioning.
Sensitive API and Admin API responses are explicitly non-cacheable.

The catalogue contains engineering defaults only. It does not approve legal
bases, retention periods, country operation, or data-subject exceptions.
