namespace BunkFy.Modules.Properties.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;

internal sealed class PropertiesOutboxWriter(
    PropertiesDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IEnumerable<IIntegrationEventScopeResolver> scopeResolvers)
    : EfOutboxWriter<PropertiesDbContext>(
        dbContext,
        clock,
        applicationIdentity,
        PropertiesMigrations.Schema,
        scopeResolvers);
