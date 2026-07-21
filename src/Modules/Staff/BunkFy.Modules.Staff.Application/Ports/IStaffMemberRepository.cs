namespace BunkFy.Modules.Staff.Application.Ports;

using Gma.Framework.Pagination;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

public interface IStaffMemberRepository
{
    Task AddAsync(StaffMember member, CancellationToken cancellationToken);
    Task<StaffMember?> GetAsync(Guid staffMemberId, CancellationToken cancellationToken);
    Task<StaffMember?> GetByAuthSubjectAsync(string authSubjectId, CancellationToken cancellationToken);
    Task<StaffDirectoryMemberDto?> GetDirectoryAsync(Guid staffMemberId, CancellationToken cancellationToken);
    Task<StaffDirectoryMemberDto?> GetDirectoryAtPropertyAsync(
        Guid propertyId,
        Guid staffMemberId,
        CancellationToken cancellationToken);
    Task<StaffDirectoryListResponse> ListDirectoryAsync(
        string? search,
        StaffStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
    Task<StaffDirectoryListResponse> ListDirectoryAtPropertyAsync(
        Guid propertyId,
        string? search,
        StaffStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
    Task<bool> EmployeeNumberExistsAsync(string employeeNumber, Guid? exceptStaffMemberId,
        CancellationToken cancellationToken);
    Task<bool> AuthSubjectExistsAsync(string authSubjectId, Guid? exceptStaffMemberId,
        CancellationToken cancellationToken);
}
