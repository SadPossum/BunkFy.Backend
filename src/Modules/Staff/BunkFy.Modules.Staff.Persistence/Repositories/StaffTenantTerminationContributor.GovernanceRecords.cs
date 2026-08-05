namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Governance;
using Microsoft.EntityFrameworkCore;

internal sealed partial class StaffTenantTerminationContributor
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
        count = await this.ExportEmploymentGovernanceAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportEmploymentGovernanceReceiptsAsync(
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
        await foreach (StaffDataRightsCorrectionReceipt receipt in
            dbContext.DataRightsCorrectionReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffDataRightsCorrectionReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.StaffMemberId,
                receipt.Id,
                new StaffDataRightsCorrectionProofTenantExport(
                    receipt.ContractVersion,
                    receipt.ExecutionId,
                    receipt.CaseId,
                    receipt.ApprovalRevision,
                    receipt.SelectedRecordVersion,
                    receipt.CurrentRecordVersion,
                    receipt.ChangedFieldsMask,
                    receipt.RequestSha256,
                    receipt.ProfileEventId,
                    receipt.CompletionEventId,
                    receipt.CompletedAtUtc));
            await WriteAsync(
                StaffTenantTerminationMetadata
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
        await foreach (StaffProcessingRestriction restriction in
            dbContext.ProcessingRestrictions
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffProcessingRestrictionTenantExport record = new(
                restriction.ScopeId,
                restriction.StaffMemberId,
                restriction.Id,
                new StaffProcessingRestrictionStateTenantExport(
                    restriction.ApplyCaseId,
                    restriction.ApplyApprovalRevision,
                    restriction.ApplySelectedStaffVersion,
                    restriction.Status,
                    restriction.Version,
                    restriction.AppliedAtUtc,
                    restriction.ReleaseCaseId,
                    restriction.ReleaseApprovalRevision,
                    restriction.ReleaseSelectedStaffVersion,
                    restriction.ReleasedAtUtc),
                new StaffLifecycleAttributionTenantExport(
                    restriction.AppliedBy,
                    restriction.ReleasedBy));
            await WriteAsync(
                StaffTenantTerminationMetadata
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
        await foreach (StaffProcessingRestrictionReceipt receipt in
            dbContext.ProcessingRestrictionReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffProcessingRestrictionReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.StaffMemberId,
                receipt.Id,
                new StaffProcessingRestrictionReceiptStateTenantExport(
                    receipt.IdempotencyKey,
                    receipt.RestrictionId,
                    receipt.Action,
                    receipt.CaseId,
                    receipt.ApprovalRevision,
                    receipt.SelectedStaffVersion,
                    receipt.ResultingRestrictionVersion,
                    receipt.ResultingProjectionRevision,
                    receipt.EffectiveRestricted,
                    receipt.EventId,
                    receipt.CompletedAtUtc),
                new StaffActorAttributionTenantExport(receipt.ActorId));
            await WriteAsync(
                StaffTenantTerminationMetadata
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

    private async Task<long> ExportEmploymentGovernanceAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (StaffEmploymentGovernance governance in
            dbContext.EmploymentGovernance
                .AsNoTracking()
                .Include(item => item.AcceptedAcknowledgements)
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsSplitQuery()
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffEmploymentGovernanceTenantExport record = new(
                governance.ScopeId,
                governance.StaffMemberId,
                new StaffEmploymentGovernanceStateTenantExport(
                    governance.GovernanceContractVersion,
                    governance.SelectedStaffVersion,
                    governance.Binding.OperatingCountryCode,
                    governance.Binding.PolicyId,
                    governance.Binding.PolicyVersion,
                    governance.Binding.DataRegionId,
                    governance.Binding.TransferProfileId,
                    governance.Binding.RetentionPolicyId,
                    governance.Binding.RetentionPolicyVersion,
                    governance.Binding.ContentSha256,
                    governance.Binding.PolicyEffectiveAtUtc,
                    governance.Binding.PolicyExpiresAtUtc,
                    governance.Binding.EvaluatedAtUtc,
                    governance.AcceptedAcknowledgements
                        .OrderBy(
                            item => item.AcknowledgementId,
                            StringComparer.Ordinal)
                        .ThenBy(item => item.AcknowledgementVersion)
                        .Select(item =>
                            new StaffGovernanceAcknowledgementTenantExport(
                                item.AcknowledgementId,
                                item.AcknowledgementVersion))
                        .ToArray(),
                    governance.ConfiguredAtUtc,
                    governance.Version),
                new StaffActorAttributionTenantExport(
                    governance.ConfiguredBy));
            await WriteAsync(
                StaffTenantTerminationMetadata
                    .EmploymentGovernanceRecordType,
                governance.Id,
                governance.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportEmploymentGovernanceReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (StaffEmploymentGovernanceChangeReceipt receipt in
            dbContext.EmploymentGovernanceChangeReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffEmploymentGovernanceChangeReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.StaffMemberId,
                receipt.Id,
                new StaffEmploymentGovernanceChangeProofTenantExport(
                    receipt.IdempotencyKey,
                    receipt.GovernanceContractVersion,
                    receipt.SelectedStaffVersion,
                    receipt.PreviousGovernanceVersion,
                    receipt.ResultingGovernanceVersion,
                    receipt.PolicyContentSha256,
                    receipt.AcknowledgementsSha256,
                    receipt.RequestSha256,
                    receipt.ReceiptSha256,
                    receipt.CompletedAtUtc),
                new StaffActorAttributionTenantExport(receipt.ActorId));
            await WriteAsync(
                StaffTenantTerminationMetadata
                    .EmploymentGovernanceChangeReceiptRecordType,
                receipt.Id,
                receipt.ResultingGovernanceVersion,
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
        await foreach (StaffDataHold hold in dbContext.DataHolds
            .AsNoTracking()
            .Where(item => item.ScopeId == tenantId)
            .OrderBy(item => item.Id)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            StaffDataHoldTenantExport record = new(
                hold.ScopeId,
                hold.StaffMemberId,
                hold.Id,
                new StaffDataHoldStateTenantExport(
                    hold.ReasonCode,
                    hold.State,
                    hold.PlacedAtUtc,
                    hold.ReleasedAtUtc,
                    hold.Version),
                new StaffLifecycleAttributionTenantExport(
                    hold.PlacedBy,
                    hold.ReleasedBy));
            await WriteAsync(
                StaffTenantTerminationMetadata.DataHoldRecordType,
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
        await foreach (StaffDataHoldReceipt receipt in
            dbContext.DataHoldReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffDataHoldReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.StaffMemberId,
                receipt.Id,
                new StaffDataHoldReceiptStateTenantExport(
                    receipt.IdempotencyKey,
                    receipt.HoldId,
                    receipt.Action,
                    receipt.ReasonCode,
                    receipt.SelectedStaffVersion,
                    receipt.ResultingHoldVersion,
                    receipt.CompletedAtUtc),
                new StaffActorAttributionTenantExport(receipt.ActorId));
            await WriteAsync(
                StaffTenantTerminationMetadata.DataHoldReceiptRecordType,
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
        await foreach (StaffAnonymisationReceipt receipt in
            dbContext.AnonymisationReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffAnonymisationReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.StaffMemberId,
                receipt.Id,
                new StaffAnonymisationReceiptProofTenantExport(
                    receipt.ContractVersion,
                    receipt.IdempotencyKey,
                    receipt.CaseId,
                    receipt.ApprovalRevision,
                    receipt.OperationRevision,
                    receipt.SelectedStaffVersion,
                    receipt.ResultingStaffVersion,
                    receipt.SelectedOperationLockRevision,
                    receipt.ResultingOperationLockRevision,
                    receipt.Disposition,
                    receipt.Reason,
                    receipt.ApprovalEvidenceSha256,
                    receipt.StateBindingsSha256,
                    receipt.EventId,
                    receipt.CompletedAtUtc,
                    receipt.CanonicalSha256),
                new StaffActorAttributionTenantExport(receipt.ActorId));
            await WriteAsync(
                StaffTenantTerminationMetadata
                    .AnonymisationReceiptRecordType,
                receipt.Id,
                receipt.ResultingStaffVersion,
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
        await foreach (StaffAnonymisationTombstone tombstone in
            dbContext.AnonymisationTombstones
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffAnonymisationTombstoneTenantExport record = new(
                tombstone.ScopeId,
                tombstone.Id,
                new StaffAnonymisationTombstoneProofTenantExport(
                    tombstone.ContractVersion,
                    tombstone.Revision,
                    tombstone.State,
                    tombstone.Authority,
                    tombstone.CompletedAtUtc,
                    tombstone.LedgerEntryId,
                    tombstone.OwnerReceiptSha256,
                    tombstone.LastReplayedAtUtc));
            await WriteAsync(
                StaffTenantTerminationMetadata
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
        await foreach (StaffAnonymisationRestoreReceipt receipt in
            dbContext.AnonymisationRestoreReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffAnonymisationRestoreReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.StaffMemberId,
                receipt.Id,
                new StaffAnonymisationRestoreProofTenantExport(
                    receipt.ContractVersion,
                    receipt.LedgerEntryId,
                    receipt.OwnerReceiptContractVersion,
                    receipt.OwnerReceiptId,
                    receipt.OwnerReceiptSha256,
                    receipt.ResultingStaffVersion,
                    receipt.TombstoneRevision,
                    receipt.ReplayedAtUtc,
                    receipt.CanonicalSha256));
            await WriteAsync(
                StaffTenantTerminationMetadata
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
