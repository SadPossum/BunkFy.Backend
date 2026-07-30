namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Microsoft.Extensions.Logging;

internal sealed class
    OperationsNotificationsReservationAccessExportCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger<
            OperationsNotificationsReservationAccessExportCompanionContributor>
            logger)
    : OperationsNotificationsReservationRequiredCompanionContributor(
        lifecycle,
        scopeContext,
        logger)
{
    public override string ContributorKey =>
        OperationsNotificationsDataRightsCoordinates
            .ReservationAccessExportCompanionKey;

    public override DataRightsOperation Operation =>
        DataRightsOperation.AccessExport;

    protected override Task<NotificationHistoryReferenceSnapshot>
        ResolveSnapshotAsync(
            string tenantId,
            NotificationHistoryReference reference,
            CancellationToken cancellationToken) =>
        this.Lifecycle.EnsureOpenAsync(
            tenantId,
            reference,
            cancellationToken);
}

internal sealed class
    OperationsNotificationsReservationAnonymisationCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger<
            OperationsNotificationsReservationAnonymisationCompanionContributor>
            logger)
    : OperationsNotificationsReservationRequiredCompanionContributor(
        lifecycle,
        scopeContext,
        logger)
{
    public override string ContributorKey =>
        OperationsNotificationsDataRightsCoordinates
            .ReservationAnonymisationCompanionKey;

    public override DataRightsOperation Operation =>
        DataRightsOperation.Anonymisation;

    protected override Task<NotificationHistoryReferenceSnapshot>
        ResolveSnapshotAsync(
            string tenantId,
            NotificationHistoryReference reference,
            CancellationToken cancellationToken) =>
        this.Lifecycle.EnsureOpenAsync(
            tenantId,
            reference,
            cancellationToken);
}

internal abstract class
    OperationsNotificationsReservationRequiredCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger logger)
    : IDataRightsRequiredCompanionContributor
{
    private const string InvalidRequest =
        "OperationsNotifications.CompanionRequestInvalid";
    private const string CapacityExceeded =
        "OperationsNotifications.CompanionCapacityExceeded";
    private const string LifecycleUnavailable =
        "OperationsNotifications.CompanionLifecycleUnavailable";

    protected INotificationHistoryLifecycle Lifecycle { get; } = lifecycle;

    public abstract string ContributorKey { get; }

    public string SourceOwnerKey =>
        ReservationsDataRightsCoordinates.Owner;

    public string SourceRecordType =>
        ReservationsDataRightsCoordinates.ReservationRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.GuestRights;

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

        try
        {
            NotificationHistoryReference reference =
                OperationsNotificationsDataRightsCoordinates.ForReservation(
                    request.TenantId,
                    request.PropertyId!.Value,
                    request.SourceCoordinate.RecordId);
            NotificationHistoryReferenceSnapshot snapshot =
                await this.ResolveSnapshotAsync(
                        request.TenantId,
                        reference,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (snapshot.Status is
                NotificationHistoryReferenceStatus.Missing or
                NotificationHistoryReferenceStatus.Closed)
            {
                return DataRightsRequiredCompanionResult.Completed();
            }

            if (snapshot.Status !=
                    NotificationHistoryReferenceStatus.Open ||
                snapshot.Version <= 0)
            {
                return DataRightsRequiredCompanionResult.RetryRequired(
                    LifecycleUnavailable);
            }

            if (request.RemainingSubjectCapacity < 1)
            {
                return DataRightsRequiredCompanionResult.Blocked(
                    CapacityExceeded);
            }

            return DataRightsRequiredCompanionResult.Completed(
            [
                new DataRightsSubjectCoordinate(
                    OperationsNotificationsDataRightsCoordinates.Owner,
                    OperationsNotificationsDataRightsCoordinates
                        .ReservationHistoryRecordType,
                    request.SourceCoordinate.RecordId,
                    snapshot.Version)
            ]);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Operations Notifications companion resolution failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return DataRightsRequiredCompanionResult.RetryRequired(
                LifecycleUnavailable);
        }
    }

    protected abstract Task<NotificationHistoryReferenceSnapshot>
        ResolveSnapshotAsync(
            string tenantId,
            NotificationHistoryReference reference,
            CancellationToken cancellationToken);

    private bool IsValid(
        DataRightsRequiredCompanionRequest? request) =>
        request is not null &&
        request.ContractVersion == this.ContractVersion &&
        request.CaseType == this.CaseType &&
        request.Operation == this.Operation &&
        OperationsNotificationsDataRightsValidation.IsGuestPropertyScope(
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
