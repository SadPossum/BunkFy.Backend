namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Domain;
using Microsoft.EntityFrameworkCore;

internal sealed partial class
    WorkspaceStaffCorrelationAnonymisationRepository
{
    private async Task<MutationCounts> ScrubAsync(
        string tenantId,
        string subjectId,
        string pseudonym,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        if (this.dbContext.Database.IsRelational())
        {
            int onboarding = await this.dbContext
                .StaffOnboardingApplications
                .Where(application =>
                    application.ScopeId == tenantId &&
                    application.SubjectId == subjectId &&
                    (application.Status ==
                        WorkspaceStaffOnboardingState.Completed ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Rejected ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Superseded ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Expired ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Withdrawn))
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(
                            application =>
                                application.SubjectId,
                            pseudonym)
                        .SetProperty(
                            application =>
                                application.LastChangedAtUtc,
                            completedAtUtc)
                        .SetProperty(
                            application => application.Version,
                            application =>
                                application.Version + 1),
                    cancellationToken).ConfigureAwait(false);
            int processes = await this.dbContext
                .StaffAccessProcesses
                .Where(process =>
                    process.ScopeId == tenantId &&
                    process.State ==
                        WorkspaceStaffAccessProcessState.Completed &&
                    (process.SubjectId == subjectId ||
                     process.RequestedBy == subjectId))
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
                            completedAtUtc)
                        .SetProperty(
                            process => process.Version,
                            process => process.Version + 1),
                    cancellationToken).ConfigureAwait(false);
            int plans = await this.dbContext
                .StaffAccessPlans
                .Where(plan =>
                    plan.ScopeId == tenantId &&
                    plan.CreatedBySubjectId == subjectId)
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(
                            plan =>
                                plan.CreatedBySubjectId,
                            pseudonym)
                        .SetProperty(
                            plan => plan.LastChangedAtUtc,
                            completedAtUtc)
                        .SetProperty(
                            plan => plan.Version,
                            plan => plan.Version + 1),
                    cancellationToken).ConfigureAwait(false);
            return new(onboarding, processes, plans);
        }

        WorkspaceStaffOnboarding[] onboardingRecords =
            await this.dbContext.StaffOnboardingApplications
                .Where(application =>
                    application.ScopeId == tenantId &&
                    application.SubjectId == subjectId &&
                    (application.Status ==
                        WorkspaceStaffOnboardingState.Completed ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Rejected ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Superseded ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Expired ||
                     application.Status ==
                        WorkspaceStaffOnboardingState.Withdrawn))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        WorkspaceStaffAccessProcess[] processRecords =
            await this.dbContext.StaffAccessProcesses
                .Where(process =>
                    process.ScopeId == tenantId &&
                    process.State ==
                        WorkspaceStaffAccessProcessState.Completed &&
                    (process.SubjectId == subjectId ||
                     process.RequestedBy == subjectId))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        WorkspaceStaffAccessPlan[] planRecords =
            await this.dbContext.StaffAccessPlans
                .Where(plan =>
                    plan.ScopeId == tenantId &&
                    plan.CreatedBySubjectId == subjectId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

        foreach (WorkspaceStaffOnboarding record in
                 onboardingRecords)
        {
            Set(record, nameof(record.SubjectId), pseudonym);
            Set(
                record,
                nameof(record.LastChangedAtUtc),
                completedAtUtc);
            Set(record, nameof(record.Version), record.Version + 1);
        }

        foreach (WorkspaceStaffAccessProcess record in processRecords)
        {
            if (string.Equals(
                    record.SubjectId,
                    subjectId,
                    StringComparison.Ordinal))
            {
                Set(record, nameof(record.SubjectId), pseudonym);
            }

            if (string.Equals(
                    record.RequestedBy,
                    subjectId,
                    StringComparison.Ordinal))
            {
                Set(record, nameof(record.RequestedBy), pseudonym);
            }

            Set(
                record,
                nameof(record.LastChangedAtUtc),
                completedAtUtc);
            Set(record, nameof(record.Version), record.Version + 1);
        }

        foreach (WorkspaceStaffAccessPlan record in planRecords)
        {
            Set(
                record,
                nameof(record.CreatedBySubjectId),
                pseudonym);
            Set(
                record,
                nameof(record.LastChangedAtUtc),
                completedAtUtc);
            Set(record, nameof(record.Version), record.Version + 1);
        }

        await this.dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return new(
            onboardingRecords.Length,
            processRecords.Length,
            planRecords.Length);
    }

    private static void Set<TEntity>(
        TEntity entity,
        string propertyName,
        object value)
        where TEntity : class
    {
        typeof(TEntity).GetProperty(propertyName)!.SetValue(
            entity,
            value);
    }
}
