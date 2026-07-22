namespace BunkFy.Extensions.Operations.Notifications;

internal interface IOperationalNotificationPayload;

internal sealed record ReservationNotificationPayload(
    Guid PropertyId,
    Guid ReservationId) : IOperationalNotificationPayload;

internal sealed record ProviderAttentionNotificationPayload(
    Guid PropertyId,
    Guid ReceiptId,
    Guid ConnectionId,
    Guid? ReservationId) : IOperationalNotificationPayload;

internal sealed record InventoryBlockCreatedNotificationPayload(
    Guid PropertyId,
    Guid BlockGroupId,
    DateOnly Arrival,
    DateOnly Departure) : IOperationalNotificationPayload;

internal sealed record InventoryBlockReleasedNotificationPayload(
    Guid PropertyId,
    Guid BlockGroupId) : IOperationalNotificationPayload;

internal sealed record RoomNotificationPayload(
    Guid PropertyId,
    Guid RoomId) : IOperationalNotificationPayload;

internal sealed record PropertyNotificationPayload(
    Guid PropertyId) : IOperationalNotificationPayload;

internal sealed record StaffProfileNotificationPayload(
    Guid StaffMemberId) : IOperationalNotificationPayload;
