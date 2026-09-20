namespace BunkFy.Modules.Stations.Application;

using BunkFy.Modules.Stations.Contracts;
using BunkFy.Modules.Stations.Domain;
using System.Security.Cryptography;

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
        string pin, DateTimeOffset now, StationEnrollmentBinding? enrollment = null, bool ownerRejected = false, string? opaqueCredential = null,
        CancellationToken cancellationToken = default);
    Task<StationCoreResult> LockCoreAsync(Guid operationId, Guid stationId, Guid browserId, long expectedGeneration,
        DateTimeOffset now, string? opaqueCredential = null, CancellationToken cancellationToken = default);
    Task<StationCoreResult> RevokeCredentialCoreAsync(Guid operationId, Guid staffId, long expectedRevision,
        DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<StationCoreResult> RevokeStationCoreAsync(Guid operationId, Guid stationId, DateTimeOffset now,
        CancellationToken cancellationToken = default);
    Task<StationCoreResult> RevokeGrantCoreAsync(Guid operationId, Guid propertyId, Guid staffId, long expectedRevision,
        DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<StationCoreResult> RedeemSetupCoreAsync(Guid operationId, Guid setupId, Guid browserId, StationPinMaterial material,
        DateTimeOffset now, StationEnrollmentBinding? enrollment = null, string? opaqueCredential = null, CancellationToken cancellationToken = default);
}

/// <summary>Stations-owner bootstrap only; never accepts a caller tenant or exposes a general scope runner.</summary>
public interface IStationCredentialBootstrap
{
    Task<StationDeviceReference?> FindAsync(string opaqueCredential, CancellationToken cancellationToken = default);
}
public sealed record StationDeviceReference(string ScopeId, Guid BrowserSessionId, Guid StationId, Guid PropertyId);
public sealed record StationCredentialFacts(Guid StaffMemberId, long Revision, bool Revoked, StationEnrollmentBinding Enrollment);
public sealed record StationRuntimeFacts(StationSessionSnapshot Session, bool ActorCurrent,
    StationCredentialFacts? Credential, long? GrantRevision, bool GrantRevoked);
public sealed record StationSetupFacts(Guid Id, Guid StaffMemberId, long CredentialRevision,
    StationEnrollmentBinding Enrollment, StationActorKind Intent, DateTimeOffset ExpiresAtUtc);
public interface IStationRuntimeStore
{
    Task<StationRuntimeFacts?> ReadAsync(StationDeviceReference device, string opaqueCredential, Guid? selectedStaff,
        DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<StationSetupFacts?> ReadSetupAsync(StationDeviceReference device, Guid setupId, DateTimeOffset now,
        CancellationToken cancellationToken = default);
    Task<StationCoreResult?> ReadSetupOutcomeAsync(Guid operationId, StationSetupFacts setup, StationDeviceReference device,
        CancellationToken cancellationToken = default);
    Task<StationCoreResult> ForegroundActivityAsync(Guid operationId, StationDeviceReference device, string opaqueCredential,
        StationActorCoordinate actor, StationCredentialFacts credential, long? grantRevision, DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public enum StationAdmissionState { Current = 0, Denied = 1, StateChanged = 2, Unavailable = 3 }
public sealed record StationAdmission(StationAdmissionState State, DateOnly? PropertyLocalDate = null);

/// <summary>One canonical 32-byte opaque station credential; never diagnostics or a client-supplied scope.</summary>
public static class StationCredentialEncoding
{
    public static string? Digest(string credential)
    {
        if (credential is not { Length: 43 } || credential.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
        {
            return null;
        }
        byte[] bytes;
        try
        { bytes = Convert.FromBase64String(credential.Replace('-', '+').Replace('_', '/') + "="); }
        catch (FormatException) { return null; }
        try
        {
            if (bytes.Length != 32 || Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') != credential)
            {
                return null;
            }
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
