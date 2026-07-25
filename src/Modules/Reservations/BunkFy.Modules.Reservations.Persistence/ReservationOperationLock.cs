namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.Domain.Models;

internal sealed class ReservationOperationLock : ScopedEntity<Guid>
{
    private ReservationOperationLock() { }

    public ReservationOperationLock(
        Guid id,
        string scopeId,
        Guid reservationId)
        : base(id, scopeId)
    {
        if (id == Guid.Empty || reservationId == Guid.Empty)
        {
            throw new ArgumentException(
                "The reservation operation-lock coordinate is invalid.");
        }

        this.ReservationId = reservationId;
    }

    public Guid ReservationId { get; private set; }
    public long Revision { get; private set; } = 1;

    public void Touch() => this.Revision = checked(this.Revision + 1);
}
