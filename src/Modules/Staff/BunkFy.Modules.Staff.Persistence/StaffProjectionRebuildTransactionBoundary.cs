namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using BunkFy.Modules.Staff.Contracts;

internal sealed class StaffProjectionRebuildTransactionBoundary(StaffDbContext dbContext)
    : EfProjectionRebuildTransactionBoundary<StaffDbContext>(dbContext, StaffModuleMetadata.Name);
