namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class GetDataRightsExportArtifactQueryHandler(
    IDataRightsExportArtifactRepository artifacts,
    ISystemClock clock)
    : IQueryHandler<
        GetDataRightsExportArtifactQuery,
        DataRightsExportArtifactDto>
{
    public async Task<Result<DataRightsExportArtifactDto>> HandleAsync(
        GetDataRightsExportArtifactQuery query,
        CancellationToken cancellationToken)
    {
        DataRightsExportArtifact? artifact = await artifacts.GetByCaseAsync(
            query.Scope,
            query.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            return Result.Failure<DataRightsExportArtifactDto>(
                DataRightsApplicationErrors.ExportArtifactNotFound);
        }

        DataRightsExportArtifactStatus status =
            clock.UtcNow >= artifact.ExpiresAtUtc &&
            artifact.State is not (
                DataRightsExportArtifactState.Deleting or
                DataRightsExportArtifactState.Deleted)
                ? DataRightsExportArtifactStatus.Expired
                : (DataRightsExportArtifactStatus)artifact.State;
        return Result.Success(artifact.ToDto(status));
    }
}
