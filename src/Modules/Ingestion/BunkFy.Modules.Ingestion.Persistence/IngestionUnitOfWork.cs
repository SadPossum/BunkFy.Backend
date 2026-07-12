namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class IngestionUnitOfWork(IngestionDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<IngestionDbContext>(IngestionMigrations.Schema, dbContext, domainEventDispatcher)
{
}
