namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Logging;

internal sealed class WorkspaceStaffOnboardingProcessor(
    IStaffOnboardingProvisioner staff,
    WorkspaceAccessProvisioner access,
    ISystemClock clock,
    ILogger<WorkspaceStaffOnboardingProcessor> logger)
{
    public async Task<Result> ProcessAsync(
        WorkspaceStaffOnboarding application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (application.Status == WorkspaceStaffOnboardingState.Completed)
        {
            return Result.Success();
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result started = application.BeginProvisioning(nowUtc);
        if (started.IsFailure)
        {
            return started;
        }

        if (!application.StaffMemberId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(application.DisplayName))
            {
                return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
            }

            StaffOnboardingProvisioningResult provisioned = await staff.ProvisionAsync(
                new StaffOnboardingProvisioningRequest(
                    application.SubjectId,
                    application.DisplayName,
                    application.LegalName,
                    application.WorkEmail,
                    application.WorkPhone,
                    application.EmployeeNumber,
                    application.JobTitle,
                    application.Department,
                    "integration:organizations",
                    "Workspace Staff onboarding accepted."),
                cancellationToken).ConfigureAwait(false);
            if (!provisioned.IsSuccess || !provisioned.StaffMemberId.HasValue)
            {
                string failureCode = provisioned.ErrorCode ??
                    WorkspaceStaffOnboardingApplicationErrors.ProvisioningFailed.Code;
                application.Fail(failureCode, clock.UtcNow);
                logger.LogWarning(
                    "Staff onboarding {ApplicationId} could not provision Staff: {ErrorCode}.",
                    application.Id,
                    failureCode);
                return Result.Failure(WorkspaceStaffOnboardingApplicationErrors.ProvisioningFailed);
            }

            Result ready = application.MarkStaffReady(provisioned.StaffMemberId.Value, clock.UtcNow);
            if (ready.IsFailure)
            {
                return ready;
            }
        }
        else if (application.Status == WorkspaceStaffOnboardingState.Provisioning)
        {
            Result ready = application.MarkStaffReady(application.StaffMemberId.Value, clock.UtcNow);
            if (ready.IsFailure)
            {
                return ready;
            }
        }

        try
        {
            await access.ProvisionDefaultMemberAsync(
                    application.ScopeId,
                    application.SubjectId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            application.Fail("Workspaces.AccessProvisioningFailed", clock.UtcNow);
            logger.LogWarning(
                exception,
                "Staff onboarding {ApplicationId} could not provision workspace access.",
                application.Id);
            return Result.Failure(WorkspaceStaffOnboardingApplicationErrors.ProvisioningFailed);
        }

        return application.Complete(clock.UtcNow);
    }
}
