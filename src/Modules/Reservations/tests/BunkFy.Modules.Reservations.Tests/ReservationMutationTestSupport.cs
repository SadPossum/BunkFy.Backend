namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using Gma.Framework.Scoping;

internal static class ReservationMutationTestSupport
{
    public static ReservationMutationCoordinator Create(
        IReservationRepository reservations,
        IReservationOperationLock? operationLock = null,
        IScopeContext? scopeContext = null) => new(
            reservations,
            operationLock ?? new AllowingReservationOperationLock(),
            scopeContext ?? new TestReservationScopeContext());

    public static IReservationOperationLock AllowingOperationLock() =>
        new AllowingReservationOperationLock();

    private sealed class AllowingReservationOperationLock
        : IReservationOperationLock
    {
        public Task<bool> TryAcquireExistingAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task AcquireCoordinateAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestReservationScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
