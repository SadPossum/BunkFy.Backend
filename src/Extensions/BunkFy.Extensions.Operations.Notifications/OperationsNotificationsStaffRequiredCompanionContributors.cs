namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Microsoft.Extensions.Logging;

internal sealed class
    OperationsNotificationsStaffAccessExportCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger<
            OperationsNotificationsStaffAccessExportCompanionContributor>
            logger)
    : OperationsNotificationsStaffRequiredCompanionContributor(
        lifecycle,
        scopeContext,
        logger)
{
    public override string ContributorKey =>
        OperationsNotificationsDataRightsCoordinates
            .StaffAccessExportCompanionKey;

    public override DataRightsOperation Operation =>
        DataRightsOperation.AccessExport;
}

internal sealed class
    OperationsNotificationsStaffAnonymisationCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger<
            OperationsNotificationsStaffAnonymisationCompanionContributor>
            logger)
    : OperationsNotificationsStaffRequiredCompanionContributor(
        lifecycle,
        scopeContext,
        logger)
{
    public override string ContributorKey =>
        OperationsNotificationsDataRightsCoordinates
            .StaffAnonymisationCompanionKey;

    public override DataRightsOperation Operation =>
        DataRightsOperation.Anonymisation;
}

internal abstract class
    OperationsNotificationsStaffRequiredCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger logger)
    : IDataRightsRequiredCompanionContributor
{
    private const string InvalidRequest =
        "OperationsNotifications.StaffCompanionRequestInvalid";
    private const string CapacityExceeded =
        "OperationsNotifications.StaffCompanionCapacityExceeded";
    private const string LifecycleUnavailable =
        "OperationsNotifications.StaffCompanionLifecycleUnavailable";

    public abstract string ContributorKey { get; }

    public string SourceOwnerKey =>
        StaffDataRightsCoordinates.Owner;

    public string SourceRecordType =>
        StaffDataRightsCoordinates.StaffMemberRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public abstract DataRightsOperation Operation { get; }

    public int ContractVersion =>
        DataRightsRequiredCompanionContract.CurrentVersion;

    public async Task<DataRightsRequiredCompanionResult> ExpandAsync(
        DataRightsRequiredCompanionRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return DataRightsRequiredCompanionResult.Blocked(
                InvalidRequest);
        }

        if (request.RemainingSubjectCapacity < 1)
        {
            return DataRightsRequiredCompanionResult.Blocked(
                CapacityExceeded);
        }

        try
        {
            NotificationHistoryReference reference =
                OperationsNotificationsDataRightsCoordinates.ForStaff(
                    request.TenantId,
                    request.SourceCoordinate.RecordId);
            NotificationHistoryReferenceSnapshot snapshot =
                await lifecycle.EnsureOpenAsync(
                        request.TenantId,
                        reference,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (snapshot.Status !=
                    NotificationHistoryReferenceStatus.Open ||
                snapshot.Version <= 0)
            {
                return DataRightsRequiredCompanionResult.RetryRequired(
                    LifecycleUnavailable);
            }

            return DataRightsRequiredCompanionResult.Completed(
            [
                new DataRightsSubjectCoordinate(
                    OperationsNotificationsDataRightsCoordinates.Owner,
                    OperationsNotificationsDataRightsCoordinates
                        .StaffInboxHistoryRecordType,
                    request.SourceCoordinate.RecordId,
                    snapshot.Version)
            ]);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Operations Notifications Staff companion resolution failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return DataRightsRequiredCompanionResult.RetryRequired(
                LifecycleUnavailable);
        }
    }

    private bool IsValid(
        DataRightsRequiredCompanionRequest? request) =>
        request is not null &&
        request.ContractVersion == this.ContractVersion &&
        request.CaseType == this.CaseType &&
        request.Operation == this.Operation &&
        OperationsNotificationsDataRightsValidation.IsStaffTenantScope(
            scopeContext,
            request.TenantId,
            request.CaseType,
            request.PropertyId) &&
        request.CaseId != Guid.Empty &&
        request.RemainingSubjectCapacity >= 0 &&
        request.SourceCoordinate is not null &&
        string.Equals(
            request.SourceCoordinate.OwnerKey,
            this.SourceOwnerKey,
            StringComparison.Ordinal) &&
        string.Equals(
            request.SourceCoordinate.RecordType,
            this.SourceRecordType,
            StringComparison.Ordinal) &&
        request.SourceCoordinate.RecordId != Guid.Empty &&
        request.SourceCoordinate.RecordVersion > 0;
}
