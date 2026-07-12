namespace BunkFy.Modules.Guests.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Microsoft.Extensions.Options;

internal sealed class GuestsOutboxStore(GuestsDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<GuestsDbContext>(dbContext, options, GuestsMigrations.Schema);
