namespace BunkFy.Modules.Staff.Application.Contributors;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Pagination;

internal static class StaffDataHoldSnapshotReader
{
    public static async Task<IReadOnlyCollection<StaffDataHold>?> ReadAsync(
        string tenantId,
        Guid staffMemberId,
        IStaffDataHoldRepository repository,
        CancellationToken cancellationToken)
    {
        long count = await repository.CountAsync(
            staffMemberId,
            status: null,
            cancellationToken).ConfigureAwait(false);
        if (count is < 0 or >
            StaffDataHold.MaximumRecordsPerStaffMember)
        {
            return null;
        }

        List<StaffDataHold> snapshot = new((int)count);
        int pageCount = (int)Math.Ceiling(
            count / (double)PageRequest.MaxPageSize);
        for (int page = 1; page <= pageCount; page++)
        {
            snapshot.AddRange(await repository.ListAsync(
                staffMemberId,
                status: null,
                new PageRequest(page, PageRequest.MaxPageSize),
                cancellationToken).ConfigureAwait(false));
        }

        return snapshot.Count == count &&
            snapshot.All(hold =>
                hold.StaffMemberId == staffMemberId &&
                string.Equals(
                    hold.ScopeId,
                    tenantId,
                    StringComparison.Ordinal))
            ? snapshot
            : null;
    }
}
