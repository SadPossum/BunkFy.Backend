namespace BunkFy.Modules.DataRights.Application.Mapping;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public static class DataRightsExecutionWorkItemMappings
{
    public static DataRightsExecutionWorkItemDto ToDto(
        this DataRightsExecutionWorkItem workItem) => new(
        workItem.Id,
        workItem.CaseId,
        workItem.PropertyId,
        workItem.ApprovalRevision,
        workItem.ExecutionRevision,
        (DataRightsOperation)workItem.Operation,
        workItem.OwnerKey,
        workItem.RecordType,
        workItem.RecordId,
        workItem.SelectedRecordVersion,
        workItem.PolicyEvidenceSchemaVersion,
        workItem.PolicyId,
        workItem.PolicyVersion,
        workItem.RetentionPolicyId,
        workItem.RetentionPolicyVersion,
        workItem.PolicyContentSha256,
        (DataRightsExecutionWorkItemStatus)workItem.State,
        workItem.AttemptCount,
        workItem.CreatedAtUtc,
        workItem.Version);
}
