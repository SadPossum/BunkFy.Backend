namespace BunkFy.Modules.Reservations.Domain.Aggregates;

public sealed class ReservationCreationSnapshot
{
    private ReservationCreationSnapshot(
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        TimeOnly? expectedArrivalTime,
        TimeOnly? expectedDepartureTime,
        IReadOnlyList<Guid> inventoryUnitIds,
        string primaryGuestName,
        string? email,
        string? phone,
        int guestCount,
        ReservationSource source,
        string? sourceSystem,
        string? sourceReference,
        string? notes)
    {
        this.PropertyId = propertyId;
        this.Arrival = arrival;
        this.Departure = departure;
        this.ExpectedArrivalTime = expectedArrivalTime;
        this.ExpectedDepartureTime = expectedDepartureTime;
        this.InventoryUnitIds = inventoryUnitIds;
        this.PrimaryGuestName = primaryGuestName;
        this.Email = email;
        this.Phone = phone;
        this.GuestCount = guestCount;
        this.Source = source;
        this.SourceSystem = sourceSystem;
        this.SourceReference = sourceReference;
        this.Notes = notes;
    }

    public Guid PropertyId { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public TimeOnly? ExpectedArrivalTime { get; }
    public TimeOnly? ExpectedDepartureTime { get; }
    public IReadOnlyList<Guid> InventoryUnitIds { get; }
    public string PrimaryGuestName { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public int GuestCount { get; }
    public ReservationSource Source { get; }
    public string? SourceSystem { get; }
    public string? SourceReference { get; }
    public string? Notes { get; }

    public static ReservationCreationSnapshot Capture(
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        TimeOnly? expectedArrivalTime,
        TimeOnly? expectedDepartureTime,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        string primaryGuestName,
        string? email,
        string? phone,
        int guestCount,
        ReservationSource source,
        string? sourceSystem,
        string? sourceReference,
        string? notes)
    {
        ArgumentNullException.ThrowIfNull(inventoryUnitIds);
        return new(
            propertyId,
            arrival,
            departure,
            expectedArrivalTime,
            expectedDepartureTime,
            Array.AsReadOnly(inventoryUnitIds.Order().ToArray()),
            NormalizeRequired(primaryGuestName),
            NormalizeOptional(email),
            NormalizeOptional(phone),
            guestCount,
            source,
            NormalizeOptional(sourceSystem)?.ToLowerInvariant(),
            NormalizeOptional(sourceReference),
            NormalizeOptional(notes));
    }

    private static string NormalizeRequired(string? value) =>
        value?.Trim() ?? string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
