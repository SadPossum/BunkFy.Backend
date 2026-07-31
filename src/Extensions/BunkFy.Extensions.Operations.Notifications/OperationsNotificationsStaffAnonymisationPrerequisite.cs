namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Microsoft.Extensions.Logging;

internal sealed class
    OperationsNotificationsStaffAnonymisationPrerequisite(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger<
            OperationsNotificationsStaffAnonymisationPrerequisite> logger)
    : IDataRightsAnonymisationExecutionPrerequisiteV2,
      IDataRightsAnonymisationRestorePrerequisiteV3
{
    private const string InvalidRequest =
        "OperationsNotifications.StaffHistoryPrerequisiteInvalid";
    private const string StateConflict =
        "OperationsNotifications.StaffHistoryPrerequisiteStateConflict";
    private const string RetryRequired =
        "OperationsNotifications.StaffHistoryPrerequisiteRetryRequired";

    public string OwnerKey =>
        OperationsNotificationsDataRightsCoordinates.Owner;

    public string RecordType =>
        OperationsNotificationsDataRightsCoordinates
            .StaffInboxHistoryRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    int IDataRightsAnonymisationExecutionPrerequisiteV2
        .ContractVersion =>
        DataRightsAnonymisationExecutionPrerequisiteContractV2
            .CurrentVersion;

    int IDataRightsAnonymisationRestorePrerequisiteV3
        .ContractVersion =>
        DataRightsAnonymisationRestoreContractV3.CurrentVersion;

    public async Task<
        DataRightsAnonymisationExecutionPrerequisiteResult>
        ExecuteAsync(
            DataRightsAnonymisationContributionRequestV2 request,
            CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return ExecutionBlocked(InvalidRequest);
        }

        try
        {
            NotificationHistoryReference reference =
                OperationsNotificationsDataRightsCoordinates.ForStaff(
                    request.TenantId,
                    request.Coordinate.RecordId);
            NotificationHistoryReferenceSnapshot snapshot =
                await lifecycle.GetSnapshotAsync(
                        request.TenantId,
                        reference,
                        cancellationToken)
                    .ConfigureAwait(false);
            bool openFrozenState =
                snapshot.Status ==
                    NotificationHistoryReferenceStatus.Open &&
                snapshot.Version == request.Coordinate.RecordVersion &&
                OperationsNotificationsStaffHistoryPolicyEvidence
                    .MatchesFrozenBinding(
                        request.ApprovalEvidence,
                        reference,
                        snapshot);
            bool exactClosedReplay =
                request.Coordinate.RecordVersion < long.MaxValue &&
                snapshot.Status ==
                    NotificationHistoryReferenceStatus.Closed &&
                snapshot.Version ==
                    request.Coordinate.RecordVersion + 1;
            return openFrozenState || exactClosedReplay
                ? DataRightsAnonymisationExecutionPrerequisiteResult
                    .Completed(
                        DataRightsAnonymisationExecutionPrerequisiteContractV2
                            .CurrentVersion)
                : ExecutionBlocked(StateConflict);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Operations Notifications Staff history prerequisite failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return DataRightsAnonymisationExecutionPrerequisiteResult
                .RetryRequired(
                    DataRightsAnonymisationExecutionPrerequisiteContractV2
                        .CurrentVersion,
                    RetryRequired);
        }
    }

    public async Task<
        DataRightsAnonymisationRestorePrerequisiteResult>
        ExecuteAsync(
            DataRightsAnonymisationRestoreRequestV3 request,
            CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return RestoreBlocked(InvalidRequest);
        }

        try
        {
            long selectedVersion = request.ResultingRecordVersion - 1;
            NotificationHistoryReference reference =
                OperationsNotificationsDataRightsCoordinates.ForStaff(
                    request.TenantId,
                    request.RecordId);
            NotificationHistoryReferenceSnapshot snapshot =
                await lifecycle.GetSnapshotAsync(
                        request.TenantId,
                        reference,
                        cancellationToken)
                    .ConfigureAwait(false);
            bool exactPreClose =
                snapshot.Status ==
                    NotificationHistoryReferenceStatus.Open &&
                snapshot.Version == selectedVersion;
            bool exactPostClose =
                snapshot.Status ==
                    NotificationHistoryReferenceStatus.Closed &&
                snapshot.Version == request.ResultingRecordVersion;
            return exactPreClose || exactPostClose
                ? DataRightsAnonymisationRestorePrerequisiteResult
                    .Completed(
                        DataRightsAnonymisationRestoreContractV3
                            .CurrentVersion)
                : RestoreBlocked(StateConflict);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Operations Notifications Staff history restore prerequisite failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return DataRightsAnonymisationRestorePrerequisiteResult
                .RetryRequired(
                    DataRightsAnonymisationRestoreContractV3.CurrentVersion,
                    RetryRequired);
        }
    }

    private bool IsValid(
        DataRightsAnonymisationContributionRequestV2? request) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationExecutionPrerequisiteContractV2
                .CurrentVersion &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        OperationsNotificationsDataRightsValidation.IsStaffTenantScope(
            scopeContext,
            request.TenantId,
            request.CaseType,
            request.PropertyId) &&
        OperationsNotificationsDataRightsValidation
            .IsStaffHistoryCoordinate(request.Coordinate) &&
        OperationsNotificationsDataRightsValidation
            .IsStaffApprovalEvidence(request.ApprovalEvidence);

    private bool IsValid(
        DataRightsAnonymisationRestoreRequestV3? request) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationRestoreContractV3.CurrentVersion &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        OperationsNotificationsDataRightsValidation.IsStaffTenantScope(
            scopeContext,
            request.TenantId,
            request.CaseType,
            request.RoutingPropertyId) &&
        string.Equals(
            request.OwnerKey,
            this.OwnerKey,
            StringComparison.Ordinal) &&
        string.Equals(
            request.RecordType,
            this.RecordType,
            StringComparison.Ordinal) &&
        request.RecordId != Guid.Empty &&
        request.OwnerReceiptContractVersion ==
            OperationsNotificationsDataRightsReceipt.ContractVersion &&
        request.OwnerReceiptId != Guid.Empty &&
        OperationsNotificationsDataRightsValidation.IsSha256(
            request.OwnerReceiptSha256) &&
        request.ResultingRecordVersion > 1;

    private static
        DataRightsAnonymisationExecutionPrerequisiteResult
        ExecutionBlocked(string code) =>
        DataRightsAnonymisationExecutionPrerequisiteResult.Blocked(
            DataRightsAnonymisationExecutionPrerequisiteContractV2
                .CurrentVersion,
            code);

    private static
        DataRightsAnonymisationRestorePrerequisiteResult
        RestoreBlocked(string code) =>
        DataRightsAnonymisationRestorePrerequisiteResult.Blocked(
            DataRightsAnonymisationRestoreContractV3.CurrentVersion,
            code);
}
