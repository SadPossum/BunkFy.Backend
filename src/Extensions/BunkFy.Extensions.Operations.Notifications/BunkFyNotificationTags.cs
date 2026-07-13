namespace BunkFy.Extensions.Operations.Notifications;

using Gma.Framework.Notifications;
using Gma.Modules.Notifications.Contracts;

internal static class BunkFyNotificationTags
{
    private static readonly NotificationTag Web = new(
        NotificationTags.Web,
        NotificationTagKind.Delivery,
        "Web inbox",
        "Show in the staff web notification feed.");

    private static readonly NotificationTag Properties = Domain(
        "domain:properties",
        "Properties",
        "Property topology and lifecycle activity.");

    private static readonly NotificationTag Inventory = Domain(
        "domain:inventory",
        "Inventory",
        "Inventory availability and sales configuration activity.");

    private static readonly NotificationTag Reservations = Domain(
        "domain:reservations",
        "Reservations",
        "Reservation lifecycle and allocation activity.");

    private static readonly NotificationTag Providers = Domain(
        "domain:providers",
        "Data providers",
        "External provider and adapter activity requiring attention.");

    private static readonly NotificationTag Staff = Domain(
        "domain:staff",
        "Staff",
        "Staff access, assignment, and lifecycle activity.");

    public static IReadOnlyList<NotificationTag> PropertyActivity { get; } = [Web, Properties];
    public static IReadOnlyList<NotificationTag> InventoryActivity { get; } = [Web, Inventory];
    public static IReadOnlyList<NotificationTag> ReservationActivity { get; } = [Web, Reservations];
    public static IReadOnlyList<NotificationTag> ProviderAttention { get; } = [Web, Providers, Reservations];
    public static IReadOnlyList<NotificationTag> StaffActivity { get; } = [Web, Staff];

    private static NotificationTag Domain(string key, string displayName, string description) =>
        new(key, NotificationTagKind.Domain, displayName, description);
}
