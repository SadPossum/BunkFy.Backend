namespace BunkFy.Modules.Guests.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal sealed class GuestsOutboxStore(GuestsDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<GuestsDbContext>(
        dbContext,
        options,
        GuestsMigrations.Schema)
{
    protected override IQueryable<OutboxMessage> ApplyClaimAdmission(
        IQueryable<OutboxMessage> candidates) =>
        candidates.Where(message =>
            message.ScopeId == null ||
            !this.DbContext.TenantRevisions
                .IgnoreQueryFilters()
                .Any(state =>
                    state.ScopeId == message.ScopeId &&
                    state.LifecycleStatus != GuestsTenantLifecycleStatus.Open));
}
