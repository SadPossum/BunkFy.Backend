namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.Domain;

public sealed class ReservationArrivalReminder : IScopedEntity
{
    private ReservationArrivalReminder() { }

    private ReservationArrivalReminder(
        Guid id,
        string scopeId,
        Guid reservationId,
        Guid propertyId,
        long detailsRevision,
        string timeZoneId,
        DateOnly arrival,
        TimeOnly expectedArrivalTime,
        DateTimeOffset expectedArrivalAtUtc,
        DateTimeOffset dueAtUtc,
        int leadTimeMinutes)
    {
        this.Id = id;
        this.ScopeId = scopeId;
        this.ReservationId = reservationId;
        this.PropertyId = propertyId;
        this.DetailsRevision = detailsRevision;
        this.TimeZoneId = timeZoneId;
        this.Arrival = arrival;
        this.ExpectedArrivalTime = expectedArrivalTime;
        this.ExpectedArrivalAtUtc = expectedArrivalAtUtc;
        this.DueAtUtc = dueAtUtc;
        this.LeadTimeMinutes = leadTimeMinutes;
        this.State = ReservationArrivalReminderState.Pending;
        this.Version = 1;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public Guid ReservationId { get; private set; }
    public Guid PropertyId { get; private set; }
    public long DetailsRevision { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateOnly Arrival { get; private set; }
    public TimeOnly ExpectedArrivalTime { get; private set; }
    public DateTimeOffset ExpectedArrivalAtUtc { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public int LeadTimeMinutes { get; private set; }
    public ReservationArrivalReminderState State { get; private set; }
    public DateTimeOffset? DispatchedAtUtc { get; private set; }
    public long Version { get; private set; }

    internal static ReservationArrivalReminder Create(
        Guid id,
        string scopeId,
        Guid reservationId,
        Guid propertyId,
        long detailsRevision,
        string timeZoneId,
        DateOnly arrival,
        TimeOnly expectedArrivalTime,
        DateTimeOffset expectedArrivalAtUtc,
        DateTimeOffset dueAtUtc,
        int leadTimeMinutes) => new(
            id,
            scopeId,
            reservationId,
            propertyId,
            detailsRevision,
            timeZoneId,
            arrival,
            expectedArrivalTime,
            expectedArrivalAtUtc,
            dueAtUtc,
            leadTimeMinutes);

    internal void Supersede()
    {
        if (this.State != ReservationArrivalReminderState.Pending)
        {
            return;
        }

        this.State = ReservationArrivalReminderState.Superseded;
        this.Version++;
    }

    internal void Reactivate()
    {
        if (this.State != ReservationArrivalReminderState.Superseded || this.DispatchedAtUtc.HasValue)
        {
            return;
        }

        this.State = ReservationArrivalReminderState.Pending;
        this.Version++;
    }

    internal void Dispatch(DateTimeOffset dispatchedAtUtc)
    {
        if (this.State != ReservationArrivalReminderState.Pending)
        {
            return;
        }

        this.State = ReservationArrivalReminderState.Dispatched;
        this.DispatchedAtUtc = dispatchedAtUtc;
        this.Version++;
    }
}

public enum ReservationArrivalReminderState
{
    Pending = 1,
    Dispatched = 2,
    Superseded = 3
}
