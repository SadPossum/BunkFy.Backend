namespace BunkFy.Modules.Ingestion.Contracts.Adapters;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Results;

public interface IAdapterConfigurationMaterialResolver
{
    Task<Result<AdapterConfigurationMaterial>> ResolveAsync(
        AdapterConfigurationMaterialRequest request,
        CancellationToken cancellationToken);
}

public sealed record AdapterConfigurationMaterialRequest(
    Guid ConnectionId,
    string ScopeId,
    Guid PropertyId,
    string AdapterType,
    int ExpectedSchemaVersion,
    string ConfigurationReference,
    string? SecretReference);
