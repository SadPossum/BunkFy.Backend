namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffProfileMutationReceiptDto(
    Guid StaffMemberId,
    StaffStatus Status,
    long Version,
    DateTimeOffset CompletedAtUtc);
