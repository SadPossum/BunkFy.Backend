namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Modules.Organizations.Contracts;

public interface IWorkspaceStaffJoinSourceManager
{
    Task<Result<WorkspaceStaffJoinSourceListResponse>> ListAsync(
        WorkspaceStaffOnboardingSourceKind sourceKind,
        int page,
        int pageSize,
        string actorSubjectId,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceStaffJoinSourceDto>> RevokeInvitationAsync(
        Guid sourceId,
        long expectedVersion,
        string actorSubjectId,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceStaffJoinSourceDto>> DisableEnrollmentLinkAsync(
        Guid sourceId,
        long expectedVersion,
        string actorSubjectId,
        CancellationToken cancellationToken = default);
}

internal sealed class WorkspaceStaffJoinSourceManager(
    IOrganizationJoinSourceManager organizations,
    IWorkspaceStaffAccessPlanRepository plans,
    IScopeContext scopeContext) : IWorkspaceStaffJoinSourceManager
{
    public async Task<Result<WorkspaceStaffJoinSourceListResponse>> ListAsync(
        WorkspaceStaffOnboardingSourceKind sourceKind,
        int page,
        int pageSize,
        string actorSubjectId,
        CancellationToken cancellationToken = default)
    {
        Result<(Guid OrganizationId, string ActorId)> context = this.Validate(
            actorSubjectId,
            sourceKind,
            sourceId: null,
            expectedVersion: null,
            page,
            pageSize);
        if (context.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceListResponse>(context.Error);
        }

        OrganizationJoinSourceListRequest request = new(
            context.Value.OrganizationId,
            context.Value.ActorId,
            page,
            pageSize);
        if (sourceKind == WorkspaceStaffOnboardingSourceKind.Invitation)
        {
            OrganizationJoinSourceOperation<OrganizationInvitationListResponse> listed =
                await organizations.ListInvitationsAsync(request, cancellationToken)
                    .ConfigureAwait(false);
            if (!listed.IsSuccess)
            {
                return Failure<WorkspaceStaffJoinSourceListResponse>();
            }

            IReadOnlyDictionary<Guid, WorkspaceStaffAccessPlan> accessPlans =
                await plans.GetManyAsync(
                    listed.Value!.Items.Select(item => item.InvitationId).ToArray(),
                    cancellationToken).ConfigureAwait(false);
            return Result.Success(new WorkspaceStaffJoinSourceListResponse(
                listed.Value.Items.Select(item => ToProduct(
                    item,
                    accessPlans.GetValueOrDefault(item.InvitationId))).ToArray(),
                listed.Value.Page,
                listed.Value.PageSize));
        }

        OrganizationJoinSourceOperation<OrganizationEnrollmentLinkListResponse> links =
            await organizations.ListEnrollmentLinksAsync(request, cancellationToken)
                .ConfigureAwait(false);
        if (!links.IsSuccess)
        {
            return Failure<WorkspaceStaffJoinSourceListResponse>();
        }

        IReadOnlyDictionary<Guid, WorkspaceStaffAccessPlan> linkPlans = await plans.GetManyAsync(
            links.Value!.Items.Select(item => item.EnrollmentLinkId).ToArray(),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new WorkspaceStaffJoinSourceListResponse(
            links.Value.Items.Select(item => ToProduct(
                item,
                linkPlans.GetValueOrDefault(item.EnrollmentLinkId))).ToArray(),
            links.Value.Page,
            links.Value.PageSize));
    }

    public async Task<Result<WorkspaceStaffJoinSourceDto>> RevokeInvitationAsync(
        Guid sourceId,
        long expectedVersion,
        string actorSubjectId,
        CancellationToken cancellationToken = default)
    {
        Result<(Guid OrganizationId, string ActorId)> context = this.Validate(
            actorSubjectId,
            WorkspaceStaffOnboardingSourceKind.Invitation,
            sourceId,
            expectedVersion,
            page: null,
            pageSize: null);
        if (context.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceDto>(context.Error);
        }

        OrganizationJoinSourceOperation<OrganizationInvitationDto> operation =
            await organizations.RevokeInvitationAsync(
                new OrganizationInvitationRevocationRequest(
                    context.Value.OrganizationId,
                    sourceId,
                    expectedVersion,
                    context.Value.ActorId,
                    context.Value.ActorId),
                cancellationToken).ConfigureAwait(false);
        if (!operation.IsSuccess)
        {
            return Failure<WorkspaceStaffJoinSourceDto>();
        }

        WorkspaceStaffAccessPlan? plan = await plans.GetAsync(sourceId, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(ToProduct(operation.Value!, plan));
    }

    public async Task<Result<WorkspaceStaffJoinSourceDto>> DisableEnrollmentLinkAsync(
        Guid sourceId,
        long expectedVersion,
        string actorSubjectId,
        CancellationToken cancellationToken = default)
    {
        Result<(Guid OrganizationId, string ActorId)> context = this.Validate(
            actorSubjectId,
            WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
            sourceId,
            expectedVersion,
            page: null,
            pageSize: null);
        if (context.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceDto>(context.Error);
        }

        OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto> operation =
            await organizations.DisableEnrollmentLinkAsync(
                new OrganizationEnrollmentLinkDisableRequest(
                    context.Value.OrganizationId,
                    sourceId,
                    expectedVersion,
                    context.Value.ActorId,
                    context.Value.ActorId),
                cancellationToken).ConfigureAwait(false);
        if (!operation.IsSuccess)
        {
            return Failure<WorkspaceStaffJoinSourceDto>();
        }

        WorkspaceStaffAccessPlan? plan = await plans.GetAsync(sourceId, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(ToProduct(operation.Value!, plan));
    }

    private Result<(Guid OrganizationId, string ActorId)> Validate(
        string actorSubjectId,
        WorkspaceStaffOnboardingSourceKind sourceKind,
        Guid? sourceId,
        long? expectedVersion,
        int? page,
        int? pageSize)
    {
        string actor = actorSubjectId?.Trim() ?? string.Empty;
        if (!Guid.TryParse(scopeContext.ScopeId, out Guid organizationId) ||
            organizationId == Guid.Empty || actor.Length == 0 ||
            sourceKind is not (WorkspaceStaffOnboardingSourceKind.Invitation or
                WorkspaceStaffOnboardingSourceKind.EnrollmentLink) ||
            (sourceId.HasValue && sourceId.Value == Guid.Empty) ||
            (expectedVersion.HasValue && expectedVersion.Value <= 0) ||
            (page.HasValue && page.Value <= 0) ||
            (pageSize.HasValue && pageSize.Value is < 1 or > 100))
        {
            return Result.Failure<(Guid, string)>(
                WorkspaceAccessManagementErrors.JoinSourceRequestInvalid);
        }

        return Result.Success((organizationId, actor));
    }

    private static WorkspaceStaffJoinSourceDto ToProduct(
        OrganizationInvitationDto source,
        WorkspaceStaffAccessPlan? plan) => new(
            source.InvitationId,
            WorkspaceStaffOnboardingSourceKind.Invitation,
            source.RecipientEmail,
            source.ExpiresAtUtc,
            source.Status switch
            {
                OrganizationInvitationStatus.Pending => WorkspaceStaffJoinSourceStatus.Active,
                OrganizationInvitationStatus.Accepted => WorkspaceStaffJoinSourceStatus.Accepted,
                OrganizationInvitationStatus.Revoked => WorkspaceStaffJoinSourceStatus.Revoked,
                OrganizationInvitationStatus.Superseded => WorkspaceStaffJoinSourceStatus.Superseded,
                OrganizationInvitationStatus.Expired => WorkspaceStaffJoinSourceStatus.Expired,
                _ => WorkspaceStaffJoinSourceStatus.Unknown
            },
            source.Version,
            MaximumClaims: null,
            ReservedClaims: null,
            ApprovalMode: null,
            plan?.ToDto(),
            source.CreatedAtUtc,
            source.LastChangedAtUtc);

    private static WorkspaceStaffJoinSourceDto ToProduct(
        OrganizationEnrollmentLinkDto source,
        WorkspaceStaffAccessPlan? plan) => new(
            source.EnrollmentLinkId,
            WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
            RecipientEmail: null,
            source.ExpiresAtUtc,
            source.Status switch
            {
                OrganizationEnrollmentLinkStatus.Active => WorkspaceStaffJoinSourceStatus.Active,
                OrganizationEnrollmentLinkStatus.Disabled => WorkspaceStaffJoinSourceStatus.Disabled,
                OrganizationEnrollmentLinkStatus.Rotated => WorkspaceStaffJoinSourceStatus.Superseded,
                OrganizationEnrollmentLinkStatus.Expired => WorkspaceStaffJoinSourceStatus.Expired,
                OrganizationEnrollmentLinkStatus.CapacityReached =>
                    WorkspaceStaffJoinSourceStatus.CapacityReached,
                _ => WorkspaceStaffJoinSourceStatus.Unknown
            },
            source.Version,
            source.MaximumClaims,
            source.ReservedClaims,
            source.ApprovalMode.ToString(),
            plan?.ToDto(),
            source.CreatedAtUtc,
            source.LastChangedAtUtc);

    private static Result<T> Failure<T>() => Result.Failure<T>(
        WorkspaceAccessManagementErrors.JoinSourceManagementFailed);
}
