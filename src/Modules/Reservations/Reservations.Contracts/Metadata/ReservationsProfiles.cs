namespace Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;
using Inventory.Contracts;

public static class ReservationsProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        ReservationsModuleMetadata.Name,
        DefaultName,
        provides:
        [
            ReservationsCompositionFeatures.BookingLifecycleProvided(Provider(DefaultName))
        ],
        requires:
        [
            new RequiredCompositionFeature(
                TenancyCompositionFeatures.Context,
                Provider(DefaultName),
                reason: "Reservations is tenant-scoped."),
            new RequiredCompositionFeature(
                InventoryCompositionFeatures.Availability,
                Provider(DefaultName),
                reason: "Inventory owns reservation allocation and no-overbooking."),
            MessagingCompositionFeatures.OutboxRequired(
                Provider(DefaultName),
                "Reservation lifecycle and Inventory requests use the module outbox."),
            MessagingCompositionFeatures.NatsConsumersRequired(
                Provider(DefaultName),
                "Inventory allocation outcomes update reservation lifecycle asynchronously.",
                optional: true)
        ],
        requiredModules:
        [
            new RequiredCompositionModule(
                InventoryModuleMetadata.Name,
                Provider(DefaultName),
                reason: "Reservations requests concrete claims from Inventory.")
        ],
        displayName: "Reservations default",
        description: "Staff-managed reservation lifecycle with Inventory-owned asynchronous allocation.");

    private static string Provider(string profileName) => $"{ReservationsModuleMetadata.Name}/{profileName}";
}
