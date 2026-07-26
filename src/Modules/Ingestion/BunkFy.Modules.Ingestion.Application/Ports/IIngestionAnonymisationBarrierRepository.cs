namespace BunkFy.Modules.Ingestion.Application.Ports;

internal interface IIngestionAnonymisationBarrierRepository
{
    Task<bool> IsBlockedAsync(
        Guid sourceLinkId,
        IReadOnlyCollection<IngestionAnonymisationFingerprintValue> fingerprints,
        CancellationToken cancellationToken);
}
