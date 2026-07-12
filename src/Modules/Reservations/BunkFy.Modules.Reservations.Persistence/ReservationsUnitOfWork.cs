namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class ReservationsUnitOfWork(ReservationsDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<ReservationsDbContext>(ReservationsMigrations.Schema, dbContext, domainEventDispatcher)
{
}
