namespace BunkFy.Modules.Ingestion.Application.DataRights;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using Gma.Framework.Results;

internal sealed class IngestionAnonymisationBarrier(
    IIngestionSourceOperationLock operationLock,
    IIngestionAnonymisationFingerprintService fingerprints,
    IIngestionAnonymisationBarrierRepository repository)
{
    public async Task<Result<Guid>> AcquireAndCheckAsync(
        string tenantId,
        Guid connectionId,
        string recordType,
        string externalId,
        CancellationToken cancellationToken)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        string external = externalId?.Trim() ?? string.Empty;
        if (scopeId.Length == 0 ||
            connectionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(recordType) ||
            external.Length == 0)
        {
            return Result.Failure<Guid>(
                IngestionApplicationErrors.ObservationInvalid);
        }

        Guid sourceLinkId =
            ReservationOperationIdentity.CreateSourceLinkId(
                scopeId,
                connectionId,
                external);
        await operationLock.AcquireAsync(
            scopeId,
            sourceLinkId,
            cancellationToken).ConfigureAwait(false);

        Result<IReadOnlyList<IngestionAnonymisationFingerprintValue>>
            candidates = fingerprints.CreateCandidates(
                scopeId,
                IngestionAnonymisationFingerprintPurpose
                    .ObservationReceipt,
                connectionId,
                recordType,
                external);
        if (candidates.IsFailure)
        {
            return Result.Failure<Guid>(candidates.Error);
        }

        bool blocked = await repository.IsBlockedAsync(
            sourceLinkId,
            candidates.Value,
            cancellationToken).ConfigureAwait(false);
        return blocked
            ? Result.Failure<Guid>(
                IngestionApplicationErrors.AnonymisationBarrierActive)
            : Result.Success(sourceLinkId);
    }
}
