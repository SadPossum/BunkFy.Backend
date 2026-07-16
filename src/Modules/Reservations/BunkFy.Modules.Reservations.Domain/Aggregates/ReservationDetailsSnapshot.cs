namespace BunkFy.Modules.Reservations.Domain.Aggregates;

public sealed record ReservationDetailsSnapshot
{
    public ReservationDetailsSnapshot(
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        string primaryGuestName,
        string? email,
        string? phone,
        int guestCount,
        string? notes,
        TimeOnly? expectedArrivalTime = null,
        TimeOnly? expectedDepartureTime = null)
    {
        this.Arrival = arrival;
        this.Departure = departure;
        this.ExpectedArrivalTime = expectedArrivalTime;
        this.ExpectedDepartureTime = expectedDepartureTime;
        this.InventoryUnitIds = Array.AsReadOnly(inventoryUnitIds.ToArray());
        this.PrimaryGuestName = primaryGuestName;
        this.Email = email;
        this.Phone = phone;
        this.GuestCount = guestCount;
        this.Notes = notes;
    }

    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public TimeOnly? ExpectedArrivalTime { get; }
    public TimeOnly? ExpectedDepartureTime { get; }
    public IReadOnlyCollection<Guid> InventoryUnitIds { get; }
    public string PrimaryGuestName { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public int GuestCount { get; }
    public string? Notes { get; }
}
