namespace BunkFy.Modules.Ingestion.Domain.DataRights;

using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class IngestionAnonymisationRecordPlanEntry
    : ScopedEntity<Guid>
{
    private IngestionAnonymisationRecordPlanEntry() { }

    private IngestionAnonymisationRecordPlanEntry(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid TombstoneId { get; private set; }
    public IngestionAnonymisationRecordKind Kind { get; private set; }
    public Guid RecordId { get; private set; }
    public long SelectedVersion { get; private set; }
    public long ReductionVersion { get; private set; }
    public long ResultingVersion { get; private set; }
    public Guid? RawPayloadFileId { get; private set; }
    public Guid? RawPayloadConnectionId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public bool RequiresRawPayloadDeletion =>
        this.RawPayloadFileId.HasValue;

    public static Result<IngestionAnonymisationRecordPlanEntry> Create(
        Guid id,
        string tenantId,
        Guid tombstoneId,
        IngestionAnonymisationRecordKind kind,
        Guid recordId,
        long selectedVersion,
        long reductionVersion,
        long resultingVersion,
        Guid? rawPayloadFileId,
        Guid? rawPayloadConnectionId,
        DateTimeOffset createdAtUtc)
    {
        bool hasRawPayload = rawPayloadFileId.HasValue ||
            rawPayloadConnectionId.HasValue;
        bool validRawPayload = !hasRawPayload ||
            (kind == IngestionAnonymisationRecordKind.ObservationReceipt &&
             rawPayloadFileId is { } fileId &&
             fileId != Guid.Empty &&
             rawPayloadConnectionId is { } connectionId &&
             connectionId != Guid.Empty);
        if (id == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            tombstoneId == Guid.Empty ||
            !Enum.IsDefined(kind) ||
            kind == IngestionAnonymisationRecordKind.Unknown ||
            recordId == Guid.Empty ||
            selectedVersion <= 0 ||
            reductionVersion < selectedVersion ||
            resultingVersion < reductionVersion ||
            !validRawPayload ||
            createdAtUtc == default)
        {
            return Result.Failure<
                IngestionAnonymisationRecordPlanEntry>(
                    IngestionDomainErrors.AnonymisationTombstoneInvalid);
        }

        bool requiresMutation = kind is not
            IngestionAnonymisationRecordKind.ObservationReprocessingAttempt;
        long expectedReductionVersion = requiresMutation
            ? selectedVersion + 1
            : selectedVersion;
        long expectedResultingVersion = hasRawPayload
            ? expectedReductionVersion + 1
            : expectedReductionVersion;
        if (reductionVersion != expectedReductionVersion ||
            resultingVersion != expectedResultingVersion)
        {
            return Result.Failure<
                IngestionAnonymisationRecordPlanEntry>(
                    IngestionDomainErrors.AnonymisationTombstoneInvalid);
        }

        return Result.Success(
            new IngestionAnonymisationRecordPlanEntry(id, scopeId)
            {
                TombstoneId = tombstoneId,
                Kind = kind,
                RecordId = recordId,
                SelectedVersion = selectedVersion,
                ReductionVersion = reductionVersion,
                ResultingVersion = resultingVersion,
                RawPayloadFileId = rawPayloadFileId,
                RawPayloadConnectionId = rawPayloadConnectionId,
                CreatedAtUtc = createdAtUtc.ToUniversalTime()
            });
    }

    public bool MatchesVersion(long version) =>
        version == this.ResultingVersion;

    public bool MatchesReductionVersion(long version) =>
        version == this.ReductionVersion;
}

public enum IngestionAnonymisationRecordKind
{
    Unknown = 0,
    ReservationSourceLink = 1,
    ObservationReceipt = 2,
    ChangeProposal = 3,
    ReservationDispatch = 4,
    ObservationReprocessingAttempt = 5,
    ObservationReprocessingOutput = 6
}
