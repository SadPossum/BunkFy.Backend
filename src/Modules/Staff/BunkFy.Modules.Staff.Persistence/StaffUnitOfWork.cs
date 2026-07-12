namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class StaffUnitOfWork(StaffDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<StaffDbContext>(StaffMigrations.Schema, dbContext, domainEventDispatcher)
{
}
