namespace BunkFy.Modules.DataRights.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class DataRightsUnitOfWork(DataRightsDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<DataRightsDbContext>(DataRightsMigrations.Schema, dbContext, domainEventDispatcher)
{
}
