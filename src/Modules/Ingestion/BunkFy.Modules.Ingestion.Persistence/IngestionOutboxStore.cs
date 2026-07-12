namespace BunkFy.Modules.Ingestion.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class IngestionOutboxStore(IngestionDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<IngestionDbContext>(dbContext, options, IngestionMigrations.Schema);
