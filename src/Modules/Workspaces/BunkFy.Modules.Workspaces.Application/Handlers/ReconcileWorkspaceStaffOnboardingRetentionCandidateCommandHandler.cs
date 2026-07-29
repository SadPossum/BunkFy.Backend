namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Options;

internal sealed class ReconcileWorkspaceStaffOnboardingRetentionCandidateCommandHandler(
    IWorkspaceStaffOnboardingRepository applications,
    IWorkspaceStaffAccessPlanRepository plans,
    IOrganizationEnrollmentClaimInspector claims,
    WorkspaceStaffOnboardingProcessor processor,
    IOptions<WorkspaceStaffOnboardingRetentionOptions> options,
    ISystemClock clock)
    : ICommandHandler<
        ReconcileWorkspaceStaffOnboardingRetentionCandidateCommand,
        WorkspaceStaffOnboardingRetentionReconciliation>
{
    public async Task<Result<WorkspaceStaffOnboardingRetentionReconciliation>> HandleAsync(
        ReconcileWorkspaceStaffOnboardingRetentionCandidateCommand command,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffOnboarding? application = await applications.GetAsync(
            command.ApplicationId,
            cancellationToken).ConfigureAwait(false);
        if (application is null ||
            application.Version != command.ExpectedVersion ||
            application.SourceKind != WorkspaceStaffOnboardingSource.EnrollmentLink ||
            application.Status != WorkspaceStaffOnboardingState.Submitted ||
            application.ClaimId.HasValue ||
            application.ClaimVersion.HasValue)
        {
            return Unchanged();
        }

        WorkspaceStaffAccessPlan? plan = await plans.GetAsync(
            application.SourceId,
            cancellationToken).ConfigureAwait(false);
        if (plan is null ||
            plan.SourceKind != WorkspaceStaffOnboardingSource.EnrollmentLink ||
            !string.Equals(
                plan.ScopeId,
                application.ScopeId,
                StringComparison.Ordinal))
        {
            return Result.Failure<WorkspaceStaffOnboardingRetentionReconciliation>(
                WorkspaceStaffOnboardingApplicationErrors.RetentionPlanInconsistent);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        DateTimeOffset? sourceExpiredAtUtc = plan.SourceExpiredAtUtc;
        WorkspaceStaffOnboardingRetentionOptions settings = options.Value;
        if (!sourceExpiredAtUtc.HasValue ||
            sourceExpiredAtUtc.Value > nowUtc - settings.GracePeriod)
        {
            return Unchanged();
        }

        if (!Guid.TryParse(application.ScopeId, out Guid organizationId))
        {
            return Result.Failure<WorkspaceStaffOnboardingRetentionReconciliation>(
                WorkspaceStaffOnboardingApplicationErrors.RetentionCoordinateInvalid);
        }

        OrganizationEnrollmentClaimDto? claim = await claims.FindAsync(
            organizationId,
            application.SourceId,
            application.SubjectId,
            cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            if (sourceExpiredAtUtc.Value <= nowUtc - settings.AuthorityWindow)
            {
                return Success(
                    WorkspaceStaffOnboardingRetentionOutcome.AuthorityLapsed,
                    affected: false);
            }

            Result expired = application.Expire(nowUtc);
            if (expired.IsFailure)
            {
                return Failure(expired);
            }

            await this.FinalizePlanAsync(application.SourceId, nowUtc, cancellationToken)
                .ConfigureAwait(false);
            return Success(
                WorkspaceStaffOnboardingRetentionOutcome.Expired,
                affected: true);
        }

        if (!IsConsistent(claim, application, organizationId))
        {
            return Result.Failure<WorkspaceStaffOnboardingRetentionReconciliation>(
                WorkspaceStaffOnboardingApplicationErrors.RetentionClaimInconsistent);
        }

        if (plan.Status != WorkspaceStaffAccessPlanState.Active)
        {
            return Result.Failure<WorkspaceStaffOnboardingRetentionReconciliation>(
                WorkspaceStaffOnboardingApplicationErrors.RetentionPlanInconsistent);
        }

        return claim.Status switch
        {
            OrganizationEnrollmentClaimStatus.Pending =>
                ObservePending(application, claim, nowUtc),
            OrganizationEnrollmentClaimStatus.Rejected =>
                await this.ObserveRejectedAsync(
                    application, claim, nowUtc, cancellationToken).ConfigureAwait(false),
            OrganizationEnrollmentClaimStatus.Expired =>
                await this.ObserveExpiredAsync(
                    application, claim, nowUtc, cancellationToken).ConfigureAwait(false),
            OrganizationEnrollmentClaimStatus.Accepted =>
                await this.ObserveAcceptedAsync(
                    application, claim, nowUtc, cancellationToken).ConfigureAwait(false),
            _ => Result.Failure<WorkspaceStaffOnboardingRetentionReconciliation>(
                WorkspaceStaffOnboardingApplicationErrors.RetentionClaimInconsistent)
        };
    }

    private static Result<WorkspaceStaffOnboardingRetentionReconciliation> ObservePending(
        WorkspaceStaffOnboarding application,
        OrganizationEnrollmentClaimDto claim,
        DateTimeOffset nowUtc)
    {
        Result observed = application.ObserveClaimRequested(
            claim.ClaimId,
            claim.Version,
            nowUtc);
        return observed.IsFailure
            ? Failure(observed)
            : Success(
                WorkspaceStaffOnboardingRetentionOutcome.ClaimPending,
                affected: true);
    }

    private async Task<Result<WorkspaceStaffOnboardingRetentionReconciliation>> ObserveRejectedAsync(
        WorkspaceStaffOnboarding application,
        OrganizationEnrollmentClaimDto claim,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Result observed = application.ObserveClaimRejected(
            claim.ClaimId,
            claim.Version,
            nowUtc);
        if (observed.IsFailure)
        {
            return Failure(observed);
        }

        await this.FinalizePlanAsync(application.SourceId, nowUtc, cancellationToken)
            .ConfigureAwait(false);
        return Success(
            WorkspaceStaffOnboardingRetentionOutcome.ClaimRejected,
            affected: true);
    }

    private async Task<Result<WorkspaceStaffOnboardingRetentionReconciliation>> ObserveExpiredAsync(
        WorkspaceStaffOnboarding application,
        OrganizationEnrollmentClaimDto claim,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Result observed = application.ObserveClaimExpired(
            claim.ClaimId,
            claim.Version,
            nowUtc);
        if (observed.IsFailure)
        {
            return Failure(observed);
        }

        await this.FinalizePlanAsync(application.SourceId, nowUtc, cancellationToken)
            .ConfigureAwait(false);
        return Success(
            WorkspaceStaffOnboardingRetentionOutcome.ClaimExpired,
            affected: true);
    }

    private async Task<Result<WorkspaceStaffOnboardingRetentionReconciliation>> ObserveAcceptedAsync(
        WorkspaceStaffOnboarding application,
        OrganizationEnrollmentClaimDto claim,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Result observed = application.ObserveClaimAccepted(
            claim.ClaimId,
            claim.Version,
            nowUtc);
        if (observed.IsFailure)
        {
            return Failure(observed);
        }

        Result processed = await processor.ProcessAsync(application, cancellationToken)
            .ConfigureAwait(false);
        await this.FinalizePlanAsync(application.SourceId, nowUtc, cancellationToken)
            .ConfigureAwait(false);
        return Success(
            processed.IsSuccess
                ? WorkspaceStaffOnboardingRetentionOutcome.ClaimAccepted
                : WorkspaceStaffOnboardingRetentionOutcome
                    .ClaimAcceptedRecoveryRequired,
            affected: true);
    }

    private Task FinalizePlanAsync(
        Guid enrollmentLinkId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        OrganizationEnrollmentClaimExpiredStaffOnboardingHandler
            .ExpirePlanWhenUnusedAsync(
                applications,
                plans,
                enrollmentLinkId,
                nowUtc,
                cancellationToken);

    private static bool IsConsistent(
        OrganizationEnrollmentClaimDto claim,
        WorkspaceStaffOnboarding application,
        Guid organizationId) =>
        claim.ClaimId != Guid.Empty &&
        claim.Version > 0 &&
        claim.OrganizationId == organizationId &&
        claim.EnrollmentLinkId == application.SourceId &&
        string.Equals(
            claim.SubjectId,
            application.SubjectId,
            StringComparison.Ordinal);

    private static Result<WorkspaceStaffOnboardingRetentionReconciliation> Unchanged() =>
        Success(WorkspaceStaffOnboardingRetentionOutcome.Unchanged, affected: false);

    private static Result<WorkspaceStaffOnboardingRetentionReconciliation> Success(
        WorkspaceStaffOnboardingRetentionOutcome outcome,
        bool affected) =>
        Result.Success(new WorkspaceStaffOnboardingRetentionReconciliation(
            outcome,
            affected));

    private static Result<WorkspaceStaffOnboardingRetentionReconciliation> Failure(
        Result result) =>
        Result.Failure<WorkspaceStaffOnboardingRetentionReconciliation>(result.Error);
}
