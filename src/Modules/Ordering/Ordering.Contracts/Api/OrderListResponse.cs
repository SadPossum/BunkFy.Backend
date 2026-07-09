namespace Ordering.Contracts;

public sealed record OrderListResponse(
    IReadOnlyList<OrderDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
