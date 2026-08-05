namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed partial class ReservationsTenantTerminationContributor
{
    private async Task<long> ExportGovernanceRecordsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        count = await this.ExportCorrectionReceiptsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        count = await this.ExportProcessingRestrictionsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        count = await this.ExportProcessingRestrictionReceiptsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        count = await this.ExportDataHoldsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        count = await this.ExportDataHoldReceiptsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        count = await this.ExportAnonymisationReceiptsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        count = await this.ExportAnonymisationTombstonesAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
        return await this.ExportAnonymisationRestoreReceiptsAsync(
            tenantId,
            sink,
            count,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ExportCorrectionReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationDataRightsCorrectionReceipt receipt in
            dbContext.DataRightsCorrectionReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationDataRightsCorrectionReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.ReservationId,
                receipt.Id,
                new ReservationDataRightsCorrectionProofTenantExport(
                    receipt.ContractVersion,
                    receipt.IdempotencyKey,
                    receipt.CaseId,
                    receipt.ApprovalRevision,
                    receipt.SelectedRecordVersion,
                    receipt.CurrentRecordVersion,
                    receipt.SelectedDetailsRevision,
                    receipt.CurrentDetailsRevision,
                    receipt.ChangedFieldsMask,
                    receipt.DetailsChangeEventId,
                    receipt.CorrelationId,
                    receipt.EventId,
                    receipt.CompletionEventId,
                    receipt.CompletedAtUtc));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .DataRightsCorrectionReceiptRecordType,
                receipt.Id,
                receipt.CurrentRecordVersion,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportProcessingRestrictionsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationProcessingRestriction restriction in
            dbContext.ProcessingRestrictions
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationProcessingRestrictionTenantExport record = new(
                restriction.ScopeId,
                restriction.PropertyId,
                restriction.ReservationId,
                restriction.Id,
                new ReservationProcessingRestrictionStateTenantExport(
                    restriction.ApplyCaseId,
                    restriction.ApplyApprovalRevision,
                    restriction.ApplySelectedReservationVersion,
                    restriction.Status,
                    restriction.Version,
                    restriction.AppliedAtUtc,
                    restriction.ReleaseCaseId,
                    restriction.ReleaseApprovalRevision,
                    restriction.ReleaseSelectedReservationVersion,
                    restriction.ReleasedAtUtc),
                new ReservationProcessingRestrictionStaffTenantExport(
                    restriction.AppliedBy,
                    restriction.ReleasedBy));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .ProcessingRestrictionRecordType,
                restriction.Id,
                restriction.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportProcessingRestrictionReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationProcessingRestrictionReceipt receipt in
            dbContext.ProcessingRestrictionReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationProcessingRestrictionReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.ReservationId,
                receipt.Id,
                new ReservationProcessingRestrictionReceiptStateTenantExport(
                    receipt.IdempotencyKey,
                    receipt.RestrictionId,
                    receipt.Action,
                    receipt.CaseId,
                    receipt.ApprovalRevision,
                    receipt.SelectedReservationVersion,
                    receipt.ContractVersion,
                    receipt.ResultingRestrictionVersion,
                    receipt.ResultingProjectionRevision,
                    receipt.EffectiveRestricted,
                    receipt.EventId,
                    receipt.CompletedAtUtc));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .ProcessingRestrictionReceiptRecordType,
                receipt.Id,
                receipt.ResultingRestrictionVersion,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportDataHoldsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationDataHold hold in dbContext.DataHolds
            .AsNoTracking()
            .Where(item => item.ScopeId == tenantId)
            .OrderBy(item => item.Id)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            ReservationDataHoldTenantExport record = new(
                hold.ScopeId,
                hold.PropertyId,
                hold.ReservationId,
                hold.Id,
                new ReservationDataHoldStateTenantExport(
                    hold.ReasonCode,
                    hold.State,
                    hold.PlacedAtUtc,
                    hold.ReleasedAtUtc,
                    hold.Version),
                new ReservationDataHoldStaffTenantExport(
                    hold.PlacedBy,
                    hold.ReleasedBy));
            await WriteAsync(
                ReservationsTenantTerminationMetadata.DataHoldRecordType,
                hold.Id,
                hold.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportDataHoldReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationDataHoldReceipt receipt in
            dbContext.DataHoldReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationDataHoldReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.ReservationId,
                receipt.Id,
                new ReservationDataHoldReceiptStateTenantExport(
                    receipt.IdempotencyKey,
                    receipt.HoldId,
                    receipt.Action,
                    receipt.ReasonCode,
                    receipt.SelectedReservationVersion,
                    receipt.SelectedDetailsRevision,
                    receipt.ResultingHoldVersion,
                    receipt.CompletedAtUtc));
            await WriteAsync(
                ReservationsTenantTerminationMetadata.DataHoldReceiptRecordType,
                receipt.Id,
                receipt.ResultingHoldVersion,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportAnonymisationReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationAnonymisationReceipt receipt in
            dbContext.AnonymisationReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationAnonymisationReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.ReservationId,
                receipt.Id,
                new ReservationAnonymisationReceiptProofTenantExport(
                    receipt.ContractVersion,
                    receipt.IdempotencyKey,
                    receipt.CaseId,
                    receipt.ApprovalRevision,
                    receipt.OperationRevision,
                    receipt.SelectedReservationVersion,
                    receipt.ResultingReservationVersion,
                    receipt.SelectedDetailsRevision,
                    receipt.ResultingDetailsRevision,
                    receipt.Disposition,
                    receipt.Reason,
                    receipt.RedactedHistoryCount,
                    receipt.RemovedGuestLinkCount,
                    receipt.ReducedExternalOperationCount,
                    receipt.SuppressedReminderCount,
                    receipt.ApprovalEvidenceSha256,
                    receipt.PolicyEvidenceSha256,
                    receipt.EventId,
                    receipt.CompletedAtUtc,
                    receipt.CanonicalSha256),
                new ReservationAnonymisationStaffTenantExport(
                    receipt.ActorId));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .AnonymisationReceiptRecordType,
                receipt.Id,
                receipt.ResultingReservationVersion,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportAnonymisationTombstonesAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationAnonymisationTombstone tombstone in
            dbContext.AnonymisationTombstones
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationAnonymisationTombstoneTenantExport record = new(
                tombstone.ScopeId,
                tombstone.PropertyId,
                tombstone.Id,
                new ReservationAnonymisationTombstoneProofTenantExport(
                    tombstone.ContractVersion,
                    tombstone.Revision,
                    tombstone.Authority,
                    tombstone.OwnerReceiptContractVersion,
                    tombstone.OwnerReceiptId,
                    tombstone.OwnerReceiptSha256,
                    tombstone.ResultingReservationVersion,
                    tombstone.ResultingDetailsRevision,
                    tombstone.CompletedAtUtc,
                    tombstone.LedgerEntryId,
                    tombstone.LastReplayedAtUtc));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .AnonymisationTombstoneRecordType,
                tombstone.Id,
                tombstone.Revision,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportAnonymisationRestoreReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationAnonymisationRestoreReceipt receipt in
            dbContext.AnonymisationRestoreReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            ReservationAnonymisationRestoreReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.ReservationId,
                receipt.Id,
                new ReservationAnonymisationRestoreProofTenantExport(
                    receipt.ContractVersion,
                    receipt.LedgerEntryId,
                    receipt.TenantSequence,
                    receipt.LedgerEntrySha256,
                    receipt.OwnerReceiptContractVersion,
                    receipt.OwnerReceiptId,
                    receipt.OwnerReceiptSha256,
                    receipt.ResultingReservationVersion,
                    receipt.ResultingDetailsRevision,
                    receipt.OriginallyCompletedAtUtc,
                    receipt.TombstoneRevision,
                    receipt.ReplayedAtUtc,
                    receipt.CanonicalSha256));
            await WriteAsync(
                ReservationsTenantTerminationMetadata
                    .AnonymisationRestoreReceiptRecordType,
                receipt.Id,
                receipt.TombstoneRevision,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }
}
