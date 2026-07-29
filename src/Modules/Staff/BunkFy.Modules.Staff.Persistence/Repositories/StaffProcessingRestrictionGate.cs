namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffProcessingRestrictionGate(
    StaffDbContext dbContext,
    IScopeContext scopeContext)
    : IStaffProcessingRestrictionGate
{
    public async Task<StaffProcessingRestrictionGateResult> EvaluateAsync(
        StaffProcessingRestrictionGateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContractVersion !=
            StaffProcessingRestrictionContract.CurrentVersion)
        {
            return StaffProcessingRestrictionGateResult.Unsupported(null);
        }

        if (!scopeContext.IsEnabled ||
            request.StaffMemberId == Guid.Empty ||
            !TenantIds.TryNormalize(request.TenantId, out string? tenantId) ||
            !string.Equals(
                scopeContext.ScopeId,
                tenantId,
                StringComparison.Ordinal))
        {
            return StaffProcessingRestrictionGateResult.Unknown;
        }

        StaffProcessingRestrictionProjection? projection =
            await dbContext.ProcessingRestrictionProjections
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item =>
                        item.StaffMemberId == request.StaffMemberId,
                    cancellationToken)
                .ConfigureAwait(false);
        if (projection is null)
        {
            return StaffProcessingRestrictionGateResult.Unknown;
        }

        if (projection.ContractVersion !=
            StaffProcessingRestrictionContract.CurrentVersion)
        {
            return StaffProcessingRestrictionGateResult.Unsupported(
                projection.ContractVersion,
                projection.Revision);
        }

        return projection.IsRestricted
            ? StaffProcessingRestrictionGateResult.Restricted(
                projection.ContractVersion,
                projection.Revision)
            : StaffProcessingRestrictionGateResult.Allowed(
                projection.ContractVersion,
                projection.Revision);
    }
}
