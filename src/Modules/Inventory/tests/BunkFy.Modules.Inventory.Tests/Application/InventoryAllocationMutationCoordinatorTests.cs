namespace BunkFy.Modules.Inventory.Tests;

using System.Reflection;
using BunkFy.Modules.Inventory.Application.Handlers;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryAllocationMutationCoordinatorTests
{
    [Fact]
    public async Task Existing_allocation_locks_before_authoritative_reload()
    {
        InventoryAllocation allocation = CreateAllocation();
        List<string> calls = [];
        bool visible = true;
        InventoryAllocationMutationCoordinator coordinator = new(
            new CallbackOperationLock(
                calls,
                acquired: () => visible = false),
            new TestScopeContext());

        InventoryAllocation? result = await coordinator.AcquireExistingAsync(
            allocation.Id,
            _ =>
            {
                calls.Add("reload");
                return Task.FromResult<InventoryAllocation?>(
                    visible ? allocation : null);
            },
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(["lock", "reload"], calls);
    }

    [Fact]
    public async Task Missing_allocation_lock_skips_reload()
    {
        List<string> calls = [];
        InventoryAllocationMutationCoordinator coordinator = new(
            new CallbackOperationLock(calls, acquired: null, acquiredExisting: false),
            new TestScopeContext());

        InventoryAllocation? result = await coordinator.AcquireExistingAsync(
            Guid.NewGuid(),
            _ =>
            {
                calls.Add("reload");
                return Task.FromResult<InventoryAllocation?>(null);
            },
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(["lock"], calls);
    }

    [Theory]
    [InlineData(typeof(InventoryAllocationRequestedHandler))]
    [InlineData(typeof(InventoryAllocationAmendmentRequestedHandler))]
    [InlineData(typeof(InventoryAllocationReleaseRequestedHandler))]
    [InlineData(typeof(ApplyInventoryAllocationAnonymisationCommandHandler))]
    [InlineData(typeof(RestoreInventoryAllocationAnonymisationCommandHandler))]
    public void Existing_allocation_paths_require_mutation_coordinator(
        Type handlerType)
    {
        bool hasCoordinator = handlerType
            .GetConstructors(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType ==
                typeof(InventoryAllocationMutationCoordinator));

        Assert.True(
            hasCoordinator,
            $"{handlerType.Name} must serialize through " +
            $"{nameof(InventoryAllocationMutationCoordinator)}.");
    }

    [Fact]
    public async Task Restore_uses_the_explicit_coordinate_path()
    {
        List<string> calls = [];
        InventoryAllocationMutationCoordinator coordinator = new(
            new CallbackOperationLock(calls),
            new TestScopeContext());

        bool acquired = await coordinator.AcquireRestoreCoordinateAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(acquired);
        Assert.Equal(["coordinate-lock"], calls);
    }

    private static InventoryAllocation CreateAllocation() =>
        InventoryAllocation.CreateAccepted(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 12),
            [Guid.NewGuid()],
            new DateTimeOffset(2026, 8, 6, 7, 0, 0, TimeSpan.Zero)).Value;

    private sealed class CallbackOperationLock(
        List<string> calls,
        Action? acquired = null,
        bool acquiredExisting = true)
        : IInventoryAllocationOperationLock
    {
        public Task<bool> TryAcquireExistingAsync(
            string tenantId,
            Guid allocationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            calls.Add("lock");
            acquired?.Invoke();
            return Task.FromResult(acquiredExisting);
        }

        public Task AcquireCoordinateAsync(
            string tenantId,
            Guid allocationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            calls.Add("coordinate-lock");
            return Task.CompletedTask;
        }
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
