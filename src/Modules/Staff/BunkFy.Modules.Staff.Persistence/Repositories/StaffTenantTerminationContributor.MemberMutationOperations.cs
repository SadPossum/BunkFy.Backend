namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Microsoft.EntityFrameworkCore;

internal sealed partial class StaffTenantTerminationContributor
{
    private async Task<long> ExportMemberMutationOperationsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (StaffMemberMutationOperation operation in
            dbContext.MemberMutationOperations
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.StaffMemberId)
                .ThenBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            StaffMemberMutationOperationTenantExport record = new(
                operation.ScopeId,
                operation.StaffMemberId,
                operation.Id,
                new StaffMemberMutationOperationStateTenantExport(
                    operation.Kind,
                    operation.ExpectedVersion,
                    operation.RequestFingerprint,
                    operation.ResultStatus,
                    operation.ResultVersion,
                    operation.CompletedAtUtc));
            await sink.WriteAsync(
                StaffTenantTerminationExportSchema.CreateRecord(
                    StaffTenantTerminationMetadata
                        .MemberMutationOperationRecordType,
                    DataRightsExportRecordIds.CreateDeterministicChild(
                        operation.StaffMemberId,
                        operation.Id.ToString("N")),
                    operation.ResultVersion,
                    record),
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }
}
