namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;

internal sealed partial class
    WorkspaceStaffCorrelationAnonymisationRepository
{
    private async Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
        ReadCoreAsync(
            string tenantId,
            Guid anchorProcessId,
            long anchorVersion,
            string? expectedPseudonym,
            CancellationToken cancellationToken)
    {
        if (!TenantIds.TryNormalize(
                tenantId,
                out string? normalizedTenant) ||
            anchorProcessId == Guid.Empty ||
            anchorVersion <= 0)
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Unavailable();
        }

        AnchorState? anchor = await this.dbContext
            .StaffAccessProcesses
            .AsNoTracking()
            .Where(process =>
                process.ScopeId == normalizedTenant &&
                process.Id == anchorProcessId &&
                process.TargetState ==
                    WorkspaceStaffAccessTargetState.Departed &&
                process.State ==
                    WorkspaceStaffAccessProcessState.Completed)
            .Select(process => new AnchorState(
                process.Id,
                process.Version,
                process.StaffMemberId,
                process.TargetStaffVersion,
                process.SubjectId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (anchor is null)
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Unavailable();
        }

        if (anchor.Version != anchorVersion)
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Stale();
        }

        bool subjectMatches = expectedPseudonym is null
            ? !IsPseudonym(anchor.SubjectId)
            : string.Equals(
                anchor.SubjectId,
                expectedPseudonym,
                StringComparison.Ordinal);
        if (!subjectMatches)
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Conflict();
        }

        return await this.BuildSnapshotAsync(
            normalizedTenant,
            anchor,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
        BuildSnapshotAsync(
            string tenantId,
            AnchorState anchor,
            CancellationToken cancellationToken)
    {
        bool retentionProofExists = await this.dbContext
            .StaffRetentionCorrelationReceipts
            .AsNoTracking()
            .AnyAsync(
                receipt =>
                    receipt.ScopeId == tenantId &&
                    receipt.StaffMemberId ==
                        anchor.StaffMemberId &&
                    receipt.SelectedStaffVersion ==
                        anchor.TargetStaffVersion,
                cancellationToken).ConfigureAwait(false);
        if (retentionProofExists)
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Conflict();
        }

        OnboardingState[] onboarding = await this.dbContext
            .StaffOnboardingApplications
            .AsNoTracking()
            .Where(application =>
                application.ScopeId == tenantId &&
                (application.StaffMemberId ==
                    anchor.StaffMemberId ||
                 application.SubjectId == anchor.SubjectId))
            .OrderBy(application => application.Id)
            .Take(MaximumCorrelationRecords + 1)
            .Select(application => new OnboardingState(
                application.Id,
                application.Version,
                application.Status,
                application.StaffMemberId,
                application.SubjectId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (onboarding.Any(record => IsActive(record.Status)))
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .ActiveOnboarding();
        }

        if (onboarding.Any(record =>
                record.StaffMemberId == anchor.StaffMemberId &&
                !string.Equals(
                    record.SubjectId,
                    anchor.SubjectId,
                    StringComparison.Ordinal)))
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Conflict();
        }

        AccessProcessState[] accessProcesses = await this.dbContext
            .StaffAccessProcesses
            .AsNoTracking()
            .Where(process =>
                process.ScopeId == tenantId &&
                (process.StaffMemberId == anchor.StaffMemberId ||
                 process.SubjectId == anchor.SubjectId ||
                 process.RequestedBy == anchor.SubjectId))
            .OrderBy(process => process.Id)
            .Take(MaximumCorrelationRecords + 1)
            .Select(process => new AccessProcessState(
                process.Id,
                process.Version,
                process.StaffMemberId,
                process.TargetStaffVersion,
                process.TargetState,
                process.State,
                process.SubjectId,
                process.RequestedBy))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (accessProcesses.Any(record =>
                record.State !=
                    WorkspaceStaffAccessProcessState.Completed))
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .ActiveAccessProcess();
        }

        if (accessProcesses.Any(record =>
                record.StaffMemberId == anchor.StaffMemberId &&
                !string.Equals(
                    record.SubjectId,
                    anchor.SubjectId,
                    StringComparison.Ordinal)))
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Conflict();
        }

        AccessPlanState[] accessPlans = await this.dbContext
            .StaffAccessPlans
            .AsNoTracking()
            .Where(plan =>
                plan.ScopeId == tenantId &&
                plan.CreatedBySubjectId == anchor.SubjectId)
            .OrderBy(plan => plan.Id)
            .Take(MaximumCorrelationRecords + 1)
            .Select(plan => new AccessPlanState(
                plan.Id,
                plan.Version,
                plan.Status,
                plan.CreatedBySubjectId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (onboarding.Length + accessProcesses.Length +
                accessPlans.Length >
            MaximumCorrelationRecords)
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Oversized();
        }

        AccessProcessState? selectedAnchor =
            accessProcesses.SingleOrDefault(record =>
                record.Id == anchor.Id);
        if (selectedAnchor is null ||
            selectedAnchor.Version != anchor.Version ||
            selectedAnchor.StaffMemberId != anchor.StaffMemberId ||
            selectedAnchor.TargetStaffVersion !=
                anchor.TargetStaffVersion ||
            selectedAnchor.TargetState !=
                WorkspaceStaffAccessTargetState.Departed ||
            selectedAnchor.State !=
                WorkspaceStaffAccessProcessState.Completed ||
            !string.Equals(
                selectedAnchor.SubjectId,
                anchor.SubjectId,
                StringComparison.Ordinal))
        {
            return WorkspaceStaffCorrelationAnonymisationSnapshot
                .Conflict();
        }

        return new(
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .Eligible,
            anchor.Id,
            anchor.Version,
            anchor.StaffMemberId,
            anchor.TargetStaffVersion,
            anchor.SubjectId,
            ComputeDigest(
                anchor,
                onboarding,
                accessProcesses,
                accessPlans),
            onboarding.Length,
            accessProcesses.Length,
            accessPlans.Length);
    }
}
