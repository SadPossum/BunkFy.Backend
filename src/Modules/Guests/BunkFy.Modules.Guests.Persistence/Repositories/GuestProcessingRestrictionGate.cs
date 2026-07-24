namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestProcessingRestrictionGate(
    GuestsDbContext dbContext,
    IScopeContext scopeContext)
    : IGuestProcessingRestrictionGate
{
    public async Task<GuestProcessingRestrictionGateResult> EvaluateAsync(
        GuestProcessingRestrictionGateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContractVersion != GuestProcessingRestrictionContract.CurrentVersion)
        {
            return GuestProcessingRestrictionGateResult.Unsupported(null);
        }

        if (!scopeContext.IsEnabled ||
            request.PropertyId == Guid.Empty ||
            request.GuestId == Guid.Empty ||
            !TenantIds.TryNormalize(request.TenantId, out string? tenantId) ||
            !string.Equals(scopeContext.ScopeId, tenantId, StringComparison.Ordinal))
        {
            return GuestProcessingRestrictionGateResult.Unknown;
        }

        GuestProcessingRestrictionProjection? projection =
            await dbContext.ProcessingRestrictionProjections
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.PropertyId == request.PropertyId &&
                        item.GuestId == request.GuestId,
                    cancellationToken)
                .ConfigureAwait(false);
        if (projection is null)
        {
            return GuestProcessingRestrictionGateResult.Unknown;
        }

        if (projection.ContractVersion != GuestProcessingRestrictionContract.CurrentVersion)
        {
            return GuestProcessingRestrictionGateResult.Unsupported(
                projection.ContractVersion,
                projection.Revision);
        }

        return projection.IsRestricted
            ? GuestProcessingRestrictionGateResult.Restricted(
                projection.ContractVersion,
                projection.Revision)
            : GuestProcessingRestrictionGateResult.Allowed(
                projection.ContractVersion,
                projection.Revision);
    }
}
