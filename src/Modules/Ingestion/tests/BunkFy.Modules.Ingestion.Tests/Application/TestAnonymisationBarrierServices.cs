namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using Gma.Framework.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

internal static class TestAnonymisationBarrierServices
{
    public static IServiceCollection AddAllowingAnonymisationBarrier(
        this IServiceCollection services,
        IIngestionSourceOperationLock? operationLock = null) =>
        services.AddAnonymisationBarrier(
            blocked: false,
            operationLock: operationLock);

    public static IServiceCollection AddBlockingAnonymisationBarrier(
        this IServiceCollection services,
        IIngestionSourceOperationLock? operationLock = null) =>
        services.AddAnonymisationBarrier(
            blocked: true,
            operationLock: operationLock);

    private static IServiceCollection AddAnonymisationBarrier(
        this IServiceCollection services,
        bool blocked,
        IIngestionSourceOperationLock? operationLock)
    {
        services.AddNoOpExecutionLock();
        services.AddSingleton<IIngestionSourceOperationLock>(
            operationLock ?? new NoOpSourceOperationLock());
        services.TryAddSingleton<
            IIngestionSourceGraphLocator,
            EmptySourceGraphLocator>();
        services.AddSingleton<
            IIngestionAnonymisationFingerprintService,
            DeterministicFingerprintService>();
        services.AddSingleton<IIngestionAnonymisationBarrierRepository>(
            new ConfigurableBarrierRepository(blocked));
        return services;
    }

    private sealed class NoOpSourceOperationLock
        : IIngestionSourceOperationLock
    {
        public Task AcquireAsync(
            string tenantId,
            Guid sourceLinkId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class EmptySourceGraphLocator
        : IIngestionSourceGraphLocator
    {
        public Task<IngestionSourceGraphCoordinate?> FindReceiptAsync(
            Guid receiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IngestionSourceGraphCoordinate?>(null);

        public Task<IngestionSourceGraphCoordinate?> FindProposalAsync(
            Guid proposalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IngestionSourceGraphCoordinate?>(null);

        public Task<IngestionSourceGraphCoordinate?> FindDispatchAsync(
            Guid dispatchId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IngestionSourceGraphCoordinate?>(null);

        public Task<IngestionSourceGraphCoordinate?>
            FindReprocessingAttemptAsync(
            Guid attemptId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IngestionSourceGraphCoordinate?>(null);

        public Task<IngestionSourceGraphCoordinate?> FindSourceLinkAsync(
            Guid sourceLinkId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IngestionSourceGraphCoordinate?>(null);

        public Task<IngestionSourceGraphCoordinate?>
            FindAcceptedCancellationAsync(
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IngestionSourceGraphCoordinate?>(null);
    }

    private sealed class DeterministicFingerprintService
        : IIngestionAnonymisationFingerprintService
    {
        private static readonly IngestionAnonymisationFingerprintValue Value =
            new(
                IngestionAnonymisationFingerprintPurpose.ObservationReceipt,
                1,
                new string('a', 64));

        public Result<IngestionAnonymisationFingerprintValue> CreateActive(
            string tenantId,
            IngestionAnonymisationFingerprintPurpose purpose,
            Guid connectionId,
            string recordType,
            string externalId) =>
            Result.Success(Value with { Purpose = purpose });

        public Result<
            IReadOnlyList<IngestionAnonymisationFingerprintValue>>
            CreateCandidates(
                string tenantId,
                IngestionAnonymisationFingerprintPurpose purpose,
                Guid connectionId,
                string recordType,
                string externalId) =>
            Result.Success<
                IReadOnlyList<IngestionAnonymisationFingerprintValue>>(
                    [Value with { Purpose = purpose }]);
    }

    private sealed class ConfigurableBarrierRepository(bool blocked)
        : IIngestionAnonymisationBarrierRepository
    {
        public Task<bool> IsBlockedAsync(
            Guid sourceLinkId,
            IReadOnlyCollection<IngestionAnonymisationFingerprintValue>
                fingerprints,
            CancellationToken cancellationToken) =>
            Task.FromResult(blocked);
    }
}
