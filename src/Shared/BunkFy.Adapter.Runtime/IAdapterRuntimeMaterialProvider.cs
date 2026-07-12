namespace BunkFy.Adapter.Runtime;

using BunkFy.Adapter.Abstractions;

public interface IAdapterRuntimeMaterialProvider
{
    Task<AdapterConfigurationMaterial> ResolveAsync(
        AdapterRuntimeIdentity identity,
        int configurationSchemaVersion,
        CancellationToken cancellationToken);
}
