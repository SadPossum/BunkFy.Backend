namespace BunkFy.Modules.Workspaces.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Logging;

internal sealed class WorkspaceStaffAnonymisationAccessPrerequisite(
    IStaffAnonymisationRestoreStateReader staffStateReader,
    IWorkspaceStaffAccessProcessRepository accessProcesses,
    IWorkspaceStaffRetentionCorrelationRepository correlations,
    IRequestDispatcher dispatcher,
    WorkspaceStaffAccessDenier accessDenier,
    ISystemClock clock,
    IIdGenerator ids,
    ILogger<WorkspaceStaffAnonymisationAccessPrerequisite> logger)
    : IDataRightsAnonymisationExecutionPrerequisiteV2,
      IDataRightsAnonymisationRestorePrerequisiteV3,
      IStaffRetentionAnonymisationPrerequisite
{
    private const string RetentionContributorKey =
        "workspace-access";
    private const string RequestInvalid =
        "Workspaces.StaffAnonymisationRequestInvalid";
    private const string StateUnavailable =
        "Workspaces.StaffAnonymisationStateUnavailable";
    private const string StateConflict =
        "Workspaces.StaffAnonymisationStateConflict";
    private const string AccessMappingUnavailable =
        "Workspaces.StaffAnonymisationAccessMappingUnavailable";
    private const string AccessMappingConflict =
        "Workspaces.StaffAnonymisationAccessMappingConflict";
    private const string OwnerProtected =
        "Workspaces.StaffAnonymisationOwnerProtected";
    private const string RetryRequired =
        "Workspaces.StaffAnonymisationAccessRetryRequired";
    private const string CorrelationReceiptInvalid =
        "Workspaces.StaffRetentionCorrelationReceiptInvalid";
    private const string CorrelationScrubRetryRequired =
        "Workspaces.StaffRetentionCorrelationScrubRetryRequired";

    public string OwnerKey => StaffDataRightsCoordinates.Owner;

    public string RecordType =>
        StaffDataRightsCoordinates.StaffMemberRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public string ContributorKey => RetentionContributorKey;

    int IDataRightsAnonymisationExecutionPrerequisiteV2.ContractVersion =>
        DataRightsAnonymisationExecutionPrerequisiteContractV2
            .CurrentVersion;

    int IDataRightsAnonymisationRestorePrerequisiteV3.ContractVersion =>
        DataRightsAnonymisationRestoreContractV3.CurrentVersion;

    public async Task<DataRightsAnonymisationExecutionPrerequisiteResult>
        ExecuteAsync(
            DataRightsAnonymisationContributionRequestV2 request,
            CancellationToken cancellationToken)
    {
        if (!IsValid(request))
        {
            return ExecutionBlocked(RequestInvalid);
        }

        AccessClosureResult result = await this.EnsureClosedAsync(
            request.TenantId,
            request.Coordinate.RecordId,
            request.Coordinate.RecordVersion,
            resultingRecordVersion: null,
            correlationProofExists: false,
            cancellationToken).ConfigureAwait(false);
        return result.Status switch
        {
            AccessClosureStatus.Completed =>
                DataRightsAnonymisationExecutionPrerequisiteResult.Completed(
                    DataRightsAnonymisationExecutionPrerequisiteContractV2
                        .CurrentVersion),
            AccessClosureStatus.Blocked => ExecutionBlocked(result.Code),
            _ =>
                DataRightsAnonymisationExecutionPrerequisiteResult
                    .RetryRequired(
                        DataRightsAnonymisationExecutionPrerequisiteContractV2
                            .CurrentVersion,
                        result.Code)
        };
    }

    public async Task<DataRightsAnonymisationRestorePrerequisiteResult>
        ExecuteAsync(
            DataRightsAnonymisationRestoreRequestV3 request,
            CancellationToken cancellationToken)
    {
        if (!IsValid(request))
        {
            return RestoreBlocked(RequestInvalid);
        }

        AccessClosureResult result = await this.EnsureClosedAsync(
            request.TenantId,
            request.RecordId,
            request.ResultingRecordVersion - 1,
            request.ResultingRecordVersion,
            correlationProofExists: false,
            cancellationToken).ConfigureAwait(false);
        return result.Status switch
        {
            AccessClosureStatus.Completed =>
                DataRightsAnonymisationRestorePrerequisiteResult.Completed(
                    DataRightsAnonymisationRestoreContractV3.CurrentVersion),
            AccessClosureStatus.Blocked => RestoreBlocked(result.Code),
            _ =>
                DataRightsAnonymisationRestorePrerequisiteResult
                    .RetryRequired(
                        DataRightsAnonymisationRestoreContractV3
                            .CurrentVersion,
                        result.Code)
        };
    }

    public async Task<StaffRetentionAnonymisationPrerequisiteResult>
        ExecuteAsync(
            StaffRetentionAnonymisationPrerequisiteRequest request,
            CancellationToken cancellationToken)
    {
        if (!IsValid(request))
        {
            return StaffRetentionAnonymisationPrerequisiteResult.Blocked(
                RequestInvalid);
        }

        try
        {
            WorkspaceStaffRetentionCorrelationReceipt? existing =
                await correlations.GetAsync(
                    request.StaffMemberId,
                    request.SelectedStaffVersion,
                    cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                if (!existing.Matches(
                        request.TenantId,
                        request.StaffMemberId,
                        request.SelectedStaffVersion))
                {
                    return StaffRetentionAnonymisationPrerequisiteResult
                        .Blocked(CorrelationReceiptInvalid);
                }
            }

            AccessClosureResult closure =
                await this.EnsureClosedAsync(
                    request.TenantId,
                    request.StaffMemberId,
                    request.SelectedStaffVersion,
                    resultingRecordVersion: null,
                    correlationProofExists: existing is not null,
                    cancellationToken).ConfigureAwait(false);
            if (closure.Status == AccessClosureStatus.Blocked)
            {
                return StaffRetentionAnonymisationPrerequisiteResult
                    .Blocked(closure.Code);
            }

            if (closure.Status != AccessClosureStatus.Completed)
            {
                return StaffRetentionAnonymisationPrerequisiteResult
                    .RetryRequired(closure.Code);
            }

            if (existing is not null)
            {
                return StaffRetentionAnonymisationPrerequisiteResult
                    .Completed();
            }

            Result<WorkspaceStaffRetentionCorrelationReceipt> scrubbed =
                await dispatcher.SendAsync(
                    new ScrubWorkspaceStaffRetentionCorrelationCommand(
                        ids.NewId(),
                        request.ExecutionId,
                        request.TenantId,
                        request.StaffMemberId,
                        request.SelectedStaffVersion,
                        closure.SubjectId,
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            if (scrubbed.IsSuccess)
            {
                return StaffRetentionAnonymisationPrerequisiteResult
                    .Completed();
            }

            return IsBlockedScrubFailure(scrubbed.Error)
                ? StaffRetentionAnonymisationPrerequisiteResult
                    .Blocked(scrubbed.Error.Code)
                : StaffRetentionAnonymisationPrerequisiteResult
                    .RetryRequired(
                        CorrelationScrubRetryRequired);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Workspace Staff retention correlation scrub failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return StaffRetentionAnonymisationPrerequisiteResult
                .RetryRequired(CorrelationScrubRetryRequired);
        }
    }

    private async Task<AccessClosureResult> EnsureClosedAsync(
        string tenantId,
        Guid staffMemberId,
        long departureVersion,
        long? resultingRecordVersion,
        bool correlationProofExists,
        CancellationToken cancellationToken)
    {
        try
        {
            StaffAnonymisationRestoreState? state =
                await staffStateReader.ReadAsync(
                    tenantId,
                    staffMemberId,
                    cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                return AccessClosureResult.Blocked(StateUnavailable);
            }

            string? subjectId;
            if (state.State ==
                    StaffAnonymisationRestoreRecordState.Departed &&
                state.Version == departureVersion)
            {
                subjectId = state.AuthSubjectId;
                if (string.IsNullOrWhiteSpace(subjectId))
                {
                    return AccessClosureResult.Complete(
                        subjectId: null);
                }
            }
            else if (resultingRecordVersion.HasValue &&
                     state.State ==
                        StaffAnonymisationRestoreRecordState.Anonymised &&
                     state.Version == resultingRecordVersion.Value &&
                     state.AuthSubjectId is null)
            {
                subjectId = null;
            }
            else
            {
                return AccessClosureResult.Blocked(StateConflict);
            }

            if (correlationProofExists)
            {
                if (string.IsNullOrWhiteSpace(subjectId))
                {
                    return AccessClosureResult.Complete(
                        subjectId: null);
                }

                string replaySubjectId = subjectId.Trim();
                WorkspaceStaffAccessCoordinationOutcome replayDenied =
                    await accessDenier.EnsureAccessDeniedAsync(
                        tenantId,
                        replaySubjectId,
                        WorkspaceStaffAccessTargetState.Departed,
                        cancellationToken).ConfigureAwait(false);
                return replayDenied switch
                {
                    WorkspaceStaffAccessCoordinationOutcome.Allowed =>
                        AccessClosureResult.Complete(
                            replaySubjectId),
                    WorkspaceStaffAccessCoordinationOutcome.OwnerProtected =>
                        AccessClosureResult.Blocked(OwnerProtected),
                    _ => AccessClosureResult.Retry(RetryRequired)
                };
            }

            WorkspaceStaffAccessProcess? process =
                await accessProcesses.GetCompletedDepartureAsync(
                    staffMemberId,
                    departureVersion,
                    cancellationToken).ConfigureAwait(false);
            if (process is null)
            {
                return AccessClosureResult.Blocked(
                    AccessMappingUnavailable);
            }

            if (!string.Equals(
                    process.ScopeId,
                    tenantId,
                    StringComparison.Ordinal) ||
                process.StaffMemberId != staffMemberId ||
                process.TargetStaffVersion != departureVersion ||
                process.TargetState !=
                    WorkspaceStaffAccessTargetState.Departed ||
                process.State != WorkspaceStaffAccessProcessState.Completed ||
                (subjectId is not null &&
                 !string.Equals(
                     process.SubjectId,
                     subjectId,
                     StringComparison.Ordinal)))
            {
                return AccessClosureResult.Blocked(AccessMappingConflict);
            }

            WorkspaceStaffAccessCoordinationOutcome denied =
                await accessDenier.EnsureAccessDeniedAsync(
                    process.ScopeId,
                    process.SubjectId,
                    WorkspaceStaffAccessTargetState.Departed,
                    cancellationToken).ConfigureAwait(false);
            return denied switch
            {
                WorkspaceStaffAccessCoordinationOutcome.Allowed =>
                    AccessClosureResult.Complete(
                        process.SubjectId),
                WorkspaceStaffAccessCoordinationOutcome.OwnerProtected =>
                    AccessClosureResult.Blocked(OwnerProtected),
                _ => AccessClosureResult.Retry(RetryRequired)
            };
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Workspace Staff anonymisation access closure failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return AccessClosureResult.Retry(RetryRequired);
        }
    }

    private static bool IsValid(
        DataRightsAnonymisationContributionRequestV2? request) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationExecutionPrerequisiteContractV2
                .CurrentVersion &&
        request.CaseType == DataRightsCaseType.StaffRights &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        request.PropertyId is null &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0 &&
        string.Equals(
            request.Coordinate.OwnerKey,
            StaffDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            StaffDataRightsCoordinates.StaffMemberRecordType,
            StringComparison.Ordinal);

    private static bool IsValid(
        DataRightsAnonymisationRestoreRequestV3? request) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationRestoreContractV3.CurrentVersion &&
        request.CaseType == DataRightsCaseType.StaffRights &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        request.RoutingPropertyId is null &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        request.RecordId != Guid.Empty &&
        request.ResultingRecordVersion > 1 &&
        string.Equals(
            request.OwnerKey,
            StaffDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.RecordType,
            StaffDataRightsCoordinates.StaffMemberRecordType,
            StringComparison.Ordinal);

    private static bool IsValid(
        StaffRetentionAnonymisationPrerequisiteRequest? request) =>
        request is not null &&
        request.ContractVersion ==
            StaffRetentionAnonymisationPrerequisiteContract
                .CurrentVersion &&
        request.ExecutionId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        request.StaffMemberId != Guid.Empty &&
        request.SelectedStaffVersion > 0;

    private static DataRightsAnonymisationExecutionPrerequisiteResult
        ExecutionBlocked(string code) =>
        DataRightsAnonymisationExecutionPrerequisiteResult.Blocked(
            DataRightsAnonymisationExecutionPrerequisiteContractV2
                .CurrentVersion,
            code);

    private static DataRightsAnonymisationRestorePrerequisiteResult
        RestoreBlocked(string code) =>
        DataRightsAnonymisationRestorePrerequisiteResult.Blocked(
            DataRightsAnonymisationRestoreContractV3.CurrentVersion,
            code);

    private static bool IsBlockedScrubFailure(Error error) =>
        error == WorkspaceStaffRetentionErrors.RequestInvalid ||
        error == WorkspaceStaffRetentionErrors.ReceiptInvalid ||
        error == WorkspaceStaffRetentionErrors.ActiveOnboarding ||
        error == WorkspaceStaffRetentionErrors.ActiveAccessProcess ||
        error == WorkspaceStaffRetentionErrors.AccessMappingConflict;

    private enum AccessClosureStatus
    {
        Completed,
        Blocked,
        RetryRequired
    }

    private sealed record AccessClosureResult(
        AccessClosureStatus Status,
        string Code,
        string? SubjectId)
    {
        public static AccessClosureResult Complete(
            string? subjectId) =>
            new(
                AccessClosureStatus.Completed,
                string.Empty,
                subjectId);

        public static AccessClosureResult Blocked(string code) =>
            new(
                AccessClosureStatus.Blocked,
                code,
                SubjectId: null);

        public static AccessClosureResult Retry(string code) =>
            new(
                AccessClosureStatus.RetryRequired,
                code,
                SubjectId: null);
    }
}
