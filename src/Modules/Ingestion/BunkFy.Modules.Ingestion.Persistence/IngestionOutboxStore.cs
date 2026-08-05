namespace BunkFy.Modules.Ingestion.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionOutboxStore(IngestionDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<IngestionDbContext>(
        dbContext,
        options,
        IngestionMigrations.Schema)
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
                        IngestionTenantLifecycleStatus.Open));
}
