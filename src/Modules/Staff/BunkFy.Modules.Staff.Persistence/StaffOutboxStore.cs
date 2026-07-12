namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Microsoft.Extensions.Options;

internal sealed class StaffOutboxStore(StaffDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<StaffDbContext>(dbContext, options, StaffMigrations.Schema);
