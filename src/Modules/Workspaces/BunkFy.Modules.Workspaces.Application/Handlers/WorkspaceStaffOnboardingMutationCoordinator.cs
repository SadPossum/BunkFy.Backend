namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;

internal enum WorkspaceStaffOnboardingSourceLockMode
{
    Read = 1,
    Write = 2
}

internal readonly record struct WorkspaceStaffOnboardingMutationLease(
    bool CoordinateExists,
    WorkspaceStaffOnboarding? Application)
{
    public static WorkspaceStaffOnboardingMutationLease Missing => new(false, null);
}

internal sealed class WorkspaceStaffOnboardingMutationCoordinator(
    IWorkspaceStaffOnboardingOperationLock operationLock,
    IWorkspaceStaffOnboardingRepository applications)
{
    public Task AcquireSourceAsync(
        Guid sourceId,
        WorkspaceStaffOnboardingSourceLockMode mode,
        CancellationToken cancellationToken) => mode switch
        {
            WorkspaceStaffOnboardingSourceLockMode.Read =>
                operationLock.AcquireSourceReadAsync(sourceId, cancellationToken),
            WorkspaceStaffOnboardingSourceLockMode.Write =>
                operationLock.AcquireSourceWriteAsync(sourceId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

    public async Task<WorkspaceStaffOnboardingMutationLease> AcquireExistingAsync(
        Guid applicationId,
        WorkspaceStaffOnboardingSourceLockMode mode,
        bool requireOperational,
        CancellationToken cancellationToken)
    {
        if (applicationId == Guid.Empty)
        {
            return WorkspaceStaffOnboardingMutationLease.Missing;
        }

        WorkspaceStaffOnboardingCoordinate? coordinate =
            await applications.FindCoordinateAsync(
                applicationId,
                cancellationToken).ConfigureAwait(false);
        if (coordinate is null)
        {
            return WorkspaceStaffOnboardingMutationLease.Missing;
        }

        await this.AcquireSourceAsync(
                coordinate.SourceId,
                mode,
                cancellationToken).ConfigureAwait(false);
        return await this.AcquireApplicationUnderSourceAsync(
                applicationId,
                requireOperational,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WorkspaceStaffOnboardingMutationLease> AcquireApplicantAsync(
        WorkspaceStaffOnboardingSource sourceKind,
        Guid sourceId,
        string subjectId,
        WorkspaceStaffOnboardingSourceLockMode mode,
        bool requireOperational,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceKind),
                sourceKind,
                "A Staff-onboarding mutation requires a known source kind.");
        }

        await this.AcquireSourceAsync(sourceId, mode, cancellationToken)
            .ConfigureAwait(false);
        await operationLock.AcquireApplicantAsync(
                sourceId,
                subjectId,
                cancellationToken).ConfigureAwait(false);
        Guid? applicationId = await applications.FindIdBySourceAndSubjectAsync(
                sourceKind,
                sourceId,
                subjectId,
                cancellationToken).ConfigureAwait(false);
        return applicationId.HasValue
            ? await this.AcquireApplicationUnderSourceAsync(
                    applicationId.Value,
                    requireOperational,
                    cancellationToken)
                .ConfigureAwait(false)
            : WorkspaceStaffOnboardingMutationLease.Missing;
    }

    public async Task<bool> AcquireTrackedAsync(
        WorkspaceStaffOnboarding application,
        WorkspaceStaffOnboardingSourceLockMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        await this.AcquireSourceAsync(
                application.SourceId,
                mode,
                cancellationToken).ConfigureAwait(false);
        return await this.AcquireTrackedUnderSourceAsync(
                application,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> AcquireTrackedUnderSourceAsync(
        WorkspaceStaffOnboarding application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (!await operationLock.TryAcquireAsync(
                application.Id,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await applications.ReloadAsync(application, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private async Task<WorkspaceStaffOnboardingMutationLease>
        AcquireApplicationUnderSourceAsync(
            Guid applicationId,
            bool requireOperational,
            CancellationToken cancellationToken)
    {
        if (!await operationLock.TryAcquireAsync(
                applicationId,
                cancellationToken).ConfigureAwait(false))
        {
            return WorkspaceStaffOnboardingMutationLease.Missing;
        }

        WorkspaceStaffOnboarding? application = requireOperational
            ? await applications.GetOperationalAsync(
                applicationId,
                cancellationToken).ConfigureAwait(false)
            : await applications.GetAsync(
                applicationId,
                cancellationToken).ConfigureAwait(false);
        return new WorkspaceStaffOnboardingMutationLease(
            CoordinateExists: true,
            application);
    }
}
