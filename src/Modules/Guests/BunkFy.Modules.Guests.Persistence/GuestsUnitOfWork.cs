namespace BunkFy.Modules.Guests.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class GuestsUnitOfWork(GuestsDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<GuestsDbContext>(GuestsMigrations.Schema, dbContext, domainEventDispatcher)
{
}
