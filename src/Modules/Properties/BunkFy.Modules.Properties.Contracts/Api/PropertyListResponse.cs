namespace BunkFy.Modules.Properties.Contracts;

public sealed record PropertyListResponse(
    IReadOnlyCollection<PropertyDto> Properties,
    int Page,
    int PageSize);
