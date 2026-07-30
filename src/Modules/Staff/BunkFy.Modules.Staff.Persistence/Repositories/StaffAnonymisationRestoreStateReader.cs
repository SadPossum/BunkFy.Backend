namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffAnonymisationRestoreStateReader(
    StaffDbContext dbContext)
    : IStaffAnonymisationRestoreStateReader
{
    public async Task<StaffAnonymisationRestoreState?> ReadAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        if (staffMemberId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return null;
        }

        StaffMember? member = await dbContext.StaffMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == staffMemberId &&
                    candidate.ScopeId == scopeId,
                cancellationToken).ConfigureAwait(false);
        return member is null
            ? null
            : new StaffAnonymisationRestoreState(
                member.Id,
                member.Version,
                MapState(member.Status),
                member.AuthSubjectId,
                member.AnonymisedAtUtc);
    }

    private static StaffAnonymisationRestoreRecordState MapState(
        StaffMemberState state) =>
        state switch
        {
            StaffMemberState.Active =>
                StaffAnonymisationRestoreRecordState.Active,
            StaffMemberState.Suspended =>
                StaffAnonymisationRestoreRecordState.Suspended,
            StaffMemberState.Departed =>
                StaffAnonymisationRestoreRecordState.Departed,
            StaffMemberState.Anonymised =>
                StaffAnonymisationRestoreRecordState.Anonymised,
            _ => StaffAnonymisationRestoreRecordState.Unknown
        };
}
