namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;

internal sealed partial class
    WorkspaceStaffCorrelationAnonymisationRepository
    : IWorkspaceStaffCorrelationAnonymisationRepository
{
    internal const int MaximumCorrelationRecords = 512;
    internal const string DataRightsPseudonymPrefix =
        WorkspaceStaffCorrelationAnonymisationReceipt
            .PseudonymPrefix;

    private readonly WorkspacesDbContext dbContext;

    public WorkspaceStaffCorrelationAnonymisationRepository(
        WorkspacesDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        this.dbContext = dbContext;
    }

    public async Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
        ResolveAsync(
            string tenantId,
            Guid staffMemberId,
            long selectedStaffVersion,
            string? subjectId,
            CancellationToken cancellationToken)
    {
        if (!TenantIds.TryNormalize(
                tenantId,
                out string? normalizedTenant) ||
            staffMemberId == Guid.Empty ||
            selectedStaffVersion <= 0)
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Unavailable();
        }

        string? normalizedSubject = NormalizeSubject(subjectId);
        if (normalizedSubject is null)
        {
            bool hasMapping = await this.dbContext
                .StaffAccessProcesses
                .AsNoTracking()
                .AnyAsync(
                    process =>
                        process.ScopeId == normalizedTenant &&
                        process.StaffMemberId == staffMemberId &&
                        process.TargetStaffVersion ==
                            selectedStaffVersion,
                    cancellationToken).ConfigureAwait(false);
            return hasMapping
                ? WorkspaceStaffCorrelationAnonymisationSnapshot
                    .Conflict()
                : WorkspaceStaffCorrelationAnonymisationSnapshot
                    .NoCorrelation();
        }

        if (IsPseudonym(normalizedSubject))
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Conflict();
        }

        AnchorState[] anchors = await this.dbContext
            .StaffAccessProcesses
            .AsNoTracking()
            .Where(process =>
                process.ScopeId == normalizedTenant &&
                process.StaffMemberId == staffMemberId &&
                process.TargetStaffVersion == selectedStaffVersion &&
                process.TargetState ==
                    WorkspaceStaffAccessTargetState.Departed &&
                process.State ==
                    WorkspaceStaffAccessProcessState.Completed)
            .OrderBy(process => process.Id)
            .Take(2)
            .Select(process => new AnchorState(
                process.Id,
                process.Version,
                process.StaffMemberId,
                process.TargetStaffVersion,
                process.SubjectId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (anchors.Length != 1)
        {
            return anchors.Length == 0
                ? WorkspaceStaffCorrelationAnonymisationSnapshot
                    .Unavailable()
                : WorkspaceStaffCorrelationAnonymisationSnapshot
                    .Conflict();
        }

        AnchorState anchor = anchors[0];
        if (!string.Equals(
                anchor.SubjectId,
                normalizedSubject,
                StringComparison.Ordinal))
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Conflict();
        }

        return await this.BuildSnapshotAsync(
            normalizedTenant,
            anchor,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
        ReadAsync(
            string tenantId,
            Guid anchorProcessId,
            long selectedAnchorVersion,
            CancellationToken cancellationToken) =>
        this.ReadCoreAsync(
            tenantId,
            anchorProcessId,
            selectedAnchorVersion,
            expectedPseudonym: null,
            cancellationToken);

    public Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
        ReadAnonymisedAsync(
            string tenantId,
            Guid anchorProcessId,
            long resultingAnchorVersion,
            Guid ownerReceiptId,
            CancellationToken cancellationToken)
    {
        string pseudonym = CreatePseudonym(ownerReceiptId);
        return ownerReceiptId == Guid.Empty
            ? Task.FromResult(
                WorkspaceStaffCorrelationAnonymisationSnapshot
                    .Unavailable())
            : this.ReadCoreAsync(
                tenantId,
                anchorProcessId,
                resultingAnchorVersion,
                pseudonym,
                cancellationToken);
    }

    public Task<WorkspaceStaffCorrelationAnonymisationReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
        this.dbContext.StaffCorrelationAnonymisationReceipts
            .SingleOrDefaultAsync(
                receipt =>
                    receipt.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<WorkspaceStaffCorrelationAnonymisationTombstone?>
        GetTombstoneAsync(
            Guid anchorProcessId,
            CancellationToken cancellationToken) =>
        this.dbContext.StaffCorrelationAnonymisationTombstones
            .SingleOrDefaultAsync(
                tombstone => tombstone.Id == anchorProcessId,
                cancellationToken);

    public Task<WorkspaceStaffCorrelationAnonymisationTombstone?>
        FindTombstoneAsync(
            string tenantId,
            Guid staffMemberId,
            long selectedStaffVersion,
            CancellationToken cancellationToken) =>
        this.dbContext.StaffCorrelationAnonymisationTombstones
            .SingleOrDefaultAsync(
                tombstone =>
                    tombstone.ScopeId == tenantId &&
                    tombstone.StaffMemberId == staffMemberId &&
                    tombstone.SelectedStaffVersion ==
                        selectedStaffVersion,
                cancellationToken);

    public Task<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt?>
        GetRestoreReceiptAsync(
            Guid ledgerEntryId,
            CancellationToken cancellationToken) =>
        this.dbContext.StaffCorrelationAnonymisationRestoreReceipts
            .SingleOrDefaultAsync(
                receipt => receipt.Id == ledgerEntryId,
                cancellationToken);
}
