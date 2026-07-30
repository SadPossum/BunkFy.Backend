namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Microsoft.Extensions.Logging;

internal sealed class OperationsNotificationsDataRightsDiscoveryContributor(
    INotificationHistoryLifecycle lifecycle,
    IScopeContext scopeContext,
    ILogger<OperationsNotificationsDataRightsDiscoveryContributor> logger)
    : IDataRightsSubjectDiscoveryContributor
{
    public string OwnerKey =>
        OperationsNotificationsDataRightsCoordinates.Owner;

    public IReadOnlyCollection<DataRightsCaseType> SupportedCaseTypes { get; } =
        [DataRightsCaseType.GuestRights];

    public Task<DataRightsSubjectDiscoveryResult> DiscoverAsync(
        DataRightsSubjectDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        DataRightsSubjectDiscoveryResult result =
            request is not null &&
            OperationsNotificationsDataRightsValidation.IsGuestPropertyScope(
                scopeContext,
                request.TenantId,
                request.CaseType,
                request.PropertyId) &&
            request.Lookup is not null &&
            request.MaxCandidates is > 0 and <=
                DataRightsSubjectDiscoveryLimits.MaxCandidates
                ? DataRightsSubjectDiscoveryResult.Success([])
                : DataRightsSubjectDiscoveryResult.ScopeUnavailable();
        return Task.FromResult(result);
    }

    public async Task<DataRightsSubjectSelectionValidation>
        ValidateSelectionAsync(
            DataRightsSubjectSelectionRequest request,
            CancellationToken cancellationToken)
    {
        if (request is null ||
            !OperationsNotificationsDataRightsValidation.IsGuestPropertyScope(
                scopeContext,
                request.TenantId,
                request.CaseType,
                request.PropertyId) ||
            !OperationsNotificationsDataRightsValidation
                .IsReservationHistoryCoordinate(request.Coordinate))
        {
            return DataRightsSubjectSelectionValidation.NotFound();
        }

        try
        {
            NotificationHistoryReference reference =
                OperationsNotificationsDataRightsCoordinates.ForReservation(
                    request.TenantId,
                    request.PropertyId!.Value,
                    request.Coordinate.RecordId);
            NotificationHistoryReferenceSnapshot snapshot =
                await lifecycle.GetSnapshotAsync(
                        request.TenantId,
                        reference,
                        cancellationToken)
                    .ConfigureAwait(false);
            return snapshot.Status switch
            {
                NotificationHistoryReferenceStatus.Open
                    when snapshot.Version ==
                         request.Coordinate.RecordVersion =>
                    DataRightsSubjectSelectionValidation.Valid(
                        new DataRightsSubjectCoordinate(
                            this.OwnerKey,
                            OperationsNotificationsDataRightsCoordinates
                                .ReservationHistoryRecordType,
                            request.Coordinate.RecordId,
                            snapshot.Version)),
                NotificationHistoryReferenceStatus.Open =>
                    DataRightsSubjectSelectionValidation.Stale(),
                NotificationHistoryReferenceStatus.Missing or
                    NotificationHistoryReferenceStatus.Closed =>
                    DataRightsSubjectSelectionValidation.NotFound(),
                _ => DataRightsSubjectSelectionValidation.ScopeUnavailable()
            };
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Operations Notifications Data Rights selection validation failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return DataRightsSubjectSelectionValidation.ScopeUnavailable();
        }
    }
}
