namespace BunkFy.Modules.DataRights.Domain.Models;

public sealed record DataRightsSubjectSelection(
    string OwnerKey,
    string RecordType,
    Guid RecordId,
    long RecordVersion);
