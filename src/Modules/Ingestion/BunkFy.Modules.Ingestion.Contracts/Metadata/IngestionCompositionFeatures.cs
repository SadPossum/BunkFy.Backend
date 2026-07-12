namespace BunkFy.Modules.Ingestion.Contracts;

using Gma.Framework.ModuleComposition;

public static class IngestionCompositionFeatures
{
    public static readonly CompositionFeatureId ObservationReceipts = new("ingestion.observation-receipts");

    public static ProvidedCompositionFeature ObservationReceiptsProvided(string provider) =>
        new(ObservationReceipts, provider, "External observations can be durably received and deduplicated.");
}
