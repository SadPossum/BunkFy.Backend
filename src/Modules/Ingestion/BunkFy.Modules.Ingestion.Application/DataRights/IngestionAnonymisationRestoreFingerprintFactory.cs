namespace BunkFy.Modules.Ingestion.Application.DataRights;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;

internal static class IngestionAnonymisationRestoreFingerprintFactory
{
    public static Result<
        IReadOnlyList<IngestionAnonymisationFingerprintValue>> CreateValues(
            IIngestionAnonymisationFingerprintService fingerprintService,
            string tenantId,
            IngestionAnonymisationRestoreGraph graph)
    {
        List<IngestionAnonymisationFingerprintValue> values = [];
        Result<IngestionAnonymisationFingerprintValue> source =
            fingerprintService.CreateActive(
                tenantId,
                IngestionAnonymisationFingerprintPurpose.SourceLink,
                graph.SourceLink.ConnectionId,
                graph.SourceLink.SourceSystem,
                graph.SourceLink.SourceReference);
        if (source.IsFailure)
        {
            return FailureValues(source.Error);
        }

        values.Add(source.Value);
        foreach (ObservationReceipt receipt in graph.Receipts)
        {
            Result<IngestionAnonymisationFingerprintValue> created =
                fingerprintService.CreateActive(
                    tenantId,
                    IngestionAnonymisationFingerprintPurpose
                        .ObservationReceipt,
                    receipt.ConnectionId,
                    receipt.SourceRecordType,
                    receipt.ExternalId);
            if (created.IsFailure)
            {
                return FailureValues(created.Error);
            }

            values.Add(created.Value);
        }

        return Result.Success<
            IReadOnlyList<IngestionAnonymisationFingerprintValue>>(
                values.Distinct().ToArray());
    }

    public static Result<
        IReadOnlyList<IngestionAnonymisationFingerprint>> CreateEntities(
            IIdGenerator ids,
            string tenantId,
            Guid tombstoneId,
            IReadOnlyCollection<IngestionAnonymisationFingerprintValue>
                values,
            DateTimeOffset createdAtUtc)
    {
        List<IngestionAnonymisationFingerprint> fingerprints = [];
        foreach (IngestionAnonymisationFingerprintValue value in values)
        {
            Result<IngestionAnonymisationFingerprint> created =
                IngestionAnonymisationFingerprint.Create(
                    ids.NewId(),
                    tenantId,
                    tombstoneId,
                    value.Purpose,
                    value.KeyVersion,
                    value.Sha256,
                    createdAtUtc);
            if (created.IsFailure)
            {
                return Result.Failure<
                    IReadOnlyList<IngestionAnonymisationFingerprint>>(
                        created.Error);
            }

            fingerprints.Add(created.Value);
        }

        return Result.Success<
            IReadOnlyList<IngestionAnonymisationFingerprint>>(
                fingerprints);
    }

    private static Result<
        IReadOnlyList<IngestionAnonymisationFingerprintValue>> FailureValues(
            Error error) =>
        Result.Failure<
            IReadOnlyList<IngestionAnonymisationFingerprintValue>>(error);
}
