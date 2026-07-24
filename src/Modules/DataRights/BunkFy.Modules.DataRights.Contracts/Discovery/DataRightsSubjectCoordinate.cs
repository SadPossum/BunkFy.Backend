namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsSubjectCoordinate(
    string OwnerKey,
    string RecordType,
    Guid RecordId,
    long RecordVersion);

public sealed record DataRightsSubjectCoordinateKey(
    string OwnerKey,
    string RecordType,
    Guid RecordId);
