namespace BunkFy.Modules.Staff.Domain.Events;

using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Domain;

public sealed record StaffDataRightsCorrectionAppliedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string ScopeId,
    Guid ExecutionId,
    Guid ReceiptId,
    Guid CaseId,
    long ApprovalRevision,
    Guid StaffMemberId,
    long SelectedRecordVersion,
    long CurrentRecordVersion,
    IReadOnlyCollection<StaffProfileField> ChangedFields)
    : ScopedDomainEvent(EventId, OccurredAtUtc, ScopeId);
