namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.ModuleComposition;

public static class InventoryCompositionFeatures
{
    public static readonly CompositionFeatureId Availability = new("inventory.availability");

    public static ProvidedCompositionFeature AvailabilityProvided(string provider) =>
        new(Availability, provider, "Inventory configuration and availability facts are selected.");
}
