namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetTenantTerminationOperatorStatusQueryHandler(
    ITenantTerminationCaseRepository cases,
    ITenantTerminationOperatorStatusRepository statusRepository)
    : IQueryHandler<GetTenantTerminationOperatorStatusQuery,
        TenantTerminationOperatorStatusDto>
{
    public async Task<Result<TenantTerminationOperatorStatusDto>> HandleAsync(
        GetTenantTerminationOperatorStatusQuery query,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            query.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<TenantTerminationOperatorStatusDto>(
                DataRightsApplicationErrors.TenantTerminationCaseNotFound);
        }

        TenantTerminationProcess? process =
            await statusRepository.GetProcessByCaseIdAsync(
                dataRightsCase.Id,
                cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems =
            process is null
                ? []
                : await statusRepository.ListOwnerWorkItemsAsync(
                    process.Id,
                    cancellationToken).ConfigureAwait(false);
        if (!IsConsistent(dataRightsCase, process, workItems))
        {
            return Result.Failure<TenantTerminationOperatorStatusDto>(
                DataRightsApplicationErrors
                    .TenantTerminationExecutionStateInvalid);
        }

        return Result.Success(new TenantTerminationOperatorStatusDto(
            dataRightsCase.ToTenantTerminationDto(),
            process?.ToDto(),
            workItems.Select(item => item.ToDto()).ToArray()));
    }

    private static bool IsConsistent(
        DataRightsCase dataRightsCase,
        TenantTerminationProcess? process,
        IReadOnlyCollection<TenantTerminationOwnerWorkItem> workItems)
    {
        bool isTenantTermination =
            dataRightsCase.Kind == DataRightsCaseKind.TenantTermination &&
            dataRightsCase.RequestedOperations ==
                DataRightsCaseOperation.Anonymisation &&
            dataRightsCase.TenantTerminationExportRequested.HasValue;
        if (!isTenantTermination)
        {
            return false;
        }

        if (process is null)
        {
            return workItems.Count == 0 &&
                dataRightsCase.Status is
                    DataRightsCaseState.ReviewRequired or
                    DataRightsCaseState.Approved or
                    DataRightsCaseState.Denied;
        }

        DataRightsCaseState expectedCaseStatus = process.Status switch
        {
            TenantTerminationProcessStatus.Completed =>
                DataRightsCaseState.Completed,
            TenantTerminationProcessStatus.Cancelled =>
                DataRightsCaseState.Canceled,
            _ => DataRightsCaseState.Executing
        };
        return dataRightsCase.Status == expectedCaseStatus &&
            dataRightsCase.Decision == DataRightsCaseDecision.Approved &&
            dataRightsCase.Id == process.CaseId &&
            string.Equals(
                dataRightsCase.ScopeId,
                process.ScopeId,
                StringComparison.Ordinal) &&
            dataRightsCase.DecisionRevision == process.ApprovalRevision &&
            dataRightsCase.TenantTerminationExportRequested ==
                process.ExportRequested &&
            dataRightsCase.DecidedAtUtc == process.ApprovedAtUtc &&
            dataRightsCase.ExecutionStartedAtUtc == process.CreatedAtUtc &&
            string.Equals(
                dataRightsCase.DecidedBy,
                process.ApprovedBy,
                StringComparison.Ordinal) &&
            string.Equals(
                dataRightsCase.ExecutionStartedBy,
                process.CreatedBy,
                StringComparison.Ordinal) &&
            TenantTerminationApprovalEvidenceContract.FixedTimeSha256Equals(
                dataRightsCase.TenantTerminationPolicyEvidenceSha256,
                process.PolicyEvidenceSha256) &&
            workItems.Count <=
                TenantTerminationProcess.MaximumFrozenOwners * 5 &&
            workItems.All(item =>
                item.ProcessId == process.Id &&
                item.OperationRevision <= process.OperationRevision);
    }
}
