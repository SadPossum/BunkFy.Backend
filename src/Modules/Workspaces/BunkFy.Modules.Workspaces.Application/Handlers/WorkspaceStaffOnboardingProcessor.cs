namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Logging;

internal sealed class WorkspaceStaffOnboardingProcessor(
    IStaffOnboardingProvisioner staff,
    IStaffPropertyAssignmentProvisioner staffProperties,
    IWorkspaceStaffOnboardingRepository applications,
    IWorkspaceStaffOnboardingProcessingRestrictionProjectionRepository
        restrictionProjections,
    IWorkspaceStaffOnboardingOperationLock operationLock,
    IWorkspaceStaffAccessPlanRepository plans,
    WorkspaceStaffAccessPlanPolicy planPolicy,
    WorkspaceAccessProvisioner access,
    WorkspaceOperationalAdmissionEvaluator operationalAdmission,
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

        if (!await operationLock.TryAcquireAsync(
                application.Id,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(
                WorkspaceStaffOnboardingApplicationErrors
                    .ApplicationNotFound);
        }

        await applications.ReloadAsync(
            application,
            cancellationToken).ConfigureAwait(false);
        if (application.Status == WorkspaceStaffOnboardingState.Completed)
        {
            return Result.Success();
        }

        Result admitted = await this.RequireOperationalAsync(
            application.ScopeId,
            cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return admitted;
        }

        WorkspaceStaffOnboardingProcessingRestrictionProjection? restriction =
            await restrictionProjections.GetAsync(
                application.Id,
                cancellationToken).ConfigureAwait(false);
        if (restriction is null ||
            restriction.ContractVersion !=
                WorkspaceStaffOnboardingProcessingRestrictionContract
                    .CurrentVersion)
        {
            return Result.Failure(
                WorkspaceStaffOnboardingApplicationErrors
                    .RestrictionProjectionUnavailable);
        }

        if (restriction.IsRestricted)
        {
            return Result.Failure(
                WorkspaceStaffOnboardingApplicationErrors
                    .ProcessingRestricted);
        }

        Result started = application.BeginProvisioning(clock.UtcNow);
        if (started.IsFailure)
        {
            return started;
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

        if (!application.StaffMemberId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(application.DisplayName))
            {
                return Result.Failure(WorkspaceStaffOnboardingErrors.StateConflict);
            }

            admitted = await this.RequireOperationalAsync(
                application.ScopeId,
                cancellationToken).ConfigureAwait(false);
            if (admitted.IsFailure)
            {
                return admitted;
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

        admitted = await this.RequireOperationalAsync(
            application.ScopeId,
            cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return admitted;
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
            admitted = await this.RequireOperationalAsync(
                application.ScopeId,
                cancellationToken).ConfigureAwait(false);
            if (admitted.IsFailure)
            {
                return admitted;
            }

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

    private async ValueTask<Result> RequireOperationalAsync(
        string tenantId,
        CancellationToken cancellationToken) =>
        WorkspaceOperationalAdmissionGuard.RequireAllowed(
            await operationalAdmission.EvaluateAsync(
                tenantId,
                cancellationToken).ConfigureAwait(false));
}
