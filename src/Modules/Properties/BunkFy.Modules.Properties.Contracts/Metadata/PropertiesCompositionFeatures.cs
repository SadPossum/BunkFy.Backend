namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.ModuleComposition;

public static class PropertiesCompositionFeatures
{
    public static readonly CompositionFeatureId PhysicalSetup = new("properties.physical-setup");

    public static ProvidedCompositionFeature PhysicalSetupProvided(string provider) =>
        new(PhysicalSetup, provider, "Property, room, and bed facts are selected.");

    public static RequiredCompositionFeature PhysicalSetupRequired(string owner, string? reason = null, bool optional = false) =>
        new(PhysicalSetup, owner, optional, reason);
}
