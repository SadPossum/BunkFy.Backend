namespace BunkFy.Modules.Guests.Contracts;

public sealed record GuestMutationReceiptDto(
    Guid GuestId,
    GuestStatus Status,
    long Version,
    DateTimeOffset LastChangedAtUtc);
