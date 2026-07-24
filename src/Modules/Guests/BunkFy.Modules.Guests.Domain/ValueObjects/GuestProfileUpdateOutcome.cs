namespace BunkFy.Modules.Guests.Domain.ValueObjects;

using BunkFy.Modules.Guests.Domain.Models;

public sealed class GuestProfileUpdateOutcome
{
    internal GuestProfileUpdateOutcome(
        long previousVersion,
        long currentVersion,
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        IReadOnlyCollection<GuestProfileField> changedFields)
    {
        this.PreviousVersion = previousVersion;
        this.CurrentVersion = currentVersion;
        this.EventId = eventId;
        this.OccurredAtUtc = occurredAtUtc;
        this.ChangedFields = Array.AsReadOnly(changedFields.ToArray());
    }

    public long PreviousVersion { get; }
    public long CurrentVersion { get; }
    public Guid EventId { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public IReadOnlyCollection<GuestProfileField> ChangedFields { get; }
}
