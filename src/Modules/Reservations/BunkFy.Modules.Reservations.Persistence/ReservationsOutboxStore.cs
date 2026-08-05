namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal sealed class ReservationsOutboxStore(ReservationsDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<ReservationsDbContext>(
        dbContext,
        options,
        ReservationsMigrations.Schema)
{
    protected override IQueryable<OutboxMessage> ApplyClaimAdmission(
        IQueryable<OutboxMessage> candidates) =>
        candidates.Where(message =>
            message.ScopeId == null ||
            !this.DbContext.TenantRevisions
                .IgnoreQueryFilters()
                .Any(state =>
                    state.ScopeId == message.ScopeId &&
                    state.LifecycleStatus !=
                        ReservationsTenantLifecycleStatus.Open));
}
