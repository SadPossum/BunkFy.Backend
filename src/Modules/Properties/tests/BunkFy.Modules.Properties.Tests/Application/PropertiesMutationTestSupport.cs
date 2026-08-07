namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application.Handlers;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Domain.Aggregates;
using Gma.Framework.Scoping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

internal static class PropertiesMutationTestSupport
{
    public static void AddServiceDependencies(IServiceCollection services)
    {
        services.TryAddSingleton<IPropertyRepository>(
            new EmptyPropertyRepository());
        services.TryAddSingleton<IRoomRepository>(new EmptyRoomRepository());
        services.TryAddSingleton<IPropertiesCreationOperationLock>(
            new PassingCreationOperationLock());
        services.TryAddSingleton<IPropertiesOperationLock>(
            new PassingOperationLock());
        services.TryAddSingleton<IPropertiesUniqueCoordinateLock>(
            new PassingUniqueCoordinateLock());
        services.TryAddSingleton<IScopeContext>(new TestScopeContext());
    }

    public static PropertiesMutationCoordinator Create(
        IPropertyRepository? properties = null,
        IRoomRepository? rooms = null,
        IPropertiesOperationLock? operationLock = null,
        IPropertiesUniqueCoordinateLock? uniqueCoordinates = null,
        IScopeContext? scopeContext = null) => new(
            properties ?? new EmptyPropertyRepository(),
            rooms ?? new EmptyRoomRepository(),
            operationLock ?? new PassingOperationLock(),
            uniqueCoordinates ?? new PassingUniqueCoordinateLock(),
            scopeContext ?? new TestScopeContext());

    private sealed class EmptyPropertyRepository : IPropertyRepository
    {
        public Task AddAsync(
            Property property,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Property?> GetAsync(
            Guid propertyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Property?>(null);

        public Task<bool> CodeExistsAsync(
            string code,
            Guid? excludingPropertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyRoomRepository : IRoomRepository
    {
        public Task AddAsync(
            Room room,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Room?> GetAsync(
            Guid roomId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Room?>(null);

        public Task<bool> HasActiveRoomsAsync(
            Guid propertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> RoomNameExistsAsync(
            Guid propertyId,
            string name,
            Guid? excludingRoomId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class PassingOperationLock : IPropertiesOperationLock
    {
        public Task<bool> TryAcquirePropertyAsync(
            string tenantId,
            Guid propertyId,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> TryAcquireRoomAsync(
            string tenantId,
            Guid roomId,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class PassingCreationOperationLock
        : IPropertiesCreationOperationLock
    {
        public Task AcquireAsync(
            string tenantId,
            Guid operationId,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PassingUniqueCoordinateLock
        : IPropertiesUniqueCoordinateLock
    {
        public Task AcquirePropertyCodeAsync(
            string tenantId,
            string propertyCode,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AcquireRoomNameAsync(
            string tenantId,
            Guid propertyId,
            string roomName,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
