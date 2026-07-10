namespace Reservations.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class ReservationsInboxStore(ReservationsDbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<ReservationsDbContext>(dbContext, clock, idGenerator, ReservationsMigrations.Schema);
