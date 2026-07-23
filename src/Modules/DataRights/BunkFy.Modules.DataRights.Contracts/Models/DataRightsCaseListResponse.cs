namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsCaseListResponse(
    IReadOnlyList<DataRightsCaseDto> Items,
    int Page,
    int PageSize);
