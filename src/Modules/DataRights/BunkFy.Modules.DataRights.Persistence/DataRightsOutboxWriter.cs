namespace BunkFy.Modules.DataRights.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;

internal sealed class DataRightsOutboxWriter(
    DataRightsDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity)
    : EfOutboxWriter<DataRightsDbContext>(dbContext, clock, applicationIdentity, DataRightsMigrations.Schema);
