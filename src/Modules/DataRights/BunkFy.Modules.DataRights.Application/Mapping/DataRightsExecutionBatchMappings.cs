namespace BunkFy.Modules.DataRights.Application.Mapping;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public static class DataRightsExecutionBatchMappings
{
    public static DataRightsExecutionBatchDto ToDto(
        this DataRightsExecutionBatch batch) => new(
        batch.Id,
        batch.CaseId,
        batch.PropertyId,
        batch.ApprovalRevision,
        batch.ExecutionRevision,
        batch.SelectedSubjectCount,
        batch.CreatedAtUtc,
        batch.Version,
        (DataRightsCaseType)batch.CaseKind,
        (DataRightsExecutionScopeKind)batch.ScopeKind);
}
