namespace Reservations.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class ReservationsOutboxStore(ReservationsDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<ReservationsDbContext>(dbContext, options, ReservationsMigrations.Schema);
