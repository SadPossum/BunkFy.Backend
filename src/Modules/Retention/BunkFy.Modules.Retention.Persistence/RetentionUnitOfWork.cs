namespace BunkFy.Modules.Retention.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class RetentionUnitOfWork(RetentionDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<RetentionDbContext>(RetentionMigrations.Schema, dbContext, domainEventDispatcher)
{
}
