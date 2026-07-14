namespace BunkFy.Modules.Staff.Application.Ports;

using Gma.Framework.Pagination;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

public interface IStaffMemberRepository
{
    Task AddAsync(StaffMember member, CancellationToken cancellationToken);
    Task<StaffMember?> GetAsync(Guid staffMemberId, CancellationToken cancellationToken);
    Task<StaffMember?> GetByAuthSubjectAsync(string authSubjectId, CancellationToken cancellationToken);
    Task<StaffMember?> GetAtPropertyAsync(Guid propertyId, Guid staffMemberId, CancellationToken cancellationToken);
    Task<StaffListResponse> ListAsync(string? search, StaffStatus? status, PageRequest pageRequest,
        CancellationToken cancellationToken);
    Task<StaffListResponse> ListAtPropertyAsync(Guid propertyId, string? search, StaffStatus? status,
        PageRequest pageRequest, CancellationToken cancellationToken);
    Task<bool> EmployeeNumberExistsAsync(string employeeNumber, Guid? exceptStaffMemberId,
        CancellationToken cancellationToken);
    Task<bool> AuthSubjectExistsAsync(string authSubjectId, Guid? exceptStaffMemberId,
        CancellationToken cancellationToken);
}
