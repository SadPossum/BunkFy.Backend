namespace BunkFy.Modules.DataRights.Domain.Entities;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Errors;
using Gma.Framework.Results;

public sealed class DataRightsSubjectCoordinate
{
    public const int OwnerKeyMaxLength = 100;
    public const int RecordTypeMaxLength = 100;

    private DataRightsSubjectCoordinate() { }

    private DataRightsSubjectCoordinate(
        string ownerKey,
        string recordType,
        Guid recordId,
        long recordVersion,
        string selectedBy,
        DateTimeOffset selectedAtUtc)
    {
        this.OwnerKey = ownerKey;
        this.RecordType = recordType;
        this.RecordId = recordId;
        this.RecordVersion = recordVersion;
        this.SelectedBy = selectedBy;
        this.SelectedAtUtc = selectedAtUtc;
    }

    public string OwnerKey { get; private set; } = string.Empty;
    public string RecordType { get; private set; } = string.Empty;
    public Guid RecordId { get; private set; }
    public long RecordVersion { get; private set; }
    public string SelectedBy { get; private set; } = string.Empty;
    public DateTimeOffset SelectedAtUtc { get; private set; }

    public static Result<DataRightsSubjectCoordinate> Create(
        string ownerKey,
        string recordType,
        Guid recordId,
        long recordVersion,
        string selectedBy,
        DateTimeOffset selectedAtUtc)
    {
        string normalizedOwner = ownerKey?.Trim().ToLowerInvariant() ?? string.Empty;
        string normalizedType = recordType?.Trim().ToLowerInvariant() ?? string.Empty;
        string normalizedActor = selectedBy?.Trim() ?? string.Empty;
        if (normalizedOwner.Length is 0 or > OwnerKeyMaxLength ||
            normalizedType.Length is 0 or > RecordTypeMaxLength ||
            recordId == Guid.Empty ||
            recordVersion <= 0)
        {
            return Result.Failure<DataRightsSubjectCoordinate>(
                DataRightsDomainErrors.SubjectCoordinateInvalid);
        }

        if (normalizedActor.Length is 0 or > DataRightsCase.ActorIdMaxLength)
        {
            return Result.Failure<DataRightsSubjectCoordinate>(DataRightsDomainErrors.ActorInvalid);
        }

        return selectedAtUtc == default
            ? Result.Failure<DataRightsSubjectCoordinate>(DataRightsDomainErrors.TimestampInvalid)
            : Result.Success(new DataRightsSubjectCoordinate(
                normalizedOwner,
                normalizedType,
                recordId,
                recordVersion,
                normalizedActor,
                selectedAtUtc));
    }
}
