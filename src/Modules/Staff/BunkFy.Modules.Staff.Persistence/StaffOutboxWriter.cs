namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Options;

internal sealed class StaffOutboxWriter(
    StaffDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity)
    : EfOutboxWriter<StaffDbContext>(dbContext, clock, applicationIdentity, StaffMigrations.Schema);
