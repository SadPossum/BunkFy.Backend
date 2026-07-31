namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffNotificationRecipientResolver(
    StaffDbContext dbContext)
    : IStaffNotificationRecipientResolver
{
    public async Task<IReadOnlyList<StaffNotificationRecipient>>
        ResolveActiveAsync(
            string scopeId,
            IReadOnlyCollection<string> authSubjectIds,
            CancellationToken cancellationToken)
    {
        string normalizedScopeId =
            ScopeIds.Normalize(scopeId, nameof(scopeId));
        ArgumentNullException.ThrowIfNull(authSubjectIds);
        if (authSubjectIds.Count >
            StaffNotificationRecipientContract.MaximumCandidateCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authSubjectIds),
                $"At most {StaffNotificationRecipientContract.MaximumCandidateCount} subjects can be resolved at once.");
        }

        string[] candidates = authSubjectIds
            .Select(authSubjectId =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(authSubjectId);
                return authSubjectId.Trim();
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
        {
            return [];
        }

        var rows = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(member =>
                member.ScopeId == normalizedScopeId &&
                member.Status == StaffMemberState.Active &&
                member.AuthSubjectId != null &&
                candidates.Contains(member.AuthSubjectId) &&
                dbContext.ProcessingRestrictionProjections.Any(projection =>
                    projection.StaffMemberId == member.Id &&
                    projection.ContractVersion ==
                        StaffProcessingRestrictionContract.CurrentVersion &&
                    !projection.IsRestricted))
            .Select(member => new
            {
                StaffMemberId = member.Id,
                AuthSubjectId = member.AuthSubjectId!
            })
            .OrderBy(recipient => recipient.AuthSubjectId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(recipient => new StaffNotificationRecipient(
                recipient.StaffMemberId,
                recipient.AuthSubjectId))
            .ToArray();
    }
}
