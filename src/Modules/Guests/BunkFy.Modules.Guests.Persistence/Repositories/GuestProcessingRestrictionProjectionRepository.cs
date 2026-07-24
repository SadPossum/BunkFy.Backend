namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;
using Gma.Framework.Naming;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestProcessingRestrictionProjectionRepository(
    GuestsDbContext dbContext,
    IScopeContext scopeContext)
    : IGuestProcessingRestrictionProjectionRepository
{
    public Task<GuestProcessingRestrictionProjection?> GetAsync(
        Guid propertyId,
        Guid guestId,
        CancellationToken cancellationToken) =>
        dbContext.ProcessingRestrictionProjections.FirstOrDefaultAsync(
            projection =>
                projection.PropertyId == propertyId &&
                projection.GuestId == guestId,
            cancellationToken);

    public async Task EnsureAsync(
        string tenantId,
        Guid propertyId,
        Guid guestId,
        DateTimeOffset initializedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            !TenantIds.TryNormalize(tenantId, out string? normalizedTenantId) ||
            !string.Equals(scopeContext.ScopeId, normalizedTenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(GuestsApplicationErrors.TenantRequired.Code);
        }

        bool tracked = dbContext.ProcessingRestrictionProjections.Local.Any(projection =>
            projection.PropertyId == propertyId && projection.GuestId == guestId);
        if (tracked || await dbContext.ProcessingRestrictionProjections.AnyAsync(
                projection => projection.PropertyId == propertyId && projection.GuestId == guestId,
                cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        Result<GuestProcessingRestrictionProjection> created =
            GuestProcessingRestrictionProjection.Create(
                normalizedTenantId,
                propertyId,
                guestId,
                GuestProcessingRestrictionContract.CurrentVersion,
                initializedAtUtc);
        if (created.IsFailure)
        {
            throw new InvalidOperationException(created.Error.Code);
        }

        dbContext.ProcessingRestrictionProjections.Add(created.Value);
    }
}
