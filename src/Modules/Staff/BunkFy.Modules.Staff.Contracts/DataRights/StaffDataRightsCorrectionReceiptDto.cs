namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffDataRightsCorrectionReceiptDto(
    Guid ReceiptId,
    Guid ExecutionId,
    Guid CaseId,
    long ApprovalRevision,
    Guid StaffMemberId,
    long SelectedRecordVersion,
    long CurrentRecordVersion,
    IReadOnlyCollection<string> ChangedFieldKeys,
    DateTimeOffset CompletedAtUtc);
