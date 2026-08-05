namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed partial class GuestsTenantTerminationContributor
{
    private async Task<long> ExportGovernanceRecordsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        count = await this.ExportCorrectionReceiptsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportRestrictionsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportRestrictionReceiptsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportDataHoldsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportDataHoldReceiptsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportAnonymisationReceiptsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportAnonymisationTombstonesAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        return await this.ExportAnonymisationRestoreReceiptsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ExportCorrectionReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (GuestDataRightsCorrectionReceipt receipt in
            dbContext.DataRightsCorrectionReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            GuestDataRightsCorrectionReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.GuestId,
                receipt.Id,
                new GuestDataRightsCorrectionProofTenantExport(
                    receipt.ContractVersion,
                    receipt.IdempotencyKey,
                    receipt.CaseId,
                    receipt.ApprovalRevision,
                    receipt.SelectedRecordVersion,
                    receipt.CurrentRecordVersion,
                    receipt.ChangedFieldsMask,
                    receipt.EventId,
                    receipt.CompletionEventId,
                    receipt.CompletedAtUtc));
            await WriteAsync(
                GuestsTenantTerminationMetadata
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

    private async Task<long> ExportRestrictionsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (GuestProcessingRestriction restriction in
            dbContext.ProcessingRestrictions
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            GuestProcessingRestrictionTenantExport record = new(
                restriction.ScopeId,
                restriction.PropertyId,
                restriction.GuestId,
                restriction.Id,
                new GuestProcessingRestrictionStateTenantExport(
                    restriction.ApplyCaseId,
                    restriction.ApplyApprovalRevision,
                    restriction.ApplySelectedGuestVersion,
                    restriction.Status,
                    restriction.Version,
                    restriction.AppliedAtUtc,
                    restriction.ReleaseCaseId,
                    restriction.ReleaseApprovalRevision,
                    restriction.ReleaseSelectedGuestVersion,
                    restriction.ReleasedAtUtc),
                new GuestRestrictionStaffTenantExport(
                    restriction.AppliedBy,
                    restriction.ReleasedBy));
            await WriteAsync(
                GuestsTenantTerminationMetadata
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

    private async Task<long> ExportRestrictionReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (GuestProcessingRestrictionReceipt receipt in
            dbContext.ProcessingRestrictionReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            GuestProcessingRestrictionReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.GuestId,
                receipt.Id,
                new GuestProcessingRestrictionReceiptStateTenantExport(
                    receipt.IdempotencyKey,
                    receipt.RestrictionId,
                    receipt.Action,
                    receipt.CaseId,
                    receipt.ApprovalRevision,
                    receipt.SelectedGuestVersion,
                    receipt.ResultingRestrictionVersion,
                    receipt.ResultingProjectionRevision,
                    receipt.EffectiveRestricted,
                    receipt.EventId,
                    receipt.CompletedAtUtc),
                new GuestActorStaffTenantExport(receipt.ActorId));
            await WriteAsync(
                GuestsTenantTerminationMetadata
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
        await foreach (GuestDataHold hold in dbContext.DataHolds
            .AsNoTracking()
            .Where(item => item.ScopeId == tenantId)
            .OrderBy(item => item.Id)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            GuestDataHoldTenantExport record = new(
                hold.ScopeId,
                hold.PropertyId,
                hold.GuestId,
                hold.Id,
                new GuestDataHoldStateTenantExport(
                    hold.ReasonCode,
                    hold.State,
                    hold.PlacedAtUtc,
                    hold.ReleasedAtUtc,
                    hold.Version),
                new GuestDataHoldStaffTenantExport(
                    hold.PlacedBy,
                    hold.ReleasedBy));
            await WriteAsync(
                GuestsTenantTerminationMetadata.DataHoldRecordType,
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
        await foreach (GuestDataHoldReceipt receipt in
            dbContext.DataHoldReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            GuestDataHoldReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.GuestId,
                receipt.Id,
                new GuestDataHoldReceiptStateTenantExport(
                    receipt.IdempotencyKey,
                    receipt.HoldId,
                    receipt.Action,
                    receipt.ReasonCode,
                    receipt.SelectedGuestVersion,
                    receipt.ResultingHoldVersion,
                    receipt.CompletedAtUtc),
                new GuestActorStaffTenantExport(receipt.ActorId));
            await WriteAsync(
                GuestsTenantTerminationMetadata.DataHoldReceiptRecordType,
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
        await foreach (GuestAnonymisationReceipt receipt in
            dbContext.AnonymisationReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            GuestAnonymisationReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.RoutingPropertyId,
                receipt.GuestId,
                receipt.Id,
                new GuestAnonymisationReceiptProofTenantExport(
                    receipt.ContractVersion,
                    receipt.IdempotencyKey,
                    receipt.CaseId,
                    receipt.ApprovalRevision,
                    receipt.OperationRevision,
                    receipt.SelectedGuestVersion,
                    receipt.ResultingGuestVersion,
                    receipt.Disposition,
                    receipt.Reason,
                    receipt.AffectedPropertyCount,
                    receipt.ApprovalEvidenceSha256,
                    receipt.PolicySetSha256,
                    receipt.EventId,
                    receipt.CompletedAtUtc,
                    receipt.CanonicalSha256),
                new GuestActorStaffTenantExport(receipt.ActorId));
            await WriteAsync(
                GuestsTenantTerminationMetadata
                    .AnonymisationReceiptRecordType,
                receipt.Id,
                receipt.ResultingGuestVersion,
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
        await foreach (GuestAnonymisationTombstone tombstone in
            dbContext.AnonymisationTombstones
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            GuestAnonymisationTombstoneTenantExport record = new(
                tombstone.ScopeId,
                tombstone.Id,
                new GuestAnonymisationTombstoneProofTenantExport(
                    tombstone.ContractVersion,
                    tombstone.Revision,
                    tombstone.State,
                    tombstone.Authority,
                    tombstone.CompletedAtUtc,
                    tombstone.LedgerEntryId,
                    tombstone.OwnerReceiptSha256,
                    tombstone.LastReplayedAtUtc));
            await WriteAsync(
                GuestsTenantTerminationMetadata
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
        await foreach (GuestAnonymisationRestoreReceipt receipt in
            dbContext.AnonymisationRestoreReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            GuestAnonymisationRestoreReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.GuestId,
                receipt.Id,
                new GuestAnonymisationRestoreProofTenantExport(
                    receipt.ContractVersion,
                    receipt.LedgerEntryId,
                    receipt.OwnerReceiptContractVersion,
                    receipt.OwnerReceiptId,
                    receipt.OwnerReceiptSha256,
                    receipt.ResultingGuestVersion,
                    receipt.TombstoneRevision,
                    receipt.ReplayedAtUtc,
                    receipt.CanonicalSha256));
            await WriteAsync(
                GuestsTenantTerminationMetadata
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
