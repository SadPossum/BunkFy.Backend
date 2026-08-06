namespace BunkFy.Modules.Workspaces.Application.Ports;

public interface IWorkspaceStaffOnboardingOperationLock
{
    Task AcquireSourceReadAsync(
        Guid sourceId,
        CancellationToken cancellationToken);

    Task AcquireSourceWriteAsync(
        Guid sourceId,
        CancellationToken cancellationToken);

    Task AcquireApplicantAsync(
        Guid sourceId,
        string subjectId,
        CancellationToken cancellationToken);

    Task<bool> TryAcquireAsync(
        Guid applicationId,
        CancellationToken cancellationToken);
}
