namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.Domain;

public sealed class ReservationPropertyProjection : IScopedEntity
{
    private ReservationPropertyProjection() { }

    private ReservationPropertyProjection(Guid id, string scopeId)
    {
        this.Id = id;
        this.ScopeId = scopeId;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public string? TimeZoneId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsKnown { get; private set; }
    public long SourceVersion { get; private set; }

    internal static ReservationPropertyProjection Create(Guid propertyId, string scopeId) =>
        new(propertyId, scopeId);

    internal bool Apply(string? timeZoneId, bool isActive, long sourceVersion)
    {
        if (sourceVersion <= this.SourceVersion)
        {
            return false;
        }

        string? previousTimeZoneId = this.TimeZoneId;
        bool previousIsActive = this.IsActive;
        bool wasKnown = this.IsKnown;
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            this.TimeZoneId = timeZoneId.Trim();
        }

        this.IsActive = isActive;
        this.IsKnown = true;
        this.SourceVersion = sourceVersion;
        return !wasKnown ||
               previousIsActive != this.IsActive ||
               !string.Equals(previousTimeZoneId, this.TimeZoneId, StringComparison.Ordinal);
    }
}
