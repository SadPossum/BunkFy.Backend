namespace BunkFy.Modules.Ingestion.Contracts.Adapters;

using Gma.Framework.Results;

public static class AdapterConfigurationMaterialErrors
{
    public static readonly Error ReferenceInvalid = new(
        "Ingestion.AdapterConfigurationReferenceInvalid",
        "The adapter configuration reference is invalid.");

    public static readonly Error MaterialNotFound = new(
        "Ingestion.AdapterConfigurationMaterialNotFound",
        "The adapter configuration material was not found.");

    public static readonly Error SchemaMismatch = new(
        "Ingestion.AdapterConfigurationSchemaMismatch",
        "The adapter configuration schema version does not match the runner.");

    public static readonly Error MaterialInvalid = new(
        "Ingestion.AdapterConfigurationMaterialInvalid",
        "The adapter configuration material is invalid.");
}
