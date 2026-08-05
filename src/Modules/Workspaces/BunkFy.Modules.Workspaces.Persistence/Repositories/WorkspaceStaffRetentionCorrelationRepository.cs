namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspaceStaffRetentionCorrelationRepository(
    WorkspacesDbContext dbContext)
    : IWorkspaceStaffRetentionCorrelationRepository
{
    public Task<WorkspaceStaffRetentionCorrelationReceipt?> GetAsync(
        Guid staffMemberId,
        long selectedStaffVersion,
        CancellationToken cancellationToken) =>
        dbContext.StaffRetentionCorrelationReceipts
            .SingleOrDefaultAsync(
                receipt =>
                    receipt.StaffMemberId == staffMemberId &&
                    receipt.SelectedStaffVersion ==
                        selectedStaffVersion,
                cancellationToken);

    public async Task<Result<
        WorkspaceStaffRetentionCorrelationReceipt>> ScrubAsync(
            WorkspaceStaffRetentionCorrelationScrubRequest command,
            CancellationToken cancellationToken)
    {
        WorkspaceStaffRetentionCorrelationReceipt? existing =
            await this.GetAsync(
                command.StaffMemberId,
                command.SelectedStaffVersion,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Matches(
                command.TenantId,
                command.StaffMemberId,
                command.SelectedStaffVersion)
                ? Result.Success(existing)
                : InvalidProof();
        }

        await dbContext.AcquireOperationalMutationAdmissionAsync(
            cancellationToken).ConfigureAwait(false);

        string? subjectId = NormalizeSubject(command.SubjectId);
        if (subjectId is not null)
        {
            bool activeOnboarding = await dbContext
                .StaffOnboardingApplications
                .AnyAsync(
                    application =>
                        application.SubjectId == subjectId &&
                        application.Status !=
                            WorkspaceStaffOnboardingState.Completed &&
                        application.Status !=
                            WorkspaceStaffOnboardingState.Rejected &&
                        application.Status !=
                            WorkspaceStaffOnboardingState.Superseded &&
                        application.Status !=
                            WorkspaceStaffOnboardingState.Expired,
                    cancellationToken)
                .ConfigureAwait(false);
            if (activeOnboarding)
            {
                return Result.Failure<
                    WorkspaceStaffRetentionCorrelationReceipt>(
                        WorkspaceStaffRetentionErrors
                            .ActiveOnboarding);
            }

            bool activeAccessProcess = await dbContext
                .StaffAccessProcesses
                .AnyAsync(
                    process =>
                        process.State !=
                            WorkspaceStaffAccessProcessState
                                .Completed &&
                        (process.SubjectId == subjectId ||
                         process.RequestedBy == subjectId),
                    cancellationToken)
                .ConfigureAwait(false);
            if (activeAccessProcess)
            {
                return Result.Failure<
                    WorkspaceStaffRetentionCorrelationReceipt>(
                        WorkspaceStaffRetentionErrors
                            .ActiveAccessProcess);
            }

            bool departureMapping = await dbContext
                .StaffAccessProcesses
                .AnyAsync(
                    process =>
                        process.StaffMemberId ==
                            command.StaffMemberId &&
                        process.TargetStaffVersion ==
                            command.SelectedStaffVersion &&
                        process.TargetState ==
                            WorkspaceStaffAccessTargetState
                                .Departed &&
                        process.State ==
                            WorkspaceStaffAccessProcessState
                                .Completed &&
                        process.SubjectId == subjectId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!departureMapping)
            {
                return Result.Failure<
                    WorkspaceStaffRetentionCorrelationReceipt>(
                        WorkspaceStaffRetentionErrors
                            .AccessMappingConflict);
            }
        }
        else
        {
            bool hasAccessMapping = await dbContext
                .StaffAccessProcesses
                .AnyAsync(
                    process =>
                        process.StaffMemberId ==
                            command.StaffMemberId &&
                        process.TargetStaffVersion ==
                            command.SelectedStaffVersion,
                    cancellationToken)
                .ConfigureAwait(false);
            if (hasAccessMapping)
            {
                return Result.Failure<
                    WorkspaceStaffRetentionCorrelationReceipt>(
                        WorkspaceStaffRetentionErrors
                            .AccessMappingConflict);
            }
        }

        string pseudonym = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{WorkspaceStaffRetentionCorrelationReceipt.PseudonymPrefix}" +
            $"{command.ReceiptId:N}");
        int onboardingCount = 0;
        int accessProcessCount = 0;
        int accessPlanCount = 0;
        if (subjectId is not null)
        {
            onboardingCount = await dbContext
                .StaffOnboardingApplications
                .Where(application =>
                    application.SubjectId == subjectId &&
                    (application.Status ==
                        WorkspaceStaffOnboardingState.Completed ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Rejected ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Superseded ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Expired))
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(
                            application =>
                                application.SubjectId,
                            pseudonym)
                        .SetProperty(
                            application =>
                                application.LastChangedAtUtc,
                            command.CompletedAtUtc)
                        .SetProperty(
                            application => application.Version,
                            application =>
                                application.Version + 1),
                    cancellationToken)
                .ConfigureAwait(false);
            accessProcessCount = await dbContext
                .StaffAccessProcesses
                .Where(process =>
                    process.SubjectId == subjectId ||
                    process.RequestedBy == subjectId)
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(
                            process => process.SubjectId,
                            process =>
                                process.SubjectId == subjectId
                                    ? pseudonym
                                    : process.SubjectId)
                        .SetProperty(
                            process => process.RequestedBy,
                            process =>
                                process.RequestedBy == subjectId
                                    ? pseudonym
                                    : process.RequestedBy)
                        .SetProperty(
                            process =>
                                process.LastChangedAtUtc,
                            command.CompletedAtUtc)
                        .SetProperty(
                            process => process.Version,
                            process => process.Version + 1),
                    cancellationToken)
                .ConfigureAwait(false);
            accessPlanCount = await dbContext
                .StaffAccessPlans
                .Where(plan =>
                    plan.CreatedBySubjectId == subjectId)
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(
                            plan =>
                                plan.CreatedBySubjectId,
                            pseudonym)
                        .SetProperty(
                            plan => plan.LastChangedAtUtc,
                            command.CompletedAtUtc)
                        .SetProperty(
                            plan => plan.Version,
                            plan => plan.Version + 1),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Result<WorkspaceStaffRetentionCorrelationReceipt>
            created =
            WorkspaceStaffRetentionCorrelationReceipt.Create(
                command.ReceiptId,
                command.TenantId,
                command.ExecutionId,
                command.StaffMemberId,
                command.SelectedStaffVersion,
                onboardingCount,
                accessProcessCount,
                accessPlanCount,
                command.CompletedAtUtc);
        if (created.IsFailure)
        {
            return created;
        }

        dbContext.StaffRetentionCorrelationReceipts.Add(
            created.Value);
        return created;
    }

    private static string? NormalizeSubject(string? subjectId)
    {
        string value = subjectId?.Trim() ?? string.Empty;
        return value.Length == 0 ? null : value;
    }

    private static Result<
        WorkspaceStaffRetentionCorrelationReceipt> InvalidProof() =>
        Result.Failure<
            WorkspaceStaffRetentionCorrelationReceipt>(
                WorkspaceStaffRetentionErrors.ReceiptInvalid);
}
