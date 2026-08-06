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
        Result<SourceIdentity> identity = ResolveIdentity(
            tenantId,
            connectionId,
            recordType,
            externalId);
        if (identity.IsFailure)
        {
            return Result.Failure<Guid>(
                identity.Error);
        }

        await operationLock.AcquireAsync(
            identity.Value.ScopeId,
            identity.Value.SourceLinkId,
            cancellationToken).ConfigureAwait(false);
        return await this.CheckAsync(identity.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<Guid>> CheckUnderLockAsync(
        string tenantId,
        Guid connectionId,
        string recordType,
        string externalId,
        CancellationToken cancellationToken)
    {
        Result<SourceIdentity> identity = ResolveIdentity(
            tenantId,
            connectionId,
            recordType,
            externalId);
        return identity.IsFailure
            ? Result.Failure<Guid>(identity.Error)
            : await this.CheckAsync(identity.Value, cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<Result<Guid>> CheckAsync(
        SourceIdentity identity,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<IngestionAnonymisationFingerprintValue>>
            candidates = fingerprints.CreateCandidates(
                identity.ScopeId,
                IngestionAnonymisationFingerprintPurpose
                    .ObservationReceipt,
                identity.ConnectionId,
                identity.RecordType,
                identity.ExternalId);
        if (candidates.IsFailure)
        {
            return Result.Failure<Guid>(candidates.Error);
        }

        bool blocked = await repository.IsBlockedAsync(
            identity.SourceLinkId,
            candidates.Value,
            cancellationToken).ConfigureAwait(false);
        return blocked
            ? Result.Failure<Guid>(
                IngestionApplicationErrors.AnonymisationBarrierActive)
            : Result.Success(identity.SourceLinkId);
    }

    private static Result<SourceIdentity> ResolveIdentity(
        string tenantId,
        Guid connectionId,
        string recordType,
        string externalId)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        string record = recordType?.Trim() ?? string.Empty;
        string external = externalId?.Trim() ?? string.Empty;
        if (scopeId.Length == 0 ||
            connectionId == Guid.Empty ||
            record.Length == 0 ||
            external.Length == 0)
        {
            return Result.Failure<SourceIdentity>(
                IngestionApplicationErrors.ObservationInvalid);
        }

        return Result.Success(new SourceIdentity(
            scopeId,
            connectionId,
            record,
            external,
            ReservationOperationIdentity.CreateSourceLinkId(
                scopeId,
                connectionId,
                external)));
    }

    private sealed record SourceIdentity(
        string ScopeId,
        Guid ConnectionId,
        string RecordType,
        string ExternalId,
        Guid SourceLinkId);
}
