namespace BunkFy.Modules.Retention.Tests.Application;

using System.Reflection;
using BunkFy.Modules.Retention.Application.Commands;
using BunkFy.Modules.Retention.Application.Handlers;
using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Retention.Domain.Aggregates;
using BunkFy.Modules.Retention.Domain.Models;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RetentionMutationCoordinatorTests
{
    private const string TenantId = "tenant-a";
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Start_locks_target_execution_and_schedule_before_reload()
    {
        Guid propertyId = Guid.Parse(
            "10000000-0000-0000-0000-000000000001");
        Guid executionId = Guid.Parse(
            "20000000-0000-0000-0000-000000000001");
        List<string> calls = [];
        RetentionExecution execution = CreateExecution(executionId, propertyId);
        RetentionExecutionMutationCoordinator coordinator = new(
            new RecordingMutationLock(calls),
            new RecordingExecutionRepository(execution, calls),
            new RecordingScopeRepository(calls),
            new TestScopeContext());

        RetentionExecutionStartLease lease = await coordinator.AcquireStartAsync(
            CreateStartCommand(executionId, propertyId),
            CancellationToken.None);

        Assert.True(lease.TargetAvailable);
        Assert.Same(execution, lease.Execution);
        Assert.Equal(
            [
                "tenant-read",
                $"property-read:{propertyId:N}",
                $"execution:{executionId:N}",
                $"schedule:guests:guest-operational:{propertyId:N}:1",
                "target-read",
                $"execution-read:{executionId:N}"
            ],
            calls);
    }

    [Fact]
    public async Task Completion_locks_execution_then_authoritative_schedule()
    {
        Guid propertyId = Guid.Parse(
            "10000000-0000-0000-0000-000000000002");
        Guid executionId = Guid.Parse(
            "20000000-0000-0000-0000-000000000002");
        List<string> calls = [];
        RetentionExecution execution = CreateExecution(executionId, propertyId);
        RetentionExecutionMutationCoordinator coordinator = new(
            new RecordingMutationLock(calls),
            new RecordingExecutionRepository(execution, calls),
            new RecordingScopeRepository(calls),
            new TestScopeContext());

        RetentionExecution? result = await coordinator.AcquireCompletionAsync(
            executionId,
            CancellationToken.None);

        Assert.Same(execution, result);
        Assert.Equal(
            [
                $"execution:{executionId:N}",
                $"execution-read:{executionId:N}",
                $"schedule:guests:guest-operational:{propertyId:N}:1"
            ],
            calls);
    }

    [Fact]
    public async Task Property_projection_locks_before_merge()
    {
        Guid propertyId = Guid.Parse(
            "10000000-0000-0000-0000-000000000003");
        List<string> calls = [];
        RetentionScopeMutationCoordinator coordinator = new(
            new RecordingMutationLock(calls),
            new RecordingScopeRepository(calls),
            new TestScopeContext());

        await coordinator.ApplyPropertyPolicyAsync(
            new(TenantId, propertyId, true, 2, 3),
            CancellationToken.None);

        Assert.Equal(
            [
                $"property-write:{propertyId:N}",
                $"property-policy:{propertyId:N}:3"
            ],
            calls);
    }

    [Fact]
    public async Task Cross_scope_start_fails_before_locking()
    {
        List<string> calls = [];
        RetentionExecutionMutationCoordinator coordinator = new(
            new RecordingMutationLock(calls),
            new RecordingExecutionRepository(null, calls),
            new RecordingScopeRepository(calls),
            new TestScopeContext());
        BeginRetentionExecutionCommand command = CreateStartCommand(
            Guid.NewGuid(),
            Guid.NewGuid()) with
        {
            TenantId = "tenant-b"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.AcquireStartAsync(command, CancellationToken.None));

        Assert.Empty(calls);
    }

    [Fact]
    public void All_online_writers_require_their_mutation_coordinator()
    {
        AssertCoordinator<RetentionExecutionMutationCoordinator>(
            typeof(BeginRetentionExecutionCommandHandler),
            typeof(CompleteRetentionExecutionCommandHandler));
        AssertCoordinator<RetentionScopeMutationCoordinator>(
            typeof(RetentionOrganizationChangedHandler),
            typeof(RetentionPropertyCreatedHandler),
            typeof(RetentionPropertyUpdatedHandler),
            typeof(RetentionPropertyRetiredHandler),
            typeof(RetentionPropertyProcessingPolicyActivatedHandler),
            typeof(RetentionPropertyProcessingSuspendedHandler));
    }

    private static void AssertCoordinator<TCoordinator>(params Type[] writerTypes)
    {
        foreach (Type writerType in writerTypes)
        {
            bool found = writerType
                .GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(TCoordinator));
            Assert.True(
                found,
                $"{writerType.Name} must serialize through " +
                $"{typeof(TCoordinator).Name}.");
        }
    }

    private static BeginRetentionExecutionCommand CreateStartCommand(
        Guid executionId,
        Guid propertyId) => new(
        executionId,
        TenantId,
        "guests",
        "guest-operational",
        RetentionTargetScopeKind.Property,
        propertyId,
        1,
        1,
        Now,
        Now.AddMinutes(5),
        Now.AddHours(1));

    private static RetentionExecution CreateExecution(
        Guid executionId,
        Guid propertyId) => RetentionExecution.Start(
        executionId,
        TenantId,
        "guests",
        "guest-operational",
        RetentionExecutionTargetKind.Property,
        propertyId,
        1,
        1,
        Now,
        Now.AddMinutes(5)).Value;

    private sealed class RecordingMutationLock(List<string> calls)
        : IRetentionMutationLock
    {
        public Task AcquireTenantTargetReadAsync(
            string tenantId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(TenantId, tenantId);
            calls.Add("tenant-read");
            return Task.CompletedTask;
        }

        public Task AcquireTenantTargetWriteAsync(
            string tenantId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(TenantId, tenantId);
            calls.Add("tenant-write");
            return Task.CompletedTask;
        }

        public Task AcquirePropertyTargetReadAsync(
            string tenantId,
            Guid propertyId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(TenantId, tenantId);
            calls.Add($"property-read:{propertyId:N}");
            return Task.CompletedTask;
        }

        public Task AcquirePropertyTargetWriteAsync(
            string tenantId,
            Guid propertyId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(TenantId, tenantId);
            calls.Add($"property-write:{propertyId:N}");
            return Task.CompletedTask;
        }

        public Task AcquireExecutionAsync(
            string tenantId,
            Guid executionId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(TenantId, tenantId);
            calls.Add($"execution:{executionId:N}");
            return Task.CompletedTask;
        }

        public Task AcquireScheduleAsync(
            string tenantId,
            string ownerKey,
            string dataClassKey,
            Guid? propertyId,
            int executionPolicyVersion,
            CancellationToken cancellationToken)
        {
            Assert.Equal(TenantId, tenantId);
            calls.Add(
                $"schedule:{ownerKey}:{dataClassKey}:" +
                $"{propertyId:N}:{executionPolicyVersion}");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingExecutionRepository(
        RetentionExecution? execution,
        List<string> calls)
        : IRetentionExecutionRepository
    {
        public Task AddAsync(
            RetentionExecution value,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<RetentionExecution?> GetAsync(
            Guid executionId,
            CancellationToken cancellationToken)
        {
            calls.Add($"execution-read:{executionId:N}");
            return Task.FromResult(
                execution?.Id == executionId ? execution : null);
        }
    }

    private sealed class RecordingScopeRepository(List<string> calls)
        : IRetentionScopeRepository
    {
        public Task ApplyOrganizationAsync(
            RetentionOrganizationWriteModel organization,
            CancellationToken cancellationToken)
        {
            calls.Add($"organization:{organization.SourceVersion}");
            return Task.CompletedTask;
        }

        public Task ApplyPropertyTopologyAsync(
            RetentionPropertyTopologyWriteModel property,
            CancellationToken cancellationToken)
        {
            calls.Add($"property-topology:{property.PropertyId:N}:" +
                property.SourceVersion);
            return Task.CompletedTask;
        }

        public Task ApplyPropertyPolicyAsync(
            RetentionPropertyPolicyWriteModel property,
            CancellationToken cancellationToken)
        {
            calls.Add($"property-policy:{property.PropertyId:N}:" +
                property.SourceVersion);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RetentionScheduleTarget>> ListActiveTargetsAsync(
            RetentionTargetScopeKind targetKind,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetentionScheduleTarget>>([]);

        public Task<IReadOnlyList<RetentionScheduleTarget>>
            ListCurrentActiveTargetsAsync(
                RetentionTargetScopeKind targetKind,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetentionScheduleTarget>>([]);

        public Task<bool> IsActiveTargetAsync(
            RetentionTargetScopeKind targetKind,
            Guid? propertyId,
            CancellationToken cancellationToken)
        {
            calls.Add("target-read");
            return Task.FromResult(true);
        }
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }
}
