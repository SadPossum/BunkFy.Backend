namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Logging;

internal sealed class WorkspaceStaffOnboardingProcessor(
    IStaffOnboardingProvisioner staff,
    IStaffPropertyAssignmentProvisioner staffProperties,
    IWorkspaceStaffAccessPlanRepository plans,
    WorkspaceStaffAccessPlanPolicy planPolicy,
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

        WorkspaceStaffAccessPlan? plan = await plans.GetAsync(
            application.SourceId,
            cancellationToken).ConfigureAwait(false);
        if (plan is null || plan.SourceKind != application.SourceKind ||
            plan.Status != WorkspaceStaffAccessPlanState.Active)
        {
            application.Fail(
                WorkspaceStaffOnboardingApplicationErrors.AccessPlanUnavailable.Code,
                clock.UtcNow);
            return Result.Failure(
                WorkspaceStaffOnboardingApplicationErrors.AccessPlanUnavailable);
        }

        Guid[] propertyIds = plan.Properties
            .Select(property => property.PropertyId)
            .Order()
            .ToArray();
        Result<Gma.Modules.AccessControl.Contracts.AccessProfileDto> validated =
            await planPolicy.ValidateAsync(
                application.ScopeId,
                plan.SourceKind,
                plan.ProfileKey,
                propertyIds,
                plan.CreatedBySubjectId,
                cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure || validated.Value.Id != plan.ProfileId)
        {
            string failureCode = validated.IsFailure
                ? validated.Error.Code
                : WorkspaceStaffAccessPlanApplicationErrors.ProfileUnavailable.Code;
            application.Fail(failureCode, clock.UtcNow);
            return Result.Failure(
                WorkspaceStaffOnboardingApplicationErrors.AccessPlanUnavailable);
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
                    "Staff onboarding could not provision Staff: {ErrorCode}.",
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

        StaffPropertyAssignmentProvisioningResult assignments =
            await staffProperties.ReconcileAsync(
                new StaffPropertyAssignmentProvisioningRequest(
                    application.StaffMemberId!.Value,
                    propertyIds,
                    "integration:workspaces",
                    "Workspace Staff access plan applied."),
                cancellationToken).ConfigureAwait(false);
        if (!assignments.IsSuccess)
        {
            string failureCode = assignments.ErrorCode ??
                WorkspaceStaffOnboardingApplicationErrors.ProvisioningFailed.Code;
            application.Fail(failureCode, clock.UtcNow);
            logger.LogWarning(
                "Staff onboarding could not reconcile properties: {ErrorCode}.",
                failureCode);
            return Result.Failure(WorkspaceStaffOnboardingApplicationErrors.ProvisioningFailed);
        }

        try
        {
            await access.ProvisionMemberAsync(
                    application.ScopeId,
                    application.SubjectId,
                    plan.ProfileId,
                    propertyIds,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            application.Fail("Workspaces.AccessProvisioningFailed", clock.UtcNow);
            logger.LogWarning(
                "Staff onboarding could not provision workspace access because {ExceptionType} was raised.",
                exception.GetType().Name);
            return Result.Failure(WorkspaceStaffOnboardingApplicationErrors.ProvisioningFailed);
        }

        return application.Complete(clock.UtcNow);
    }
}
