namespace BunkFy.Modules.Ingestion.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;

internal sealed class IngestionOutboxWriter(
    IngestionDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity)
    : EfOutboxWriter<IngestionDbContext>(dbContext, clock, applicationIdentity, IngestionMigrations.Schema);
