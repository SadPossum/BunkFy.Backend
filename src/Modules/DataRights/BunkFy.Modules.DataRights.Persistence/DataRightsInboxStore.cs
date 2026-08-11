namespace BunkFy.Modules.DataRights.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class DataRightsInboxStore(
    DataRightsDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator,
    IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventInboxStore<DataRightsDbContext>(
        dbContext,
        clock,
        idGenerator,
        domainEventDispatcher,
        DataRightsMigrations.Schema);
