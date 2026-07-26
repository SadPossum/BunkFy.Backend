namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.DataRights;
using Gma.Framework.Results;

internal interface IIngestionAnonymisationFingerprintService
{
    Result<IngestionAnonymisationFingerprintValue> CreateActive(
        string tenantId,
        IngestionAnonymisationFingerprintPurpose purpose,
        Guid connectionId,
        string recordType,
        string externalId);

    Result<IReadOnlyList<IngestionAnonymisationFingerprintValue>>
        CreateCandidates(
            string tenantId,
            IngestionAnonymisationFingerprintPurpose purpose,
            Guid connectionId,
            string recordType,
            string externalId);
}

internal sealed record IngestionAnonymisationFingerprintValue(
    IngestionAnonymisationFingerprintPurpose Purpose,
    int KeyVersion,
    string Sha256);
