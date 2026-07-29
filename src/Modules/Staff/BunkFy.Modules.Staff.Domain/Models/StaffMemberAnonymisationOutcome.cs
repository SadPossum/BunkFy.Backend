namespace BunkFy.Modules.Staff.Domain.Models;

public sealed record StaffMemberAnonymisationOutcome(
    long PreviousVersion,
    long CurrentVersion,
    Guid EventId,
    DateTimeOffset OccurredAtUtc);
