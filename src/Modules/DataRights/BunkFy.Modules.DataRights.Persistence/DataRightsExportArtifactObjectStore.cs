namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;
using Gma.Framework.FileManagement;
using Gma.Framework.Scoping;

internal sealed class DataRightsExportArtifactObjectStore(
    IFileStorage storage,
    IScopeContext scopeContext) : IDataRightsExportArtifactObjectStore
{
    public Task<bool> DeleteAsync(
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            throw new InvalidOperationException(
                "DataRights.ExportArtifactTenantRequired");
        }

        FileStorageObjectKey key = DataRightsExportStorageKey.Create(
            scopeContext.ScopeId,
            artifactId);
        return storage.DeleteAsync(key, cancellationToken);
    }
}
