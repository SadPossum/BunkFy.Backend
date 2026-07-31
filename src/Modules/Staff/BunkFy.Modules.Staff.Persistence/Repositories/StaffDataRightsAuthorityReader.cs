namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffDataRightsAuthorityReader(
    StaffDbContext dbContext)
    : IStaffDataRightsAuthorityReader
{
    public async Task<StaffDataRightsAuthorityState?> ReadAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        string normalizedTenant =
            ScopeIds.Normalize(tenantId, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(
            staffMemberId,
            Guid.Empty);

        var row = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(member =>
                member.ScopeId == normalizedTenant &&
                member.Id == staffMemberId)
            .Select(member => new
            {
                member.Id,
                member.Version,
                member.Status
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null
            ? null
            : new StaffDataRightsAuthorityState(
                row.Id,
                row.Version,
                Map(row.Status));
    }

    private static StaffDataRightsAuthorityRecordState Map(
        StaffMemberState state) =>
        state switch
        {
            StaffMemberState.Active =>
                StaffDataRightsAuthorityRecordState.Active,
            StaffMemberState.Suspended =>
                StaffDataRightsAuthorityRecordState.Suspended,
            StaffMemberState.Departed =>
                StaffDataRightsAuthorityRecordState.Departed,
            StaffMemberState.Anonymised =>
                StaffDataRightsAuthorityRecordState.Anonymised,
            _ => StaffDataRightsAuthorityRecordState.Unknown
        };
}
