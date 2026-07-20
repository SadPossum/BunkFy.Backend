# Reservations PII Minimization Task

Status: implemented and verified

## Goal

Remove direct guest identity from operational reservation reminder messaging without losing durable delivery, and add a focused executable guard against reintroducing guest text into Reservations-owned operational events.

This is the Reservations slice of the broader SP-001 data inventory and minimisation control. It does not complete the product-wide data catalogue.

## Current finding

The version 1 `reservation-arrival-reminder-due` event carries `PrimaryGuestName`, and the BunkFy notifications extension copies that value into notification history. The reminder scheduler and dispatch port also move the name even though reminder timing needs only reservation, property, date, time-zone, and revision identifiers.

`ReservationAllocationRejectedIntegrationEvent.Reason` is an enum rather than free-form text, so it is not a hidden guest-data channel. Provider ingress commands intentionally carry bounded booking details and remain canonical command boundaries; they are not operational broadcast events.

## Ownership

- Reservations owns reminder scheduling, event contracts, the outbox migration, and contract classification tests.
- `BunkFy.Extensions.Operations.Notifications` owns BunkFy-specific notification wording and both reminder event consumers.
- GMA Messaging owns generic durable transport and version metadata; no framework change is needed.
- GMA Notifications owns its history. BunkFy must not mutate that module's tables directly.

## Delivery design

1. Keep a version 1 compatibility contract and consumer so durable messages already queued on the v1 subject remain consumable.
2. Publish a version 2 reminder event that contains no guest name, email, phone, or notes.
3. Keep the existing v1 consumer identity and add a distinct v2 consumer identity. Both project the same generic notification and reservation navigation payload.
4. Remove guest names from reminder application ports and project only fields needed by scheduling and dispatch.
5. Redact existing PostgreSQL v1 outbox payloads to the neutral value `A guest`. This is compatible with old deployed v1 consumers while new consumers ignore the legacy field.
6. Add reflection-backed contract tests that fail when Reservations-owned operational events expose direct guest fields or use free-form rejection reasons.

## Deployment note

JetStream messages and notification history already published before this change are outside the Reservations database and cannot be rewritten by a Reservations migration. Before the first environment is allowed to contain real guest data, deploy the v1-compatible notification consumer, drain or reset pre-production JetStream data, and reset pre-production notification history. No real guest data is currently approved for production use.

## Non-goals

- Product-wide field, purpose, retention, and data-subject-rights catalogues.
- Guest export, correction, restriction, erasure, or deletion-ledger workflows.
- Direct writes into GMA Notifications storage.
- File, adapter, Inventory, or support-data classification.

## Acceptance criteria

- New reminder outbox messages use event version 2 and contain no direct guest fields.
- Version 1 reminder JSON, including legacy JSON with `primaryGuestName`, remains consumable.
- Reminder notification title, body, and navigation payload contain no guest name.
- Existing v1 PostgreSQL outbox payloads are irreversibly neutralized by migration.
- Focused unit, migration, architecture, and repository verification pass.

## Completion evidence

- Reminder production now emits the PII-free version 2 contract, while the original version 1 subject and consumer identity remain available for queued delivery.
- Reminder scheduling queries and application ports no longer materialize or transport the primary guest name.
- Both reminder versions project the same generic notification with reservation navigation identifiers only.
- The PostgreSQL migration neutralizes legacy v1 outbox names without making payloads unreadable to an older v1 consumer; a Docker migration test proves the upgrade from the preceding schema.
- Contract tests classify direct guest fields, reject them on operational events, and require bounded enums for `Reason` properties.
- `eng/verify.ps1 -SkipRestore` passed with a warning-free solution build, all migration drift checks, and 2,076 fast tests. The focused PostgreSQL migration suite also passed 2/2.
