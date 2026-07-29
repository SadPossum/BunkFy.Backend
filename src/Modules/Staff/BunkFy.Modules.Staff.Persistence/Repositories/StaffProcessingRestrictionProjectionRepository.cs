namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffProcessingRestrictionProjectionRepository(
    StaffDbContext dbContext)
    : IStaffProcessingRestrictionProjectionRepository
{
    public Task<StaffProcessingRestrictionProjection?> GetAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        dbContext.ProcessingRestrictionProjections.FirstOrDefaultAsync(
            projection => projection.StaffMemberId == staffMemberId,
            cancellationToken);
}
