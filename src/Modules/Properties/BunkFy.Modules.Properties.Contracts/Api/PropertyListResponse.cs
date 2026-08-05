namespace BunkFy.Modules.Properties.Contracts;

public sealed record PropertyListResponse(
    IReadOnlyCollection<PropertyListItemDto> Properties,
    int Page,
    int PageSize,
    bool HasMore);
