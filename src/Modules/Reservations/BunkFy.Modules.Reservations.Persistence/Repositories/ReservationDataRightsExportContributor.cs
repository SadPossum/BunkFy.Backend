namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationDataRightsExportContributor(
    ReservationsDbContext dbContext,
    IScopeContext scopeContext) : IDataRightsSubjectExportContributor
{
    public const string PendingAmendmentRecordType = "reservation-pending-amendment";
    public const string GuestLinkRecordType = "reservation-guest-link";
    public const string DetailsHistoryRecordType = "reservation-details-history";
    public const string ExternalOperationRecordType = "reservation-external-operation";
    public const string ArrivalReminderRecordType = "reservation-arrival-reminder";
    public const string DataRightsCorrectionReceiptRecordType =
        "reservation-data-rights-correction-receipt";
    public const string ProcessingRestrictionRecordType =
        "reservation-processing-restriction";
    public const string ProcessingRestrictionStateRecordType =
        "reservation-processing-restriction-state";
    public const string ProcessingRestrictionReceiptRecordType =
        "reservation-processing-restriction-receipt";

    public string OwnerKey => ReservationDataRightsDiscoveryContributor.Owner;

    public DataRightsExportDescriptor Descriptor => ReservationDataRightsExportSchema.Descriptor;

    public async Task<DataRightsSubjectExportResult> ExportAsync(
        DataRightsSubjectExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        if (!this.IsValidScope(request.TenantId, request.PropertyId))
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        if (request.Coordinate is not { } coordinate ||
            !string.Equals(
                coordinate.OwnerKey,
                ReservationDataRightsDiscoveryContributor.Owner,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                coordinate.RecordType,
                ReservationDataRightsDiscoveryContributor.ReservationRecordType,
                StringComparison.OrdinalIgnoreCase) ||
            coordinate.RecordId == Guid.Empty ||
            coordinate.RecordVersion <= 0)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (!await this.IsKnownPropertyAsync(request.PropertyId, cancellationToken)
                .ConfigureAwait(false))
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        Reservation? reservation = await dbContext.Reservations
            .AsNoTracking()
            .Include(candidate => candidate.RequestedUnits)
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.PropertyId == request.PropertyId &&
                    candidate.Id == coordinate.RecordId,
                cancellationToken)
            .ConfigureAwait(false);
        if (reservation is null)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (reservation.Version != coordinate.RecordVersion)
        {
            return DataRightsSubjectExportResult.Stale();
        }

        ReservationDataRightsExport reservationExport = new(
            reservation.Id,
            reservation.PropertyId,
            reservation.AllocationRequestId,
            reservation.AllocationId,
            reservation.AllocationVersion,
            reservation.AllocationRejection,
            reservation.ReleaseRequestId,
            reservation.LastReleaseRejectionCode,
            reservation.LastAllocationAmendmentRejectionCode,
            reservation.PendingStayBusinessDate,
            reservation.CheckedInBusinessDate,
            reservation.CheckedInAtUtc,
            reservation.NoShowBusinessDate,
            reservation.NoShowAtUtc,
            reservation.CheckedOutBusinessDate,
            reservation.CheckedOutAtUtc,
            reservation.Arrival,
            reservation.Departure,
            reservation.ExpectedArrivalTime,
            reservation.ExpectedDepartureTime,
            reservation.RequestedUnits
                .Select(unit => unit.InventoryUnitId)
                .Order()
                .ToArray(),
            reservation.PrimaryGuestName,
            reservation.Email,
            reservation.Phone,
            reservation.GuestCount,
            reservation.Source,
            reservation.SourceSystem,
            reservation.SourceReference,
            reservation.Notes,
            reservation.DetailsRevision,
            reservation.LastDetailsChangeOrigin,
            reservation.LastDetailsAdapterConnectionId,
            reservation.LastDetailsExternalOperationId,
            reservation.LastDetailsChangedAtUtc,
            reservation.Status,
            reservation.Version,
            reservation.CreatedAtUtc,
            reservation.UpdatedAtUtc);
        await sink.WriteAsync(
            ReservationDataRightsExportSchema.CreateReservationRecord(reservationExport),
            cancellationToken).ConfigureAwait(false);
        int recordCount = 1;

        if (reservation.PendingAllocationAmendmentId.HasValue)
        {
            ReservationPendingAmendmentDataRightsExport pending = new(
                reservation.PendingAllocationAmendmentId.Value,
                reservation.Id,
                reservation.PendingAllocationAmendmentRequestFingerprint!,
                reservation.PendingArrival!.Value,
                reservation.PendingDeparture!.Value,
                reservation.PendingExpectedArrivalTime,
                reservation.PendingExpectedDepartureTime,
                ParseInventoryUnitIds(reservation.PendingInventoryUnitIds),
                reservation.PendingPrimaryGuestName!,
                reservation.PendingEmail,
                reservation.PendingPhone,
                reservation.PendingGuestCount!.Value,
                reservation.PendingNotes,
                reservation.PendingDetailsChangeOrigin,
                reservation.PendingDetailsAdapterConnectionId,
                reservation.PendingDetailsExternalOperationId,
                reservation.PendingDetailsCorrelationId!.Value,
                reservation.Version);
            await sink.WriteAsync(
                ReservationDataRightsExportSchema.CreatePendingAmendmentRecord(pending),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        IQueryable<ReservationGuestLinkDataRightsExport> links = dbContext.ReservationGuests
            .AsNoTracking()
            .Where(link => link.ReservationId == reservation.Id)
            .OrderBy(link => link.Id)
            .Select(link => new ReservationGuestLinkDataRightsExport(
                link.Id,
                link.ReservationId,
                reservation.PropertyId,
                link.Role,
                link.LinkedAtUtc,
                link.LinkVersion,
                link.IsCurrent,
                link.UnlinkedAtUtc,
                link.UnlinkedArrival,
                link.UnlinkedDeparture,
                link.UnlinkedReservationStatus,
                link.UnlinkedCheckedInBusinessDate,
                link.UnlinkedNoShowBusinessDate,
                link.UnlinkedCheckedOutBusinessDate));
        await foreach (ReservationGuestLinkDataRightsExport link in links
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            await sink.WriteAsync(
                ReservationDataRightsExportSchema.CreateGuestLinkRecord(link),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        IQueryable<ReservationDetailsHistoryDataRightsExport> historyEntries =
            dbContext.ReservationDetailsHistory
                .AsNoTracking()
                .Where(history =>
                    history.PropertyId == request.PropertyId &&
                    history.ReservationId == reservation.Id)
                .OrderBy(history => history.ToRevision)
                .ThenBy(history => history.Id)
                .Select(history => new ReservationDetailsHistoryDataRightsExport(
                    history.Id,
                    history.ReservationId,
                    history.PropertyId,
                    history.FromRevision,
                    history.ToRevision,
                    history.Origin,
                    history.AdapterConnectionId,
                    history.ExternalOperationId,
                    history.OperationDeduplicationKey,
                    history.CorrelationId,
                    history.ChangedFieldsJson,
                    history.BeforeSnapshotJson,
                    history.AfterSnapshotJson,
                    history.AfterSnapshotHash,
                    history.OccurredAtUtc));
        await foreach (ReservationDetailsHistoryDataRightsExport history in historyEntries
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            await sink.WriteAsync(
                ReservationDataRightsExportSchema.CreateDetailsHistoryRecord(history),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        IQueryable<ReservationDataRightsCorrectionReceipt> correctionReceipts =
            dbContext.DataRightsCorrectionReceipts
                .AsNoTracking()
                .Where(receipt =>
                    receipt.PropertyId == request.PropertyId &&
                    receipt.ReservationId == reservation.Id)
                .OrderBy(receipt => receipt.CompletedAtUtc)
                .ThenBy(receipt => receipt.Id);
        await foreach (ReservationDataRightsCorrectionReceipt receipt in
                           correctionReceipts
                               .AsAsyncEnumerable()
                               .WithCancellation(cancellationToken)
                               .ConfigureAwait(false))
        {
            ReservationDataRightsCorrectionReceiptDataRightsExport export = new(
                receipt.ContractVersion,
                receipt.Id,
                receipt.PropertyId,
                receipt.CaseId,
                receipt.ApprovalRevision,
                receipt.ReservationId,
                receipt.SelectedRecordVersion,
                receipt.CurrentRecordVersion,
                receipt.SelectedDetailsRevision,
                receipt.CurrentDetailsRevision,
                receipt.ChangedFields,
                receipt.DetailsChangeEventId,
                receipt.EventId,
                receipt.CompletedAtUtc);
            await sink.WriteAsync(
                ReservationDataRightsExportSchema.CreateDataRightsCorrectionReceiptRecord(export),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        IQueryable<ReservationProcessingRestrictionDataRightsExport>
            processingRestrictions = dbContext.ProcessingRestrictions
                .AsNoTracking()
                .Where(restriction =>
                    restriction.PropertyId == request.PropertyId &&
                    restriction.ReservationId == reservation.Id)
                .OrderBy(restriction => restriction.AppliedAtUtc)
                .ThenBy(restriction => restriction.Id)
                .Select(restriction =>
                    new ReservationProcessingRestrictionDataRightsExport(
                        restriction.Id,
                        restriction.PropertyId,
                        restriction.ReservationId,
                        restriction.ApplyCaseId,
                        restriction.ApplyApprovalRevision,
                        restriction.ApplySelectedReservationVersion,
                        restriction.Status,
                        restriction.Version,
                        restriction.AppliedAtUtc,
                        restriction.ReleaseCaseId,
                        restriction.ReleaseApprovalRevision,
                        restriction.ReleaseSelectedReservationVersion,
                        restriction.ReleasedAtUtc));
        await foreach (
            ReservationProcessingRestrictionDataRightsExport restriction in
            processingRestrictions
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            await sink.WriteAsync(
                ReservationDataRightsExportSchema
                    .CreateProcessingRestrictionRecord(restriction),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        ReservationProcessingRestrictionProjection? restrictionState =
            await dbContext.ProcessingRestrictionProjections
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    state =>
                        state.PropertyId == request.PropertyId &&
                        state.ReservationId == reservation.Id,
                    cancellationToken)
                .ConfigureAwait(false);
        if (restrictionState is not null)
        {
            ReservationProcessingRestrictionStateDataRightsExport state = new(
                restrictionState.PropertyId,
                restrictionState.ReservationId,
                restrictionState.ContractVersion,
                restrictionState.Revision,
                restrictionState.ActiveRestrictionCount,
                restrictionState.IsRestricted,
                restrictionState.LastTransitionAtUtc);
            await sink.WriteAsync(
                ReservationDataRightsExportSchema
                    .CreateProcessingRestrictionStateRecord(state),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        IQueryable<ReservationProcessingRestrictionReceipt> restrictionReceipts =
            dbContext.ProcessingRestrictionReceipts
                .AsNoTracking()
                .Where(receipt =>
                    receipt.PropertyId == request.PropertyId &&
                    receipt.ReservationId == reservation.Id)
                .OrderBy(receipt => receipt.CompletedAtUtc)
                .ThenBy(receipt => receipt.Id);
        await foreach (
            ReservationProcessingRestrictionReceipt receipt in restrictionReceipts
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationProcessingRestrictionReceiptDataRightsExport export = new(
                receipt.Id,
                receipt.RestrictionId,
                receipt.Action,
                receipt.PropertyId,
                receipt.ReservationId,
                receipt.CaseId,
                receipt.ApprovalRevision,
                receipt.SelectedReservationVersion,
                receipt.ContractVersion,
                receipt.ResultingRestrictionVersion,
                receipt.ResultingProjectionRevision,
                receipt.EffectiveRestricted,
                receipt.EventId,
                receipt.CompletedAtUtc);
            await sink.WriteAsync(
                ReservationDataRightsExportSchema
                    .CreateProcessingRestrictionReceiptRecord(export),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        IQueryable<ReservationExternalOperationDataRightsExport> externalOperations =
            dbContext.ExternalOperations
                .AsNoTracking()
                .Where(operation =>
                    operation.PropertyId == request.PropertyId &&
                    operation.ReservationId == reservation.Id)
                .OrderBy(operation => operation.CompletedAtUtc)
                .ThenBy(operation => operation.Id)
                .Select(operation => new ReservationExternalOperationDataRightsExport(
                    operation.Id,
                    operation.ReceiptId,
                    operation.ConnectionId,
                    operation.PropertyId,
                    operation.Kind,
                    operation.RequestFingerprint,
                    operation.Outcome,
                    operation.ReservationId!.Value,
                    operation.DetailsRevision,
                    operation.ReservationVersion,
                    operation.ErrorCode,
                    operation.CompletedAtUtc));
        await foreach (ReservationExternalOperationDataRightsExport operation in externalOperations
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            await sink.WriteAsync(
                ReservationDataRightsExportSchema.CreateExternalOperationRecord(operation),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        IQueryable<ReservationArrivalReminderDataRightsExport> reminders =
            dbContext.ArrivalReminders
                .AsNoTracking()
                .Where(reminder =>
                    reminder.PropertyId == request.PropertyId &&
                    reminder.ReservationId == reservation.Id)
                .OrderBy(reminder => reminder.DueAtUtc)
                .ThenBy(reminder => reminder.Id)
                .Select(reminder => new ReservationArrivalReminderDataRightsExport(
                    reminder.Id,
                    reminder.ReservationId,
                    reminder.PropertyId,
                    reminder.DetailsRevision,
                    reminder.TimeZoneId,
                    reminder.Arrival,
                    reminder.ExpectedArrivalTime,
                    reminder.ExpectedArrivalAtUtc,
                    reminder.DueAtUtc,
                    reminder.LeadTimeMinutes,
                    reminder.State,
                    reminder.DispatchedAtUtc,
                    reminder.Version));
        await foreach (ReservationArrivalReminderDataRightsExport reminder in reminders
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            await sink.WriteAsync(
                ReservationDataRightsExportSchema.CreateArrivalReminderRecord(reminder),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        return DataRightsSubjectExportResult.Success(recordCount);
    }

    private Task<bool> IsKnownPropertyAsync(
        Guid propertyId,
        CancellationToken cancellationToken) =>
        dbContext.PropertyProjections
            .AsNoTracking()
            .AnyAsync(
                property => property.Id == propertyId && property.IsKnown,
                cancellationToken);

    private bool IsValidScope(string tenantId, Guid propertyId) =>
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        string.Equals(scopeContext.ScopeId, tenantId?.Trim(), StringComparison.Ordinal) &&
        propertyId != Guid.Empty;

    private static Guid[] ParseInventoryUnitIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                "The pending reservation amendment inventory is unavailable.");
        }

        return value.Split(',')
            .Select(item => Guid.ParseExact(item, "N"))
            .Order()
            .ToArray();
    }
}
