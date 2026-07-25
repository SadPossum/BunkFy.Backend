namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Naming;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationProcessingRestrictionProjectionRepository(
    ReservationsDbContext dbContext,
    IScopeContext scopeContext)
    : IReservationProcessingRestrictionProjectionRepository
{
    public Task<ReservationProcessingRestrictionProjection?> GetAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.ProcessingRestrictionProjections.FirstOrDefaultAsync(
            projection =>
                projection.PropertyId == propertyId &&
                projection.ReservationId == reservationId,
            cancellationToken);

    public async Task EnsureAsync(
        string tenantId,
        Guid propertyId,
        Guid reservationId,
        DateTimeOffset initializedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            !TenantIds.TryNormalize(tenantId, out string? normalizedTenantId) ||
            !string.Equals(
                scopeContext.ScopeId,
                normalizedTenantId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                ReservationsApplicationErrors.TenantRequired.Code);
        }

        bool tracked = dbContext.ProcessingRestrictionProjections.Local.Any(
            projection =>
                projection.PropertyId == propertyId &&
                projection.ReservationId == reservationId);
        if (tracked || await dbContext.ProcessingRestrictionProjections.AnyAsync(
                projection =>
                    projection.PropertyId == propertyId &&
                    projection.ReservationId == reservationId,
                cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        Result<ReservationProcessingRestrictionProjection> created =
            ReservationProcessingRestrictionProjection.Create(
                normalizedTenantId,
                propertyId,
                reservationId,
                ReservationProcessingRestrictionContract.CurrentVersion,
                initializedAtUtc);
        if (created.IsFailure)
        {
            throw new InvalidOperationException(created.Error.Code);
        }

        dbContext.ProcessingRestrictionProjections.Add(created.Value);
    }
}
