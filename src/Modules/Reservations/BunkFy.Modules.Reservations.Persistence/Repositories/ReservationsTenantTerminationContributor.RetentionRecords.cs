namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Retention;
using Microsoft.EntityFrameworkCore;

internal sealed partial class ReservationsTenantTerminationContributor
{
    private async Task<long> ExportRetentionRecordsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        count = await this.ExportRetentionExecutionsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        return await this.ExportRetentionReceiptsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ExportRetentionExecutionsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationRetentionExecution execution in
            dbContext.RetentionExecutions
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationRetentionExecutionTenantExport record = new(
                execution.ScopeId,
                execution.Id,
                new ReservationRetentionExecutionStateTenantExport(
                    execution.DataClassKey,
                    execution.ExecutionPolicyVersion,
                    execution.Attempt,
                    execution.StartingProjectionOrdinal,
                    execution.State,
                    execution.StartedAtUtc,
                    execution.DeadlineUtc,
                    execution.CompletedAtUtc,
                    execution.AffectedCount,
                    execution.ScannedCount,
                    execution.RemainingCount,
                    execution.OutcomeCode,
                    execution.HoldReviewDueAtUtc,
                    execution.Version));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .RetentionExecutionRecordType,
                execution.Id,
                execution.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportRetentionReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationRetentionAnonymisationReceipt receipt in
            dbContext.RetentionAnonymisationReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationRetentionAnonymisationReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.ReservationId,
                receipt.Id,
                new ReservationRetentionAnonymisationProofTenantExport(
                    receipt.ContractVersion,
                    receipt.ExecutionId,
                    receipt.SelectedReservationVersion,
                    receipt.ResultingReservationVersion,
                    receipt.SelectedDetailsRevision,
                    receipt.ResultingDetailsRevision,
                    receipt.TerminalAtUtc,
                    receipt.RetentionDeadlineUtc,
                    receipt.PolicyEvidenceSha256,
                    receipt.RedactedHistoryCount,
                    receipt.RemovedGuestLinkCount,
                    receipt.ReducedExternalOperationCount,
                    receipt.SuppressedReminderCount,
                    receipt.EventId,
                    receipt.CompletedAtUtc,
                    receipt.CanonicalSha256),
                new ReservationRetentionStaffTenantExport(
                    receipt.ActorId));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .RetentionAnonymisationReceiptRecordType,
                receipt.Id,
                receipt.ResultingReservationVersion,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }
}
