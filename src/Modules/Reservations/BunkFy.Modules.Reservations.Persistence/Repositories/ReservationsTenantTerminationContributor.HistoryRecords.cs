namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using Microsoft.EntityFrameworkCore;

internal sealed partial class ReservationsTenantTerminationContributor
{
    private async Task<long> ExportHistoryRecordsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        count = await this.ExportDetailsHistoryAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        count = await this.ExportExternalOperationsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        count = await this.ExportManagementOperationsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        return await this.ExportArrivalRemindersAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ExportDetailsHistoryAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationDetailsHistoryEntry history in
            dbContext.ReservationDetailsHistory
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationDetailsHistoryTenantExport record = new(
                history.ScopeId,
                history.PropertyId,
                history.ReservationId,
                history.Id,
                new ReservationDetailsHistoryStateTenantExport(
                    history.FromRevision,
                    history.ToRevision,
                    history.Origin,
                    history.OperationDeduplicationKey,
                    history.CorrelationId,
                    history.ChangedFieldsJson,
                    history.BeforeSnapshotJson,
                    history.AfterSnapshotJson,
                    history.AfterSnapshotHash,
                    history.OccurredAtUtc),
                new ReservationDetailsHistoryProviderTenantExport(
                    history.AdapterConnectionId,
                    history.ExternalOperationId),
                new ReservationDetailsHistoryStaffTenantExport(
                    history.ActorId));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .DetailsHistoryRecordType,
                history.Id,
                history.ToRevision,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportExternalOperationsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationExternalOperation operation in
            dbContext.ExternalOperations
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationExternalOperationTenantExport record = new(
                operation.ScopeId,
                operation.PropertyId,
                operation.ReservationId,
                operation.Id,
                new ReservationExternalOperationStateTenantExport(
                    operation.ReceiptId,
                    operation.ConnectionId,
                    operation.Kind,
                    operation.RequestFingerprint,
                    operation.Outcome,
                    operation.DetailsRevision,
                    operation.ReservationVersion,
                    operation.ErrorCode,
                    operation.CompletedAtUtc));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .ExternalOperationRecordType,
                operation.Id,
                recordVersion: 1,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportManagementOperationsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationManagementOperation operation in
            dbContext.ManagementOperations
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.ReservationId)
                .ThenBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationManagementOperationTenantExport record = new(
                operation.ScopeId,
                operation.PropertyId,
                operation.ReservationId,
                operation.Id,
                new ReservationManagementOperationStateTenantExport(
                    operation.Kind,
                    operation.ExpectedVersion,
                    operation.ExpectedDetailsRevision,
                    operation.BusinessDate,
                    operation.RequestFingerprint,
                    operation.CreatedAtUtc));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .ManagementOperationRecordType,
                DataRightsExportRecordIds.CreateDeterministicChild(
                    operation.ReservationId,
                    operation.Id.ToString("N")),
                recordVersion: 3,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportArrivalRemindersAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationArrivalReminder reminder in
            dbContext.ArrivalReminders
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationArrivalReminderTenantExport record = new(
                reminder.ScopeId,
                reminder.PropertyId,
                reminder.ReservationId,
                reminder.Id,
                new ReservationArrivalReminderStateTenantExport(
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
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .ArrivalReminderRecordType,
                reminder.Id,
                reminder.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }
}
