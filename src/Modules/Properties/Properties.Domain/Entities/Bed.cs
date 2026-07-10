namespace Properties.Domain.Entities;

using Properties.Domain.ValueObjects;
using Gma.Framework.Domain.Models;

public sealed class Bed : ScopedEntity<Guid>
{
    private Bed() { }

    private Bed(
        Guid id,
        string scopeId,
        Guid propertyId,
        Guid roomId,
        BedLabel label,
        DateTimeOffset nowUtc)
        : base(id, scopeId)
    {
        this.PropertyId = propertyId;
        this.RoomId = roomId;
        this.Label = label;
        this.CreatedAtUtc = nowUtc;
    }

    public Guid PropertyId { get; private set; }
    public Guid RoomId { get; private set; }
    public BedLabel Label { get; private set; }
    public BedState Status { get; private set; } = BedState.Active;
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? RetiredAtUtc { get; private set; }

    internal static Bed Create(
        Guid id,
        string scopeId,
        Guid propertyId,
        Guid roomId,
        BedLabel label,
        DateTimeOffset nowUtc) =>
        new(id, scopeId, propertyId, roomId, label, nowUtc);

    internal void Update(BedLabel label, DateTimeOffset nowUtc)
    {
        this.Label = label;
        this.UpdatedAtUtc = nowUtc;
        this.Version++;
    }

    internal void Retire(DateTimeOffset nowUtc)
    {
        this.Status = BedState.Retired;
        this.RetiredAtUtc = nowUtc;
        this.UpdatedAtUtc = nowUtc;
        this.Version++;
    }
}
