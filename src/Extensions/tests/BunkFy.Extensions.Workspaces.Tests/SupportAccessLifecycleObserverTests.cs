namespace BunkFy.Extensions.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Observability;
using Gma.Modules.AccessControl.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class SupportAccessLifecycleObserverTests
{
    private static readonly Guid CorrelationId =
        Guid.Parse("81c4a139-4a06-438c-b04f-c1fc767f25ad");
    private static readonly AccessScope WorkspaceScope =
        WorkspaceAccessScopes.Create("workspace-a");

    [Theory]
    [InlineData(
        AccessRoleAssignmentLifecycleStage.Requested,
        "bunkfy.support-access.requested")]
    [InlineData(
        AccessRoleAssignmentLifecycleStage.Denied,
        "bunkfy.support-access.denied")]
    [InlineData(
        AccessRoleAssignmentLifecycleStage.Revoked,
        "bunkfy.support-access.revoked")]
    public async Task Admin_actor_lifecycle_fact_records_payload_free_signal(
        AccessRoleAssignmentLifecycleStage stage,
        string expectedCode)
    {
        RecordingSecuritySignalRecorder recorder = new();
        SupportAccessLifecycleObserver observer = new(recorder);

        await observer.ObserveAsync(
            CreateEvent(stage, AccessSubject.AdminActor("support-a"), AccessScope.Global));

        RecordedSignal signal = Assert.Single(recorder.Signals);
        Assert.Equal(expectedCode, signal.Definition.Code);
        Assert.Equal(CorrelationId, signal.CorrelationId);
    }

    [Fact]
    public async Task Finite_workspace_or_property_grant_records_granted_signal()
    {
        RecordingSecuritySignalRecorder recorder = new();
        SupportAccessLifecycleObserver observer = new(recorder);
        AccessRoleAssignmentLifecycleEvent lifecycleEvent = CreateEvent(
            AccessRoleAssignmentLifecycleStage.Granted,
            AccessSubject.AdminActor("support-a"),
            WorkspaceAccessScopes.CreateProperty("workspace-a", Guid.NewGuid()),
            new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero));

        await observer.ObserveAsync(lifecycleEvent);

        Assert.Equal(
            "bunkfy.support-access.granted",
            Assert.Single(recorder.Signals).Definition.Code);
    }

    [Theory]
    [InlineData(AccessRoleAssignmentLifecycleStage.Granted)]
    [InlineData(AccessRoleAssignmentLifecycleStage.Requested)]
    [InlineData(AccessRoleAssignmentLifecycleStage.Denied)]
    [InlineData(AccessRoleAssignmentLifecycleStage.Revoked)]
    public async Task Product_user_assignment_is_not_a_support_access_signal(
        AccessRoleAssignmentLifecycleStage stage)
    {
        RecordingSecuritySignalRecorder recorder = new();
        SupportAccessLifecycleObserver observer = new(recorder);

        await observer.ObserveAsync(
            CreateEvent(stage, AccessSubject.User("member-a"), WorkspaceScope));

        Assert.Empty(recorder.Signals);
    }

    [Fact]
    public async Task Unrelated_admin_actor_role_is_not_a_support_access_signal()
    {
        RecordingSecuritySignalRecorder recorder = new();
        SupportAccessLifecycleObserver observer = new(recorder);
        AccessRoleAssignmentLifecycleEvent lifecycleEvent = new(
            AccessRoleAssignmentLifecycleStage.Denied,
            AccessSubject.AdminActor("support-a"),
            "unrelated-role",
            WorkspaceScope,
            expiresAtUtc: null,
            CorrelationId);

        await observer.ObserveAsync(lifecycleEvent);

        Assert.Empty(recorder.Signals);
    }

    [Fact]
    public async Task Standing_or_global_grant_is_not_misreported_as_granted()
    {
        RecordingSecuritySignalRecorder recorder = new();
        SupportAccessLifecycleObserver observer = new(recorder);

        await observer.ObserveAsync(
            CreateEvent(
                AccessRoleAssignmentLifecycleStage.Granted,
                AccessSubject.AdminActor("support-a"),
                WorkspaceScope,
                expiresAtUtc: null));
        await observer.ObserveAsync(
            CreateEvent(
                AccessRoleAssignmentLifecycleStage.Granted,
                AccessSubject.AdminActor("support-a"),
                AccessScope.Global,
                new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero)));

        Assert.Empty(recorder.Signals);
    }

    [Fact]
    public void Registry_contains_only_observable_lifecycle_facts()
    {
        SupportAccessSecuritySignalDefinitions source = new();

        Assert.Equal(4, source.Definitions.Count);
        Assert.DoesNotContain(
            source.Definitions,
            definition => definition.Code == "bunkfy.support-access.expired");
    }

    private static AccessRoleAssignmentLifecycleEvent CreateEvent(
        AccessRoleAssignmentLifecycleStage stage,
        AccessSubject subject,
        AccessScope accessScope,
        DateTimeOffset? expiresAtUtc = null) =>
        new(
            stage,
            subject,
            WorkspaceAccessRoles.CompanySupport,
            accessScope,
            expiresAtUtc,
            CorrelationId);

    private sealed class RecordingSecuritySignalRecorder : ISecuritySignalRecorder
    {
        public List<RecordedSignal> Signals { get; } = [];

        public SecuritySignalReceipt Record(
            SecuritySignalDefinition definition,
            Guid? correlationId = null)
        {
            this.Signals.Add(new RecordedSignal(definition, correlationId));
            return new(
                SecuritySignalCorrelation.Create(correlationId),
                WasEmitted: true);
        }
    }

    private sealed record RecordedSignal(
        SecuritySignalDefinition Definition,
        Guid? CorrelationId);
}
