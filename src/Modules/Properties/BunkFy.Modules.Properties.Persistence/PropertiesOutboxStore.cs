namespace BunkFy.Modules.Properties.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class PropertiesOutboxStore(PropertiesDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<PropertiesDbContext>(dbContext, options, PropertiesMigrations.Schema);
