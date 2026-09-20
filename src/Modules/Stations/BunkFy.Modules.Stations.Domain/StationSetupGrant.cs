namespace BunkFy.Modules.Stations.Domain;

using Gma.Framework.Domain;

public sealed class StationSetupGrant : IScopedEntity
{
    private StationSetupGrant() { }
    public StationSetupGrant(Guid id, string scopeId, Guid stationId, Guid browserSessionId, Guid propertyId,
        Guid staffId, StationActorKind kind, long expectedCredentialRevision, DateTimeOffset now, DateTimeOffset expires,
        StationEnrollmentBinding? enrollment = null)
    {
        StationRules.Coordinates(scopeId, id, stationId, browserSessionId, propertyId, staffId);
        StationRules.Utc(now);
        StationRules.Utc(expires);
        if (kind is not (StationActorKind.LinkedStation or StationActorKind.StationOnly) ||
            expectedCredentialRevision < 0 || expires <= now || expires - now > TimeSpan.FromMinutes(10) ||
            (enrollment is not null && (!enrollment.IsBound || enrollment.Kind != kind)))
        {
            throw new ArgumentException("Invalid setup grant.");
        }

        this.Id = id;
        this.ScopeId = scopeId;
        this.StationId = stationId;
        this.BrowserSessionId = browserSessionId;
        this.PropertyId = propertyId;
        this.StaffMemberId = staffId;
        this.AuthorityKind = kind;
        this.ExpectedEnrollmentAuthorityKind = enrollment?.Kind ?? StationActorKind.Unknown;
        this.ExpectedEnrollmentAuthSubjectId = enrollment?.AuthSubjectId;
        this.ExpectedCredentialRevision = expectedCredentialRevision;
        this.CreatedAtUtc = now;
        this.ExpiresAtUtc = expires;
    }
    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = "";
    public Guid StationId { get; private set; }
    public Guid BrowserSessionId { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public StationActorKind AuthorityKind { get; private set; }
    public StationActorKind ExpectedEnrollmentAuthorityKind { get; private set; }
    public string? ExpectedEnrollmentAuthSubjectId { get; private set; }
    public StationEnrollmentBinding ExpectedEnrollment() => new(this.ExpectedEnrollmentAuthorityKind, this.ExpectedEnrollmentAuthSubjectId);
    public long ExpectedCredentialRevision { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public bool Revoked { get; private set; }
    public bool Consume(DateTimeOffset now)
    {
        StationRules.Utc(now);
        if (this.Revoked || this.ConsumedAtUtc.HasValue || now < this.CreatedAtUtc || now >= this.ExpiresAtUtc)
        {
            return false;
        }

        this.ConsumedAtUtc = now;
        return true;
    }
    public void Revoke() => this.Revoked = true;
}
