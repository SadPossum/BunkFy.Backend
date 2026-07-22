# Cross-Module Integration

The default pattern is asynchronous integration through contracts and local projections.

## Rule

Modules may reference another module's `.Contracts` project only.

Allowed:

```text
BunkFy.Modules.Inventory.Application -> BunkFy.Modules.Properties.Contracts
```

Not allowed:

```text
BunkFy.Modules.Inventory.Application -> BunkFy.Modules.Properties.Application
BunkFy.Modules.Inventory.Persistence -> BunkFy.Modules.Properties.Persistence
BunkFy.Modules.Inventory.Domain -> BunkFy.Modules.Properties.Domain
InventoryDbContext -> FK to properties.rooms
```

Public contract surfaces should still belong to the module that publishes them. A consumer module can reference a producer's contracts for integration event payloads, subject constants, subscription metadata, and projection export contracts, but it should not expose producer DTOs or enums from its own `.Contracts` or `.Admin.Contracts` API. Duplicate the scalar/read-model fields that the consumer owns instead.

Projection rebuilds follow the same rule. A producer can expose an export contract from its `.Contracts` project, and the producer's persistence adapter can implement that source. The consumer still owns the destination table, checkpoint table, and rebuild task.

## Why Duplicate Data?

Duplicated read data keeps modules independently understandable and replaceable. A consumer stores the data it needs for local decisions and updates that data from integration events.

This is not "stale cache" data. It is module-owned state with eventual-consistency semantics.

## Flow

```mermaid
flowchart LR
    A["Properties command"] --> B["Property aggregate"]
    B --> C["Domain event"]
    C --> D["Properties outbox"]
    D --> E["NATS JetStream"]
    E --> F["Inventory consumer"]
    F --> G["Inventory inbox"]
    F --> H["Property topology projection"]
    H --> I["Availability decision"]
```

## Addressed Notifications From Local Decisions

Notification delivery follows the same ownership rule. The module that understands the resource decides recipients; the Notifications module only stores and streams already-addressed notification requests.

The same ownership shape applies when a BunkFy module emits an addressed notification:

```mermaid
flowchart LR
    A["Owned business fact changed"] --> B["Producer outbox"]
    B --> C["NATS JetStream"]
    C --> D["Consumer module"]
    D --> E["Consumer-owned projection"]
    D --> F["Consumer candidate decision"]
    F --> G["Organizations active-access intersection"]
    G --> H["UserNotificationRequestedIntegrationEventV2"]
    H --> I["Notifications projector"]
    I --> J["Notifications unit of work and inbox"]
    J --> K["notifications.user_notifications"]
```

This is the same shape a private chat or PMS policy module should use:

- chat membership and message visibility belong to the chat module;
- property/user-management policies belong to the PMS module;
- inventory availability belongs to Inventory and property topology belongs to Properties;
- Notifications receives explicit user targets only, never business-specific visibility rules.

BunkFy's operational notification extension is the consumer in this flow. It intentionally joins public product-event contracts, product candidate-audience readers, Organizations' bounded active-access filter, and Notifications' projector seam. Product projections nominate candidates; Organizations removes subjects whose organization or membership is no longer active immediately before addressing. The filter accepts only a caller-supplied bounded set and cannot enumerate membership, while BunkFy retains all product role, property, and recipient semantics. The source event remains durable through its producer outbox and NATS; the addressed notification and consumer inbox are committed by Notifications. Deterministic notification ids make retries idempotent for every recipient.

## Compatibility

Producer events should change additively. Breaking payload changes require a new event version and subject.

Consumers should ignore fields they do not need. They should be prepared for duplicate delivery.

Consumer registrations should use producer subject constants and consumer handler-name constants from module metadata. Avoid copying raw subject or durable-handler strings into application registration.

## Backfill and Repair

When existing local projection data must be repaired, prefer an explicit rebuild task:

```mermaid
flowchart LR
    A["Producer .Contracts export"] --> B["Producer persistence source adapter"]
    B --> C["Consumer rebuild task"]
    C --> D["Consumer projection writer"]
    C --> E["Consumer checkpoint table"]
```

The consumer may duplicate producer data deliberately. It must not add a cross-module foreign key or reference producer EF/domain/application projects to make the rebuild easier.
