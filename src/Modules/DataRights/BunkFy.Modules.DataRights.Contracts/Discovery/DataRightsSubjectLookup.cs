namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsSubjectLookup(
    Guid? RecordId,
    string? Email,
    string? Phone,
    string? Name,
    DateOnly? DateOfBirth);
