namespace BunkFy.Modules.Reservations.Domain.Entities;

using Gma.Framework.Domain.Models;
using BunkFy.Modules.Reservations.Domain.Aggregates;

public sealed class ReservationGuest : ScopedEntity<Guid>
{
    private ReservationGuest() { }

    internal ReservationGuest(
        Guid guestId,
        string scopeId,
        Guid reservationId,
        ReservationGuestRole role,
        string linkedBy,
        DateTimeOffset linkedAtUtc,
        long linkVersion)
        : base(guestId, scopeId)
    {
        this.ReservationId = reservationId;
        this.Role = role;
        this.LinkedBy = linkedBy;
        this.LinkedAtUtc = linkedAtUtc;
        this.LinkVersion = linkVersion;
        this.IsCurrent = true;
    }

    public Guid ReservationId { get; private set; }
    public Guid GuestId => this.Id;
    public ReservationGuestRole Role { get; private set; }
    public string LinkedBy { get; private set; } = string.Empty;
    public DateTimeOffset LinkedAtUtc { get; private set; }
    public long LinkVersion { get; private set; }
    public bool IsCurrent { get; private set; }
    public string? UnlinkedBy { get; private set; }
    public DateTimeOffset? UnlinkedAtUtc { get; private set; }
    public DateOnly? UnlinkedArrival { get; private set; }
    public DateOnly? UnlinkedDeparture { get; private set; }
    public ReservationState? UnlinkedReservationStatus { get; private set; }
    public DateOnly? UnlinkedCheckedInBusinessDate { get; private set; }
    public DateOnly? UnlinkedNoShowBusinessDate { get; private set; }
    public DateOnly? UnlinkedCheckedOutBusinessDate { get; private set; }

    internal void Activate(
        ReservationGuestRole role,
        string linkedBy,
        DateTimeOffset linkedAtUtc,
        long linkVersion)
    {
        this.Role = role;
        this.LinkedBy = linkedBy;
        this.LinkedAtUtc = linkedAtUtc;
        this.LinkVersion = linkVersion;
        this.IsCurrent = true;
        this.UnlinkedBy = null;
        this.UnlinkedAtUtc = null;
        this.UnlinkedArrival = null;
        this.UnlinkedDeparture = null;
        this.UnlinkedReservationStatus = null;
        this.UnlinkedCheckedInBusinessDate = null;
        this.UnlinkedNoShowBusinessDate = null;
        this.UnlinkedCheckedOutBusinessDate = null;
    }

    internal void Deactivate(
        string actorId,
        DateTimeOffset unlinkedAtUtc,
        long linkVersion,
        DateOnly arrival,
        DateOnly departure,
        ReservationState reservationStatus,
        DateOnly? checkedInBusinessDate,
        DateOnly? noShowBusinessDate,
        DateOnly? checkedOutBusinessDate)
    {
        this.IsCurrent = false;
        this.UnlinkedBy = actorId;
        this.UnlinkedAtUtc = unlinkedAtUtc;
        this.LinkVersion = linkVersion;
        this.UnlinkedArrival = arrival;
        this.UnlinkedDeparture = departure;
        this.UnlinkedReservationStatus = reservationStatus;
        this.UnlinkedCheckedInBusinessDate = checkedInBusinessDate;
        this.UnlinkedNoShowBusinessDate = noShowBusinessDate;
        this.UnlinkedCheckedOutBusinessDate = checkedOutBusinessDate;
    }
}
