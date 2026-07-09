namespace Properties.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class PropertiesUnitOfWork(PropertiesDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<PropertiesDbContext>(PropertiesMigrations.Schema, dbContext, domainEventDispatcher)
{
}
