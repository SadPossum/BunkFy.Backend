namespace BunkFy.Modules.Staff.Persistence.Repositories;

using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;
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

    public Task<StaffMember?> GetByAuthSubjectAsync(string authSubjectId, CancellationToken cancellationToken)
    {
        string normalized = authSubjectId.Trim();
        return dbContext.StaffMembers.Include(member => member.Assignments)
            .FirstOrDefaultAsync(member => member.AuthSubjectId == normalized, cancellationToken);
    }

    public Task<StaffDirectoryMemberDto?> GetDirectoryAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken) => ProjectDirectory(
            dbContext.StaffMembers.AsNoTracking().Where(member => member.Id == staffMemberId),
            visiblePropertyId: null)
        .SingleOrDefaultAsync(cancellationToken);

    public Task<StaffDirectoryMemberDto?> GetDirectoryAtPropertyAsync(
        Guid propertyId,
        Guid staffMemberId,
        CancellationToken cancellationToken) => ProjectDirectory(
            dbContext.StaffMembers.AsNoTracking().Where(member =>
                member.Id == staffMemberId &&
                member.Assignments.Any(assignment =>
                    assignment.PropertyId == propertyId && assignment.IsCurrent) &&
                dbContext.PropertyProjections.Any(property =>
                    property.Id == propertyId &&
                    property.Status == BunkFy.Modules.Properties.Contracts.PropertyStatus.Active)),
            propertyId)
        .SingleOrDefaultAsync(cancellationToken);

    public Task<StaffDirectoryListResponse> ListDirectoryAsync(
        string? search,
        StaffStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken) => ListDirectoryCoreAsync(
            dbContext.StaffMembers.AsNoTracking(),
            visiblePropertyId: null,
            search,
            status,
            pageRequest,
            cancellationToken);

    public Task<StaffDirectoryListResponse> ListDirectoryAtPropertyAsync(
        Guid propertyId,
        string? search,
        StaffStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<StaffMember> query = dbContext.StaffMembers.AsNoTracking()
            .Where(member => member.Assignments.Any(assignment => assignment.PropertyId == propertyId &&
                    assignment.IsCurrent) &&
                dbContext.PropertyProjections.Any(property => property.Id == propertyId &&
                    property.Status == BunkFy.Modules.Properties.Contracts.PropertyStatus.Active));
        return ListDirectoryCoreAsync(
            query,
            propertyId,
            search,
            status,
            pageRequest,
            cancellationToken);
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

    private static async Task<StaffDirectoryListResponse> ListDirectoryCoreAsync(
        IQueryable<StaffMember> query,
        Guid? visiblePropertyId,
        string? search,
        StaffStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
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
            query = query.Where(member => member.DisplayNameSearch.Contains(normalizedSearch));
        }

        StaffDirectoryMemberDto[] rows = await ProjectDirectory(
                query.OrderBy(member => member.DisplayName)
                    .ThenBy(member => member.Id)
                    .Skip(pageRequest.SkipCount)
                    .Take(pageRequest.PageSize),
                visiblePropertyId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new(rows, pageRequest.Page, pageRequest.PageSize);
    }

    private static IQueryable<StaffDirectoryMemberDto> ProjectDirectory(
        IQueryable<StaffMember> query,
        Guid? visiblePropertyId) => query.Select(member => new StaffDirectoryMemberDto(
        member.Id,
        member.DisplayName,
        member.JobTitle,
        member.Department,
        (StaffStatus)member.Status,
        member.Version,
        member.Assignments
            .Where(assignment => assignment.IsCurrent &&
                                 (!visiblePropertyId.HasValue ||
                                  assignment.PropertyId == visiblePropertyId.Value))
            .OrderByDescending(assignment => assignment.IsPrimary)
            .ThenBy(assignment => assignment.PropertyId)
            .Select(assignment => new StaffDirectoryAssignmentDto(
                assignment.Id,
                assignment.PropertyId,
                assignment.PropertyJobTitle,
                assignment.IsPrimary,
                assignment.EffectiveFrom))
            .ToArray()));
}
