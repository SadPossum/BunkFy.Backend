namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.Domain;

public sealed class ReservationGuestProcessingRestrictionProjection : IScopedEntity
{
    private ReservationGuestProcessingRestrictionProjection() { }

    public ReservationGuestProcessingRestrictionProjection(
        string scopeId,
        Guid propertyId,
        Guid guestId,
        int contractVersion,
        long revision,
        bool isRestricted)
    {
        this.ScopeId = scopeId;
        this.PropertyId = propertyId;
        this.GuestId = guestId;
        this.ContractVersion = contractVersion;
        this.Revision = revision;
        this.IsRestricted = isRestricted;
    }

    public string ScopeId { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public Guid GuestId { get; private set; }
    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public bool IsRestricted { get; private set; }

    public void Apply(int contractVersion, long revision, bool isRestricted)
    {
        if (revision <= this.Revision)
        {
            return;
        }

        this.ContractVersion = contractVersion;
        this.Revision = revision;
        this.IsRestricted = isRestricted;
    }
}
