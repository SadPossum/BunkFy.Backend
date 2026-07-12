namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class StaffInboxStore(StaffDbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<StaffDbContext>(dbContext, clock, idGenerator, StaffMigrations.Schema);
