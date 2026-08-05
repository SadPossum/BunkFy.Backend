namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Retention;
using Microsoft.EntityFrameworkCore;

internal sealed partial class StaffTenantTerminationContributor
{
    private async Task<long> ExportRetentionRecordsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        count = await this.ExportRetentionExecutionsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        return await this.ExportRetentionReceiptsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ExportRetentionExecutionsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (StaffRetentionExecution execution in
            dbContext.RetentionExecutions
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffRetentionExecutionTenantExport record = new(
                execution.ScopeId,
                execution.Id,
                new StaffRetentionExecutionStateTenantExport(
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
                StaffTenantTerminationMetadata.RetentionExecutionRecordType,
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
        await foreach (StaffRetentionAnonymisationReceipt receipt in
            dbContext.RetentionAnonymisationReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffRetentionAnonymisationReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.StaffMemberId,
                receipt.Id,
                new StaffRetentionAnonymisationProofTenantExport(
                    receipt.ContractVersion,
                    receipt.ExecutionId,
                    receipt.SelectedStaffVersion,
                    receipt.ResultingStaffVersion,
                    receipt.SelectedOperationLockRevision,
                    receipt.ResultingOperationLockRevision,
                    receipt.DepartedAtUtc,
                    receipt.RetentionDeadlineUtc,
                    receipt.PolicyEvidenceSha256,
                    receipt.EventId,
                    receipt.CompletedAtUtc,
                    receipt.CanonicalSha256),
                new StaffActorAttributionTenantExport(receipt.ActorId));
            await WriteAsync(
                StaffTenantTerminationMetadata
                    .RetentionAnonymisationReceiptRecordType,
                receipt.Id,
                receipt.ResultingStaffVersion,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }
}
