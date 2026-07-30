namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Authorization;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Models;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Application.Queries;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class
    GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQueryHandlerTests
{
    private const string TenantId =
        "10000000-0000-0000-0000-000000000001";
    private static readonly Guid ApplicationId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now =
        new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Approved_exact_target_is_returned_without_identity_fields()
    {
        RecordingExecutionGate gate = new();
        GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQueryHandler
            handler = CreateHandler(
                new WorkspaceStaffOnboardingDataRightsCorrectionTarget(
                    ApplicationId,
                    Version: 3,
                    WorkspaceStaffOnboardingState.Submitted,
                    "Ada Operator",
                    "Ada Lovelace",
                    "ada@example.test",
                    "+1 555 0100",
                    "EMP-100",
                    "Manager",
                    "Operations"),
                gate);
        GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQuery query =
            Query();

        Result<WorkspaceStaffOnboardingDataRightsCorrectionTargetDto> result =
            await handler.HandleAsync(query, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(ApplicationId, result.Value.ApplicationId);
        Assert.Equal(3, result.Value.Version);
        Assert.Equal("Ada Operator", result.Value.DisplayName);
        Assert.Equal("ada@example.test", result.Value.WorkEmail);
        Assert.DoesNotContain(
            result.Value.GetType().GetProperties(),
            property => string.Equals(
                property.Name,
                "SubjectId",
                StringComparison.Ordinal) ||
                string.Equals(
                    property.Name,
                    "VerifiedAccountEmail",
                    StringComparison.Ordinal));
        Assert.Equal(
            WorkspacesDataRightsCoordinates
                .StaffOnboardingCorrectionFieldPolicyKey,
            gate.Request!.FieldPolicyKey);
        Assert.Equal(query.ActorId, gate.Request.ExecutingActorId);
    }

    [Theory]
    [InlineData(WorkspaceStaffOnboardingState.PendingApproval, 3)]
    [InlineData(WorkspaceStaffOnboardingState.Submitted, 4)]
    public async Task Reviewed_or_stale_target_is_not_disclosed(
        WorkspaceStaffOnboardingState status,
        long version)
    {
        GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQueryHandler
            handler = CreateHandler(
                new WorkspaceStaffOnboardingDataRightsCorrectionTarget(
                    ApplicationId,
                    version,
                    status,
                    "Ada Operator",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                new RecordingExecutionGate());

        Result<WorkspaceStaffOnboardingDataRightsCorrectionTargetDto> result =
            await handler.HandleAsync(Query(), CancellationToken.None);

        Assert.Equal(
            WorkspaceStaffOnboardingApplicationErrors
                .CorrectionTargetUnavailable,
            result.Error);
    }

    [Fact]
    public async Task Denied_execution_does_not_read_target()
    {
        RecordingTargetReader targets = new(null);
        RecordingExecutionGate gate = new(allowed: false);
        GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQueryHandler
            handler = new(
                targets,
                new WorkspaceStaffOnboardingDataRightsCorrectionAuthorizer(
                    gate,
                    new TestScopeContext()));

        Result<WorkspaceStaffOnboardingDataRightsCorrectionTargetDto> result =
            await handler.HandleAsync(Query(), CancellationToken.None);

        Assert.Equal(
            WorkspaceStaffOnboardingApplicationErrors
                .DataRightsApprovalRequired,
            result.Error);
        Assert.Equal(0, targets.ReadCount);
    }

    private static
        GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQueryHandler
        CreateHandler(
            WorkspaceStaffOnboardingDataRightsCorrectionTarget target,
            RecordingExecutionGate gate) =>
        new(
            new RecordingTargetReader(target),
            new WorkspaceStaffOnboardingDataRightsCorrectionAuthorizer(
                gate,
                new TestScopeContext()));

    private static
        GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQuery Query() =>
        new(
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            ApprovalRevision: 5,
            ApplicationId,
            ExpectedVersion: 3,
            "user:privacy-owner");

    private sealed class RecordingTargetReader(
        WorkspaceStaffOnboardingDataRightsCorrectionTarget? target)
        : IWorkspaceStaffOnboardingDataRightsCorrectionTargetReader
    {
        public int ReadCount { get; private set; }

        public Task<WorkspaceStaffOnboardingDataRightsCorrectionTarget?>
            GetAsync(
                Guid applicationId,
                CancellationToken cancellationToken)
        {
            this.ReadCount++;
            return Task.FromResult(
                target?.ApplicationId == applicationId
                    ? target
                    : null);
        }
    }

    private sealed class RecordingExecutionGate(bool allowed = true)
        : IDataRightsCorrectionExecutionGate
    {
        public DataRightsCorrectionExecutionGateRequest? Request
        {
            get;
            private set;
        }

        public Task<DataRightsCorrectionExecutionGateResult> EvaluateAsync(
            DataRightsCorrectionExecutionGateRequest request,
            CancellationToken cancellationToken)
        {
            this.Request = request;
            return Task.FromResult(
                allowed
                    ? DataRightsCorrectionExecutionGateResult.Allowed(
                        Now.AddMinutes(5))
                    : DataRightsCorrectionExecutionGateResult.Denied(
                        DataRightsCorrectionExecutionDenial
                            .ExecutionNotFound));
        }
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => TenantId;
    }
}
