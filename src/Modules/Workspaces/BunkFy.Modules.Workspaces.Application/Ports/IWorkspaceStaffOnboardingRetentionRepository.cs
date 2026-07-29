namespace BunkFy.Modules.Workspaces.Application.Ports;

public interface IWorkspaceStaffOnboardingRetentionRepository
{
    Task<IReadOnlyList<WorkspaceStaffOnboardingRetentionCandidate>> ListEligibleAsync(
        string tenantId,
        DateTimeOffset sourceExpiredBeforeUtc,
        int maximumCount,
        CancellationToken cancellationToken);
}

public sealed record WorkspaceStaffOnboardingRetentionCandidate(
    Guid ApplicationId,
    long ApplicationVersion,
    DateTimeOffset SourceExpiredAtUtc);
