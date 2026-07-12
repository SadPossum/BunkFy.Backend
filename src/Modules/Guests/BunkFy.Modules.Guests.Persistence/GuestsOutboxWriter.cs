namespace BunkFy.Modules.Guests.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Options;

internal sealed class GuestsOutboxWriter(
    GuestsDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity)
    : EfOutboxWriter<GuestsDbContext>(dbContext, clock, applicationIdentity, GuestsMigrations.Schema);
