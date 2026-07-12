namespace BunkFy.Modules.Properties.Contracts;

public sealed record BedListResponse(
    IReadOnlyCollection<BedDto> Beds,
    int Page,
    int PageSize);
