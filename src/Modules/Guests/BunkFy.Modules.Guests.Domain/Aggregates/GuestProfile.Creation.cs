namespace BunkFy.Modules.Guests.Domain.Aggregates;

public sealed partial class GuestProfile
{
    public bool MatchesCreation(GuestProfileCreationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return this.OriginPropertyId == snapshot.OriginPropertyId &&
            this.CreationConfirmationId == snapshot.CreationConfirmationId &&
            this.HasValues(snapshot.Values);
    }
}
