namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Options;

internal sealed class ReservationsOutboxWriter(
    ReservationsDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IEnumerable<IIntegrationEventScopeResolver> scopeResolvers)
    : EfOutboxWriter<ReservationsDbContext>(dbContext, clock, applicationIdentity, ReservationsMigrations.Schema, scopeResolvers);
