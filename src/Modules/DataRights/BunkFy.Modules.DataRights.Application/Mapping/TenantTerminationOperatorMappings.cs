namespace BunkFy.Modules.DataRights.Application.Mapping;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;

internal static class TenantTerminationOperatorMappings
{
    public static TenantTerminationCaseDto ToTenantTerminationDto(
        this DataRightsCase dataRightsCase) =>
        new(
            dataRightsCase.Id,
            (DataRightsRequesterRelationship)
                dataRightsCase.RequesterRelationship,
            dataRightsCase.TenantTerminationExportRequested ?? false,
            (DataRightsCaseStatus)dataRightsCase.Status,
            (DataRightsDecisionOutcome)dataRightsCase.Decision,
            (DataRightsDecisionReason)dataRightsCase.DecisionReason,
            dataRightsCase.DecisionRevision,
            dataRightsCase.DecidedAtUtc,
            dataRightsCase.ExecutionRevision,
            dataRightsCase.ExecutionStartedAtUtc,
            dataRightsCase.Version,
            dataRightsCase.CreatedAtUtc,
            dataRightsCase.LastChangedAtUtc);

    public static TenantTerminationProcessDto ToDto(
        this TenantTerminationProcess process) =>
        new(
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            process.ExportRequested,
            (TenantTerminationPhase)process.Phase,
            (TenantTerminationStatus)process.Status,
            process.OperationRevision,
            process.OutcomeCode,
            process.HoldReviewAtUtc,
            process.Version,
            process.CreatedAtUtc,
            process.LastChangedAtUtc);

    public static TenantTerminationOwnerWorkItemDto ToDto(
        this TenantTerminationOwnerWorkItem workItem) =>
        new(
            workItem.Id,
            workItem.OwnerKey,
            (TenantTerminationOwnerWorkPhase)workItem.Phase,
            (TenantTerminationOwnerWorkStatus)workItem.State,
            workItem.OperationRevision,
            workItem.AttemptCount,
            workItem.TaskRunId,
            workItem.LastTaskAttempt,
            workItem.LastAttemptAtUtc,
            workItem.ResultCode,
            workItem.AffectedCount,
            workItem.RemainingActiveCount,
            workItem.HoldReviewAtUtc,
            workItem.ResultRecordedAtUtc,
            workItem.Version);
}
