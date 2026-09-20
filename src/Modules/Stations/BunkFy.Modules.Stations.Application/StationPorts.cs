namespace BunkFy.Modules.Stations.Application;

using BunkFy.Modules.Stations.Contracts;
using BunkFy.Modules.Stations.Domain;

/// <summary>Secret provider is deployment-owned; no default or database pepper is supplied.</summary>
public interface IStationPepperProvider
{
    bool TryGet(string version, out ReadOnlyMemory<byte> pepper);
}

public enum StationPinVerification { Unavailable = 0, Valid = 1, Invalid = 2, Busy = 3 }
public interface IStationPinVerifier
{
    Task<StationPinMaterial?> CreateAsync(string pin, CancellationToken cancellationToken = default);
    Task<StationPinVerification> VerifyAsync(string pin, StationStaffCredential credential, CancellationToken cancellationToken = default);
}

/// <summary>
/// Local persistence primitives only. Callers MUST supply external owner admission before activation.
/// P1 registers no host/session validator or management/runtime services.
/// </summary>
public interface IStationsStore
{
    Task<StationCoreResult> TryUnlockCoreAsync(Guid operationId, Guid stationId, Guid browserId, Guid staffId,
        long expectedGeneration, long expectedCredentialRevision, StationAuthorityKind kind, long? expectedGrantRevision,
        string pin, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<StationCoreResult> LockCoreAsync(Guid operationId, Guid stationId, Guid browserId, long expectedGeneration,
        DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<StationCoreResult> RevokeCredentialCoreAsync(Guid operationId, Guid staffId, long expectedRevision,
        DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<StationCoreResult> RevokeStationCoreAsync(Guid operationId, Guid stationId, DateTimeOffset now,
        CancellationToken cancellationToken = default);
    Task<StationCoreResult> RevokeGrantCoreAsync(Guid operationId, Guid propertyId, Guid staffId, long expectedRevision,
        DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<StationCoreResult> RedeemSetupCoreAsync(Guid operationId, Guid setupId, Guid browserId, StationPinMaterial material,
        DateTimeOffset now, CancellationToken cancellationToken = default);
}

