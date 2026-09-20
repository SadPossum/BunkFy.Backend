namespace BunkFy.Modules.Stations.Domain;

using Gma.Framework.Domain;

public sealed class StationOperationReceipt : IScopedEntity
{
    private StationOperationReceipt() { }
    public StationOperationReceipt(string scopeId, Guid id, StationMutationKind kind, string fingerprint,
        StationMutationResult result, DateTimeOffset now)
    {
        StationRules.Coordinates(scopeId, id);
        StationRules.Digest(fingerprint);
        StationRules.Utc(now);
        if (!Enum.IsDefined(kind) || kind == StationMutationKind.Unknown || result.Outcome == StationMutationOutcome.Unknown)
        {
            throw new ArgumentException("Invalid operation receipt.");
        }

        this.ScopeId = scopeId;
        this.Id = id;
        this.Kind = kind;
        this.Fingerprint = fingerprint;
        this.Outcome = result.Outcome;
        this.ActorSessionId = result.ActorSessionId;
        this.Generation = result.Generation;
        this.RetryAfterUtc = result.RetryAfterUtc;
        this.CreatedAtUtc = now;
    }
    public string ScopeId { get; private set; } = "";
    public Guid Id { get; private set; }
    public StationMutationKind Kind { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public StationMutationOutcome Outcome { get; private set; }
    public Guid? ActorSessionId { get; private set; }
    public long? Generation { get; private set; }
    public DateTimeOffset? RetryAfterUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public bool Matches(StationMutationKind kind, string fingerprint) => this.Kind == kind && this.Fingerprint == fingerprint;
    public StationMutationResult Result() => new(this.Outcome, this.ActorSessionId, this.Generation, this.RetryAfterUtc);
}


public enum StationMutationKind { Unknown = 0, Register = 1, RedeemSetup = 2, Unlock = 3, Lock = 4, Reset = 5, RevokeGrant = 6, RevokeStation = 7 }
public enum StationMutationOutcome { Unknown = 0, Applied = 1, Rejected = 2, Throttled = 3, Conflict = 4, Unavailable = 5 }
public sealed record StationMutationResult(StationMutationOutcome Outcome, Guid? ActorSessionId = null, long? Generation = null, DateTimeOffset? RetryAfterUtc = null);
