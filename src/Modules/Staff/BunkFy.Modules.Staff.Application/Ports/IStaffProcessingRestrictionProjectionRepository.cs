namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Domain.DataRights;

public interface IStaffProcessingRestrictionProjectionRepository
{
    Task<StaffProcessingRestrictionProjection?> GetAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);
}
