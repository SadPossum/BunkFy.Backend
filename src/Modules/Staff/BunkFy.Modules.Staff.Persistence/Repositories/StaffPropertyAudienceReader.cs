namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffPropertyAudienceReader(StaffDbContext dbContext) : IStaffPropertyAudienceReader
{
    public async Task<IReadOnlyList<string>> ListActiveAuthSubjectIdsAsync(
        string scopeId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        string normalizedScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        ArgumentOutOfRangeException.ThrowIfEqual(propertyId, Guid.Empty);

        return await dbContext.StaffMembers
            .AsNoTracking()
            .Where(member => member.ScopeId == normalizedScopeId &&
                member.Status == StaffMemberState.Active &&
                member.AuthSubjectId != null &&
                member.Assignments.Any(assignment =>
                    assignment.PropertyId == propertyId && assignment.IsCurrent))
            .Select(member => member.AuthSubjectId!)
            .Distinct()
            .OrderBy(authSubjectId => authSubjectId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<string?> GetAuthSubjectIdAsync(
        string scopeId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        string normalizedScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        ArgumentOutOfRangeException.ThrowIfEqual(staffMemberId, Guid.Empty);

        return dbContext.StaffMembers
            .AsNoTracking()
            .Where(member => member.ScopeId == normalizedScopeId && member.Id == staffMemberId)
            .Select(member => member.AuthSubjectId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
