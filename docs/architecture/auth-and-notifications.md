# Auth And Notifications

BunkFy mounts the published GMA Auth, Notifications, and Extensions `dev` sources. The public API composes the optional OpenID Connect provider adapter, while providers remain disabled until product callback URLs and credentials are configured. Auth supports external identities, password management, email verification, and security events without making any provider mandatory.

`Gma.Extensions.Auth.Notifications` maps Auth security events into mandatory tagged notifications. Email delivery is also optional: the adapter is registered but disabled until the application supplies an `IEmailSender` and sender configuration. Verification must not be enabled operationally before that transport exists.

## Product Notifications

`BunkFy.Extensions.Operations.Notifications` owns BunkFy-specific recipient policy. It consumes public product integration events, asks Staff's narrow public audience reader for authenticated recipients, and projects V2 notification requests into Notifications. Product modules do not reference Notifications application or persistence projects.

Current web-inbox coverage is intentionally low-noise:

- property retirement;
- inventory block creation/release and room sales-mode changes;
- reservation confirmation, cancellation, allocation rejection, and no-show;
- external reservation operations that require staff attention;
- staff property-assignment and lifecycle changes for the affected staff member.

These notifications respect recipient preferences. Security notifications remain mandatory. Guest-profile edits, successful provider receipts, check-in/check-out, and other routine CRUD activity stay quiet; add coverage only when a durable source event and an unambiguous recipient policy exist.

The worker composes Auth, Staff, Notifications, and the two extensions when their module switches are enabled. Notifications admin endpoints expose tag, routing, delivery, and retry operations. Retention is configured but disabled until an operations policy is approved.
