namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Modules.Organizations.Contracts;

public sealed record WorkspaceInvitationIssuanceRequest(
    Guid SourceId,
    string? RecipientEmail,
    int LifetimeHours,
    string ProfileKey,
    IReadOnlyCollection<Guid> PropertyIds,
    string ActorSubjectId);

public sealed record WorkspaceEnrollmentLinkIssuanceRequest(
    Guid SourceId,
    int LifetimeHours,
    int MaximumClaims,
    OrganizationEnrollmentApprovalMode ApprovalMode,
    string ProfileKey,
    IReadOnlyCollection<Guid> PropertyIds,
    string ActorSubjectId);

public interface IWorkspaceStaffJoinSourceIssuer
{
    Task<Result<WorkspaceStaffJoinSourceIssuanceDto>> IssueInvitationAsync(
        WorkspaceInvitationIssuanceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceStaffJoinSourceIssuanceDto>> IssueEnrollmentLinkAsync(
        WorkspaceEnrollmentLinkIssuanceRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class WorkspaceStaffJoinSourceIssuer(
    IRequestDispatcher dispatcher,
    IOrganizationJoinSourceIssuer organizations,
    IScopeContext scopeContext)
    : IWorkspaceStaffJoinSourceIssuer
{
    public async Task<Result<WorkspaceStaffJoinSourceIssuanceDto>> IssueInvitationAsync(
        WorkspaceInvitationIssuanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryOrganizationId(scopeContext.ScopeId, out Guid organizationId))
        {
            return Result.Failure<WorkspaceStaffJoinSourceIssuanceDto>(
                WorkspaceStaffOnboardingApplicationErrors.ScopeRequired);
        }

        Result<WorkspaceStaffAccessPlanDto> prepared = await this.PrepareAsync(
            request.SourceId,
            WorkspaceStaffOnboardingSourceKind.Invitation,
            request.ProfileKey,
            request.PropertyIds,
            request.ActorSubjectId,
            cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceIssuanceDto>(prepared.Error);
        }

        Result<WorkspaceStaffAccessPlanDto> activated = await this.ActivateAsync(
            prepared.Value,
            cancellationToken).ConfigureAwait(false);
        if (activated.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceIssuanceDto>(
                activated.Error);
        }

        OrganizationJoinSourceIssuance<OrganizationInvitationDto> issued =
            await organizations.IssueInvitationAsync(
                new OrganizationInvitationIssuanceRequest(
                    request.SourceId,
                    organizationId,
                    request.RecipientEmail,
                    request.LifetimeHours,
                    request.ActorSubjectId,
                    request.ActorSubjectId),
                cancellationToken).ConfigureAwait(false);
        return CompleteIssuance(activated.Value, issued);
    }

    public async Task<Result<WorkspaceStaffJoinSourceIssuanceDto>> IssueEnrollmentLinkAsync(
        WorkspaceEnrollmentLinkIssuanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryOrganizationId(scopeContext.ScopeId, out Guid organizationId))
        {
            return Result.Failure<WorkspaceStaffJoinSourceIssuanceDto>(
                WorkspaceStaffOnboardingApplicationErrors.ScopeRequired);
        }

        Result<WorkspaceStaffAccessPlanDto> prepared = await this.PrepareAsync(
            request.SourceId,
            WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
            request.ProfileKey,
            request.PropertyIds,
            request.ActorSubjectId,
            cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceIssuanceDto>(prepared.Error);
        }

        Result<WorkspaceStaffAccessPlanDto> activated = await this.ActivateAsync(
            prepared.Value,
            cancellationToken).ConfigureAwait(false);
        if (activated.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceIssuanceDto>(
                activated.Error);
        }

        OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto> issued =
            await organizations.IssueEnrollmentLinkAsync(
                new OrganizationEnrollmentLinkIssuanceRequest(
                    request.SourceId,
                    organizationId,
                    request.LifetimeHours,
                    request.MaximumClaims,
                    request.ApprovalMode,
                    request.ActorSubjectId,
                    request.ActorSubjectId),
                cancellationToken).ConfigureAwait(false);
        return CompleteIssuance(activated.Value, issued);
    }

    private Task<Result<WorkspaceStaffAccessPlanDto>> PrepareAsync(
        Guid sourceId,
        WorkspaceStaffOnboardingSourceKind sourceKind,
        string profileKey,
        IReadOnlyCollection<Guid> propertyIds,
        string actorSubjectId,
        CancellationToken cancellationToken) => dispatcher.SendAsync(
        new PrepareWorkspaceStaffAccessPlanCommand(
            sourceId,
            sourceKind,
            profileKey,
            propertyIds,
            actorSubjectId),
        cancellationToken);

    private Task<Result<WorkspaceStaffAccessPlanDto>> ActivateAsync(
        WorkspaceStaffAccessPlanDto prepared,
        CancellationToken cancellationToken)
    {
        if (prepared.Status == WorkspaceStaffAccessPlanStatus.Active)
        {
            return Task.FromResult(Result.Success(prepared));
        }

        return dispatcher.SendAsync(
            new ActivateWorkspaceStaffAccessPlanCommand(prepared.SourceId),
            cancellationToken);
    }

    private static Result<WorkspaceStaffJoinSourceIssuanceDto> CompleteIssuance<TSource>(
        WorkspaceStaffAccessPlanDto activated,
        OrganizationJoinSourceIssuance<TSource> issued)
        where TSource : class
    {
        if (!issued.IsSuccess)
        {
            return Result.Failure<WorkspaceStaffJoinSourceIssuanceDto>(
                WorkspaceStaffAccessPlanApplicationErrors.JoinSourceIssuanceFailed);
        }

        return Result.Success(new WorkspaceStaffJoinSourceIssuanceDto(
            activated,
            issued.Token,
            issued.Outcome == OrganizationJoinSourceIssuanceOutcome.AlreadyIssued));
    }

    private static bool TryOrganizationId(string? scopeId, out Guid organizationId) =>
        Guid.TryParse(scopeId, out organizationId) && organizationId != Guid.Empty;
}
