namespace BunkFy.Modules.DataRights.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class DataRightsInboxStore(DataRightsDbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<DataRightsDbContext>(dbContext, clock, idGenerator, DataRightsMigrations.Schema);
