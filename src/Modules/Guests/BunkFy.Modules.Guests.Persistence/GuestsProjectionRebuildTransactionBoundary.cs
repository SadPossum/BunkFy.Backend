namespace BunkFy.Modules.Guests.Persistence;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using BunkFy.Modules.Guests.Contracts;

internal sealed class GuestsProjectionRebuildTransactionBoundary(GuestsDbContext dbContext)
    : EfProjectionRebuildTransactionBoundary<GuestsDbContext>(dbContext, GuestsModuleMetadata.Name);
