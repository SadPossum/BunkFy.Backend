namespace BunkFy.Modules.Guests.Domain.Models;

public sealed record GuestProfileAnonymisationOutcome(
    long PreviousVersion,
    long CurrentVersion,
    Guid EventId,
    DateTimeOffset OccurredAtUtc);
