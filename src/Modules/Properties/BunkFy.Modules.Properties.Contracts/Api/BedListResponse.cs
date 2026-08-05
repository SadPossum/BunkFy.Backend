namespace BunkFy.Modules.Properties.Contracts;

public sealed record BedListResponse(
    IReadOnlyCollection<BedListItemDto> Beds,
    int Page,
    int PageSize,
    bool HasMore);
