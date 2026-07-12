namespace BunkFy.Modules.Staff.Persistence.Repositories;

using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class StaffMemberRepository(StaffDbContext dbContext) : IStaffMemberRepository
{
    public Task AddAsync(StaffMember member, CancellationToken cancellationToken)
    {
        dbContext.StaffMembers.Add(member);
        return Task.CompletedTask;
    }

    public Task<StaffMember?> GetAsync(Guid staffMemberId, CancellationToken cancellationToken) =>
        dbContext.StaffMembers.Include(member => member.Assignments)
            .FirstOrDefaultAsync(member => member.Id == staffMemberId, cancellationToken);

    public Task<StaffMember?> GetAtPropertyAsync(Guid propertyId, Guid staffMemberId,
        CancellationToken cancellationToken) => dbContext.StaffMembers
        .Include(member => member.Assignments)
        .FirstOrDefaultAsync(member => member.Id == staffMemberId &&
            member.Assignments.Any(assignment => assignment.PropertyId == propertyId && assignment.IsCurrent) &&
            dbContext.PropertyProjections.Any(property => property.Id == propertyId &&
                property.Status == BunkFy.Modules.Properties.Contracts.PropertyStatus.Active), cancellationToken);

    public Task<StaffListResponse> ListAsync(string? search, StaffStatus? status,
        PageRequest pageRequest, CancellationToken cancellationToken) =>
        ListCoreAsync(dbContext.StaffMembers.AsNoTracking().Include(member => member.Assignments),
            search, status, pageRequest, cancellationToken);

    public Task<StaffListResponse> ListAtPropertyAsync(Guid propertyId, string? search,
        StaffStatus? status, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        IQueryable<StaffMember> query = dbContext.StaffMembers.AsNoTracking()
            .Include(member => member.Assignments)
            .Where(member => member.Assignments.Any(assignment => assignment.PropertyId == propertyId &&
                    assignment.IsCurrent) &&
                dbContext.PropertyProjections.Any(property => property.Id == propertyId &&
                    property.Status == BunkFy.Modules.Properties.Contracts.PropertyStatus.Active));
        return ListCoreAsync(query, search, status, pageRequest, cancellationToken);
    }

    public Task<bool> EmployeeNumberExistsAsync(string employeeNumber, Guid? exceptStaffMemberId,
        CancellationToken cancellationToken)
    {
        string normalized = employeeNumber.Trim().ToUpperInvariant();
        return dbContext.StaffMembers.AsNoTracking().AnyAsync(member =>
            member.EmployeeNumberSearch == normalized &&
            (!exceptStaffMemberId.HasValue || member.Id != exceptStaffMemberId.Value), cancellationToken);
    }

    public Task<bool> AuthSubjectExistsAsync(string authSubjectId, Guid? exceptStaffMemberId,
        CancellationToken cancellationToken)
    {
        string normalized = authSubjectId.Trim();
        return dbContext.StaffMembers.AsNoTracking().AnyAsync(member =>
            member.AuthSubjectId == normalized &&
            (!exceptStaffMemberId.HasValue || member.Id != exceptStaffMemberId.Value), cancellationToken);
    }

    private static async Task<StaffListResponse> ListCoreAsync(IQueryable<StaffMember> query,
        string? search, StaffStatus? status, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        if (status.HasValue)
        {
            StaffMemberState state = status.Value switch
            {
                StaffStatus.Active => StaffMemberState.Active,
                StaffStatus.Suspended => StaffMemberState.Suspended,
                StaffStatus.Departed => StaffMemberState.Departed,
                _ => StaffMemberState.Unknown
            };
            query = query.Where(member => member.Status == state);
        }

        string? normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToUpperInvariant();
        if (normalizedSearch is not null)
        {
            query = query.Where(member => member.DisplayNameSearch.Contains(normalizedSearch) ||
                (member.LegalNameSearch != null && member.LegalNameSearch.Contains(normalizedSearch)) ||
                (member.EmployeeNumberSearch != null && member.EmployeeNumberSearch.Contains(normalizedSearch)) ||
                (member.WorkEmailSearch != null && member.WorkEmailSearch.Contains(normalizedSearch)));
        }

        StaffMember[] rows = await query.OrderBy(member => member.DisplayName)
            .ThenBy(member => member.Id).Skip(pageRequest.SkipCount).Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new(rows.Select(member => member.ToDto()).ToArray(), pageRequest.Page, pageRequest.PageSize);
    }
}
