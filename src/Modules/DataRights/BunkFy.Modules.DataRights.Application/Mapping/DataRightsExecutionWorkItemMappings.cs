namespace BunkFy.Modules.DataRights.Application.Mapping;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public static class DataRightsExecutionWorkItemMappings
{
    public static DataRightsExecutionWorkItemDto ToDto(
        this DataRightsExecutionWorkItem workItem) => new(
        workItem.Id,
        workItem.BatchId,
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
        workItem.OwnerContractVersion,
        (DataRightsExecutionWorkItemStatus)workItem.State,
        workItem.AttemptCount,
        workItem.TaskRunId,
        workItem.LastTaskAttempt,
        workItem.LastAttemptAtUtc,
        workItem.OwnerReceiptContractVersion,
        workItem.OwnerReceiptId,
        workItem.ResultingRecordVersion,
        workItem.OwnerDispositionCode,
        workItem.OwnerReasonCode,
        workItem.OwnerReceiptSha256,
        workItem.OwnerCompletedAtUtc,
        workItem.OutcomeCode,
        workItem.OutcomeAtUtc,
        workItem.CreatedAtUtc,
        workItem.Version,
        (DataRightsCaseType)workItem.CaseKind,
        (DataRightsExecutionScopeKind)workItem.ScopeKind,
        workItem.PolicyPurposeCode,
        workItem.PolicySurface,
        workItem.PolicySourceProvenance,
        EmptyToNull(workItem.PolicyRetentionDataClass),
        EmptyToNull(workItem.PolicyRetentionTrigger),
        workItem.PolicyRetentionTriggeredAtUtc,
        workItem.PolicyRetentionDeadlineUtc,
        workItem.PolicyEvaluatedAtUtc,
        workItem.PolicyStateBindingsSha256,
        workItem.PolicyPropertyVersion,
        workItem.PolicyOperatingCountryCode,
        workItem.PolicyRequiresDistinctExecutor);

    private static string? EmptyToNull(string value) =>
        value.Length == 0 ? null : value;
}
