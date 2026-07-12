namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Microsoft.Extensions.Options;

internal sealed class ReservationsOutboxStore(ReservationsDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<ReservationsDbContext>(dbContext, options, ReservationsMigrations.Schema);
