namespace BunkFy.Modules.DataRights.Application.Ports;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;

public interface IDataRightsRestoreCoordinator
{
    Task<Result<Unit>> ReconcileAsync(
        DataRightsRestoreScope scope,
        string scopeSnapshotSha256,
        CancellationToken cancellationToken);
}
