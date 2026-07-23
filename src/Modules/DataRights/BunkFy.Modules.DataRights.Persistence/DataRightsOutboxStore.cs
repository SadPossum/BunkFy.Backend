namespace BunkFy.Modules.DataRights.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class DataRightsOutboxStore(DataRightsDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<DataRightsDbContext>(dbContext, options, DataRightsMigrations.Schema);
