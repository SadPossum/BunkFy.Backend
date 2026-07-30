namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Governance;
using BunkFy.Modules.Staff.Domain.Models;
using BunkFy.Modules.Staff.Persistence.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffRetentionCandidateRepository(
    StaffDbContext dbContext)
    : IStaffRetentionCandidateRepository
{
    public async Task<StaffRetentionScanPage> ScanAsync(
        long afterProjectionOrdinal,
        int limit,
        CancellationToken cancellationToken)
    {
        if (afterProjectionOrdinal < 0 || limit < 1)
        {
            throw new ArgumentOutOfRangeException(
                limit < 1
                    ? nameof(limit)
                    : nameof(afterProjectionOrdinal));
        }

        IQueryable<StaffMember> candidates = this.QueryCandidates()
            .Where(member =>
                member.ProjectionOrdinal > afterProjectionOrdinal)
            .OrderBy(member => member.ProjectionOrdinal)
            .Take(checked(limit + 1));
        StaffRetentionHead[] rows = await this.ProjectHeads(candidates)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        bool reachedEnd = rows.Length <= limit;
        StaffRetentionHead[] page = rows.Take(limit).ToArray();
        return new(
            await this.LoadSnapshotsAsync(
                page,
                cancellationToken).ConfigureAwait(false),
            reachedEnd);
    }

    public async Task<StaffRetentionCandidateSnapshot?> LoadAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        IQueryable<StaffMember> candidates = this.QueryCandidates()
            .Where(member => member.Id == staffMemberId);
        StaffRetentionHead? head = await this.ProjectHeads(candidates)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (head is null)
        {
            return null;
        }

        return (await this.LoadSnapshotsAsync(
                [head],
                cancellationToken).ConfigureAwait(false))
            .Single();
    }

    private IQueryable<StaffMember> QueryCandidates() =>
        dbContext.StaffMembers
            .AsNoTracking()
            .Where(member =>
                member.Status == StaffMemberState.Departed);

    private IQueryable<StaffRetentionHead> ProjectHeads(
        IQueryable<StaffMember> candidates) =>
        candidates.Select(member => new StaffRetentionHead(
            member.Id,
            member.Version,
            member.ProjectionOrdinal,
            member.Status,
            member.DepartedAtUtc,
            member.DepartureEffectiveOn,
            member.Assignments.Any(assignment =>
                assignment.IsCurrent),
            dbContext.DataHolds.Count(hold =>
                hold.StaffMemberId == member.Id &&
                hold.State == StaffDataHoldState.Active),
            dbContext.DataHolds
                .Where(hold =>
                    hold.StaffMemberId == member.Id &&
                    hold.State == StaffDataHoldState.Active)
                .Select(hold => (DateTimeOffset?)hold.PlacedAtUtc)
                .Min(),
            dbContext.ProcessingRestrictionProjections
                .Where(projection =>
                    projection.StaffMemberId == member.Id)
                .Select(projection =>
                    (int?)projection.ContractVersion)
                .SingleOrDefault(),
            dbContext.ProcessingRestrictionProjections
                .Where(projection =>
                    projection.StaffMemberId == member.Id)
                .Select(projection => (long?)projection.Revision)
                .SingleOrDefault(),
            dbContext.OperationLocks
                .Where(resourceLock =>
                    resourceLock.StaffMemberId == member.Id)
                .Select(resourceLock =>
                    (long?)resourceLock.Revision)
                .SingleOrDefault()));

    private async Task<IReadOnlyList<StaffRetentionCandidateSnapshot>>
        LoadSnapshotsAsync(
            IReadOnlyList<StaffRetentionHead> heads,
            CancellationToken cancellationToken)
    {
        if (heads.Count == 0)
        {
            return [];
        }

        Guid[] staffMemberIds = heads
            .Select(head => head.StaffMemberId)
            .Distinct()
            .ToArray();
        StaffEmploymentGovernance[] governance =
            await dbContext.EmploymentGovernance
                .AsNoTracking()
                .Include(item => item.AcceptedAcknowledgements)
                .Where(item =>
                    staffMemberIds.Contains(item.Id))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        Dictionary<Guid, StaffRetentionGovernanceSnapshot> byStaff =
            governance.ToDictionary(
                item => item.StaffMemberId,
                MapGovernance);

        return heads
            .Select(head => new StaffRetentionCandidateSnapshot(
                head.StaffMemberId,
                head.StaffVersion,
                head.ProjectionOrdinal,
                head.Status,
                head.DepartedAtUtc,
                head.DepartureEffectiveOn,
                head.HasCurrentAssignments,
                head.ActiveHoldCount,
                head.EarliestHoldPlacedAtUtc,
                head.ProcessingRestrictionContractVersion,
                head.ProcessingRestrictionRevision,
                head.OperationLockRevision,
                byStaff.GetValueOrDefault(head.StaffMemberId)))
            .ToArray();
    }

    private static StaffRetentionGovernanceSnapshot MapGovernance(
        StaffEmploymentGovernance governance) =>
        new(
            governance.Version,
            governance.SelectedStaffVersion,
            governance.Binding.OperatingCountryCode,
            governance.Binding.PolicyId,
            governance.Binding.PolicyVersion,
            governance.Binding.DataRegionId,
            governance.Binding.TransferProfileId,
            governance.Binding.RetentionPolicyId,
            governance.Binding.RetentionPolicyVersion,
            governance.Binding.ContentSha256,
            governance.Binding.PolicyEffectiveAtUtc,
            governance.Binding.PolicyExpiresAtUtc,
            governance.Binding.EvaluatedAtUtc,
            governance.ConfiguredAtUtc,
            governance.AcceptedAcknowledgements
                .OrderBy(
                    acknowledgement =>
                        acknowledgement.AcknowledgementId,
                    StringComparer.Ordinal)
                .ThenBy(
                    acknowledgement =>
                        acknowledgement.AcknowledgementVersion)
                .Select(acknowledgement =>
                    new StaffRetentionAcknowledgementSnapshot(
                        acknowledgement.AcknowledgementId,
                        acknowledgement.AcknowledgementVersion))
                .ToArray());

    private sealed record StaffRetentionHead(
        Guid StaffMemberId,
        long StaffVersion,
        long ProjectionOrdinal,
        StaffMemberState Status,
        DateTimeOffset? DepartedAtUtc,
        DateOnly? DepartureEffectiveOn,
        bool HasCurrentAssignments,
        int ActiveHoldCount,
        DateTimeOffset? EarliestHoldPlacedAtUtc,
        int? ProcessingRestrictionContractVersion,
        long? ProcessingRestrictionRevision,
        long? OperationLockRevision);
}
