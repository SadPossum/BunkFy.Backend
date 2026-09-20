namespace BunkFy.Modules.Stations.Contracts;

public enum StationAuthorityKind { Unknown = 0, LinkedStation = 1, StationOnly = 2 }
public enum StationOperationKind { Unknown = 0, Register = 1, RedeemSetup = 2, Unlock = 3, Lock = 4, Reset = 5, RevokeGrant = 6, RevokeStation = 7 }
public enum StationCoreOutcome { Unknown = 0, Applied = 1, Rejected = 2, Throttled = 3, Conflict = 4, Unavailable = 5 }
public sealed record StationCoreResult(StationCoreOutcome Outcome, Guid? ActorSessionId = null, long? Generation = null, DateTimeOffset? RetryAfterUtc = null);
/// <summary>Non-secret concurrency coordinates. Not proof of authorization.</summary>
public sealed record StationActorCoordinate(Guid StaffMemberId, Guid ActorSessionId, long Generation, StationAuthorityKind AuthorityKind);
public sealed record StationSessionSnapshot(Guid StationId, Guid PropertyId, Guid BrowserSessionId,
    long Generation, StationActorCoordinate? Actor, DateTimeOffset PairingExpiresAtUtc);

