namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class IngestionInboxStore(IngestionDbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<IngestionDbContext>(dbContext, clock, idGenerator, IngestionMigrations.Schema);
