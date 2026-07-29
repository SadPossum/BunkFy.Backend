namespace BunkFy.Modules.Workspaces.Application.Contributors;

using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Ports;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class WorkspaceStaffOnboardingRetentionContributor
    : IRetentionExecutionContributor
{
    private readonly IRequestDispatcher dispatcher;
    private readonly IWorkspaceStaffOnboardingRetentionRepository candidates;
    private readonly WorkspaceStaffOnboardingRetentionOptions options;
    private readonly ISystemClock clock;
    private readonly ILogger<WorkspaceStaffOnboardingRetentionContributor> logger;

    public WorkspaceStaffOnboardingRetentionContributor(
        IRequestDispatcher dispatcher,
        IWorkspaceStaffOnboardingRetentionRepository candidates,
        IOptions<WorkspaceStaffOnboardingRetentionOptions> options,
        ISystemClock clock,
        ILogger<WorkspaceStaffOnboardingRetentionContributor> logger)
    {
        this.dispatcher = dispatcher;
        this.candidates = candidates;
        this.options = options.Value;
        this.clock = clock;
        this.logger = logger;
        this.Schedule = new(
            WorkspaceStaffOnboardingRetentionCoordinates.OwnerKey,
            WorkspaceStaffOnboardingRetentionCoordinates.DataClassKey,
            RetentionTargetScopeKind.Tenant,
            WorkspaceStaffOnboardingRetentionCoordinates.ExecutionPolicyVersion,
            this.options.Interval,
            maxAttempts: 3,
            executionTimeout: TimeSpan.FromMinutes(10));
    }

    public RetentionScheduleDescriptor Schedule { get; }

    public async Task<RetentionContributionResult> ExecuteAsync(
        RetentionContributionRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset nowUtc = this.clock.UtcNow;
        if (!IsExpectedRequest(request))
        {
            return Result(
                RetentionContributionStatus.Failed,
                0,
                0,
                0,
                WorkspaceStaffOnboardingRetentionCoordinates.CoordinateInvalidOutcome,
                nowUtc);
        }

        IReadOnlyList<WorkspaceStaffOnboardingRetentionCandidate> eligible =
            await this.candidates.ListEligibleAsync(
                request.TenantId,
                nowUtc - this.options.GracePeriod,
                checked(this.options.BatchSize + 1),
                cancellationToken).ConfigureAwait(false);
        WorkspaceStaffOnboardingRetentionCandidate[] batch =
            eligible.Take(this.options.BatchSize).ToArray();

        int affectedCount = 0;
        bool authorityLapsed = false;
        foreach (WorkspaceStaffOnboardingRetentionCandidate candidate in batch)
        {
            Result<WorkspaceStaffOnboardingRetentionReconciliation> reconciled =
                await this.dispatcher.SendAsync(
                    new ReconcileWorkspaceStaffOnboardingRetentionCandidateCommand(
                        candidate.ApplicationId,
                        candidate.ApplicationVersion),
                    cancellationToken).ConfigureAwait(false);
            if (reconciled.IsFailure)
            {
                this.logger.LogWarning(
                    "Workspace Staff onboarding staging reconciliation failed with {ErrorCode}.",
                    reconciled.Error.Code);
                return Result(
                    RetentionContributionStatus.Failed,
                    batch.Length,
                    affectedCount,
                    1,
                    WorkspaceStaffOnboardingRetentionCoordinates
                        .ReconciliationFailedOutcome,
                    this.clock.UtcNow);
            }

            affectedCount += reconciled.Value.Affected ? 1 : 0;
            authorityLapsed |= reconciled.Value.Outcome ==
                WorkspaceStaffOnboardingRetentionOutcome.AuthorityLapsed;
        }

        int remainingCount = eligible.Count > this.options.BatchSize ||
            authorityLapsed
                ? 1
                : 0;
        if (authorityLapsed)
        {
            return Result(
                RetentionContributionStatus.Failed,
                batch.Length,
                affectedCount,
                remainingCount,
                WorkspaceStaffOnboardingRetentionCoordinates.AuthorityLapsedOutcome,
                this.clock.UtcNow);
        }

        return Result(
            RetentionContributionStatus.Completed,
            batch.Length,
            affectedCount,
            remainingCount,
            remainingCount > 0
                ? WorkspaceStaffOnboardingRetentionCoordinates.BacklogOutcome
                : WorkspaceStaffOnboardingRetentionCoordinates.CompletedOutcome,
            this.clock.UtcNow);
    }

    private static bool IsExpectedRequest(RetentionContributionRequest request) =>
        request.ContractVersion == RetentionExecutionContract.CurrentVersion &&
        request.PropertyId is null &&
        request.ExecutionPolicyVersion ==
            WorkspaceStaffOnboardingRetentionCoordinates.ExecutionPolicyVersion &&
        string.Equals(
            request.OwnerKey,
            WorkspaceStaffOnboardingRetentionCoordinates.OwnerKey,
            StringComparison.Ordinal) &&
        string.Equals(
            request.DataClassKey,
            WorkspaceStaffOnboardingRetentionCoordinates.DataClassKey,
            StringComparison.Ordinal) &&
        Guid.TryParse(request.TenantId, out _);

    private static RetentionContributionResult Result(
        RetentionContributionStatus status,
        int scannedCount,
        int affectedCount,
        int remainingCount,
        string outcomeCode,
        DateTimeOffset completedAtUtc) =>
        new(
            RetentionExecutionContract.CurrentVersion,
            status,
            scannedCount,
            affectedCount,
            remainingCount,
            outcomeCode,
            completedAtUtc);
}
