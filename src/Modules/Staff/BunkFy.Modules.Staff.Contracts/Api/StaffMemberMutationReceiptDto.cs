namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffMemberMutationReceiptDto(
    Guid StaffMemberId,
    StaffStatus Status,
    long Version,
    DateTimeOffset CompletedAtUtc);
