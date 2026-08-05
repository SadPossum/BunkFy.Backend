namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspaceStaffOnboardingOperationLock(
    WorkspacesDbContext dbContext)
    : IWorkspaceStaffOnboardingOperationLock
{
    public async Task<bool> TryAcquireAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException(
                "An onboarding operation lock requires an application identifier.",
                nameof(applicationId));
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "An onboarding operation lock requires an active database transaction.");
        }

        if (!dbContext.Database.IsRelational())
        {
            return await dbContext.StaffOnboardingApplications
                .AsNoTracking()
                .AnyAsync(
                    application => application.Id == applicationId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.AcquireOperationalMutationAdmissionAsync(
            cancellationToken).ConfigureAwait(false);
        int affected = await dbContext.StaffOnboardingApplications
            .Where(application => application.Id == applicationId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    application => application.Version,
                    application => application.Version),
                cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }
}
