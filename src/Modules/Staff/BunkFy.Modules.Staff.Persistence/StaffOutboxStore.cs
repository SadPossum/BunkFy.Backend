namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal sealed class StaffOutboxStore(StaffDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<StaffDbContext>(
        dbContext,
        options,
        StaffMigrations.Schema)
{
    protected override IQueryable<OutboxMessage> ApplyClaimAdmission(
        IQueryable<OutboxMessage> candidates) =>
        candidates.Where(message =>
            message.ScopeId == null ||
            !this.DbContext.TenantRevisions
                .IgnoreQueryFilters()
                .Any(state =>
                    state.ScopeId == message.ScopeId &&
                    state.LifecycleStatus != StaffTenantLifecycleStatus.Open));
}
