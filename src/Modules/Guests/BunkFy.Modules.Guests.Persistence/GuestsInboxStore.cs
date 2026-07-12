namespace BunkFy.Modules.Guests.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class GuestsInboxStore(GuestsDbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<GuestsDbContext>(dbContext, clock, idGenerator, GuestsMigrations.Schema);
