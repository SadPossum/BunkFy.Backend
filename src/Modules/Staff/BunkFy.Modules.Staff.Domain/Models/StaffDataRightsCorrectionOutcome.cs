namespace BunkFy.Modules.Staff.Domain.Models;

public sealed record StaffDataRightsCorrectionOutcome(
    long PreviousVersion,
    long CurrentVersion,
    IReadOnlyCollection<StaffProfileField> ChangedFields,
    Guid EventId,
    DateTimeOffset OccurredAtUtc);
