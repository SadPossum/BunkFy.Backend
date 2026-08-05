namespace BunkFy.Modules.Properties.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;

internal sealed class PropertiesOutboxStore(PropertiesDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<PropertiesDbContext>(
        dbContext,
        options,
        PropertiesMigrations.Schema)
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
                        PropertiesTenantLifecycleStatus.Open));
}
