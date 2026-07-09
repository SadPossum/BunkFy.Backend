namespace Properties.Domain.Entities;

using Properties.Domain.ValueObjects;

public sealed class Bed
{
    private Bed() { }

    private Bed(
        Guid id,
        string tenantId,
        Guid propertyId,
        Guid roomId,
        BedLabel label,
        DateTimeOffset nowUtc)
    {
        this.Id = id;
        this.TenantId = tenantId;
        this.PropertyId = propertyId;
        this.RoomId = roomId;
        this.Label = label;
        this.CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public Guid RoomId { get; private set; }
    public BedLabel Label { get; private set; }
    public BedState Status { get; private set; } = BedState.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? RetiredAtUtc { get; private set; }

    internal static Bed Create(
        Guid id,
        string tenantId,
        Guid propertyId,
        Guid roomId,
        BedLabel label,
        DateTimeOffset nowUtc) =>
        new(id, tenantId, propertyId, roomId, label, nowUtc);

    internal void Update(BedLabel label, DateTimeOffset nowUtc)
    {
        this.Label = label;
        this.UpdatedAtUtc = nowUtc;
    }

    internal void Retire(DateTimeOffset nowUtc)
    {
        this.Status = BedState.Retired;
        this.RetiredAtUtc = nowUtc;
        this.UpdatedAtUtc = nowUtc;
    }
}
