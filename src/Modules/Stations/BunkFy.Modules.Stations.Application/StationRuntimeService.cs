namespace BunkFy.Modules.Stations.Application;

using BunkFy.Modules.Stations.Contracts;
using BunkFy.Modules.Stations.Domain;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Paired-device runtime only. No Auth principal, management permission, pairing issuance or HTTP activation.
/// Every call discovers the tenant from the opaque credential, then uses a fresh normally filtered scope.
/// </summary>
public sealed class StationRuntimeService(IServiceScopeFactory scopes, IStationCredentialBootstrap bootstrap,
    ISystemClock clock) : IStationSessionReader
{
    public Task<StationRuntimeResponse> ReadAsync(string opaqueCredential, CancellationToken cancellationToken = default) =>
        this.WithDeviceAsync(opaqueCredential, async (services, device, store) =>
            await this.CurrentAsync(services, device, store, opaqueCredential, cancellationToken).ConfigureAwait(false), cancellationToken);

    /// <summary>One deliberate PIN attempt. Reusing its operation ID resolves that attempt, not a corrected PIN.</summary>
    public Task<StationRuntimeResponse> UnlockAsync(string opaqueCredential, Guid operationId, Guid staffId,
        long expectedGeneration, string pin, CancellationToken cancellationToken = default) =>
        this.WithDeviceAsync(opaqueCredential, async (services, device, store) =>
        {
            if (operationId == Guid.Empty || staffId == Guid.Empty || expectedGeneration < 1)
            {
                return Changed();
            }
            StationRuntimeFacts? facts = await store.ReadAsync(device, opaqueCredential, staffId, clock.UtcNow, cancellationToken).ConfigureAwait(false);
            if (facts is null)
            {
                return Invalid();
            }
            StationCredentialFacts? credential = facts.Credential;
            var binding = credential?.Enrollment ?? new StationEnrollmentBinding(StationActorKind.Unknown, null);
            bool denied = credential is null || credential.Revoked;
            if (!denied)
            {
                if (!binding.IsBound)
                {
                    return Changed();
                }
                StationAdmission admission = await services.GetRequiredService<StationAdmissionCoordinator>()
                    .ObserveAsync(device.PropertyId, staffId, binding, cancellationToken).ConfigureAwait(false);
                if (admission.State is StationAdmissionState.StateChanged or StationAdmissionState.Unavailable)
                {
                    return FromAdmission(admission);
                }
                denied = admission.State != StationAdmissionState.Current;
                if (facts != await store.ReadAsync(device, opaqueCredential, staffId, clock.UtcNow, cancellationToken).ConfigureAwait(false))
                {
                    return Changed();
                }
            }
            StationCoreResult result = await services.GetRequiredService<IStationsStore>().TryUnlockCoreAsync(operationId,
                device.StationId, device.BrowserSessionId, staffId, expectedGeneration, credential?.Revision ?? 0,
                (StationAuthorityKind)binding.Kind, binding.Kind == StationActorKind.StationOnly ? facts.GrantRevision : null,
                pin, clock.UtcNow, binding, denied, opaqueCredential, cancellationToken).ConfigureAwait(false);
            return await this.ResultAsync(services, device, store, opaqueCredential, result, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public Task<StationRuntimeResponse> LockAsync(string opaqueCredential, Guid operationId, long expectedGeneration,
        CancellationToken cancellationToken = default) =>
        this.WithDeviceAsync(opaqueCredential, async (services, device, store) =>
        {
            if (operationId == Guid.Empty || expectedGeneration < 1)
            {
                return Changed();
            }
            StationCoreResult result = await services.GetRequiredService<IStationsStore>().LockCoreAsync(operationId,
                device.StationId, device.BrowserSessionId, expectedGeneration, clock.UtcNow, opaqueCredential, cancellationToken).ConfigureAwait(false);
            return await this.ResultAsync(services, device, store, opaqueCredential, result, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>Explicit, bounded foreground activity. Snapshot/background reads never call this mutation.</summary>
    public Task<StationRuntimeResponse> ForegroundActivityAsync(string opaqueCredential, Guid operationId,
        StationActorCoordinate actor, CancellationToken cancellationToken = default) =>
        this.WithDeviceAsync(opaqueCredential, async (services, device, store) =>
        {
            if (operationId == Guid.Empty)
            {
                return Changed();
            }
            StationRuntimeFacts? facts = await store.ReadAsync(device, opaqueCredential, null, clock.UtcNow, cancellationToken).ConfigureAwait(false);
            if (facts is null)
            {
                return Invalid();
            }
            if (!facts.ActorCurrent || facts.Session.Actor != actor || facts.Credential is not { Enrollment.IsBound: true } credential)
            {
                return Changed();
            }
            StationAdmission admission = await services.GetRequiredService<StationAdmissionCoordinator>()
                .ObserveAsync(device.PropertyId, actor.StaffMemberId, credential.Enrollment, cancellationToken).ConfigureAwait(false);
            if (admission.State != StationAdmissionState.Current)
            {
                return FromAdmission(admission);
            }
            if (facts != await store.ReadAsync(device, opaqueCredential, null, clock.UtcNow, cancellationToken).ConfigureAwait(false))
            {
                return Changed();
            }
            StationCoreResult result = await store.ForegroundActivityAsync(operationId, device, opaqueCredential, actor,
                credential, facts.GrantRevision, clock.UtcNow, cancellationToken).ConfigureAwait(false);
            return await this.ResultAsync(services, device, store, opaqueCredential, result, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>
    /// Internal seeded-grant seam. Issuance and the linked-primary/private-station setup lanes are NOT implemented here.
    /// An operation identifies one attempt; replay returns its outcome, never derives or replaces the PIN again.
    /// </summary>
    public Task<StationRuntimeResponse> RedeemSeededSetupAsync(string opaqueCredential, Guid operationId, Guid setupId,
        string pin, CancellationToken cancellationToken = default) =>
        this.WithDeviceAsync(opaqueCredential, async (services, device, store) =>
        {
            if (operationId == Guid.Empty || setupId == Guid.Empty)
            {
                return Changed();
            }
            StationSetupFacts? setup = await store.ReadSetupAsync(device, setupId, clock.UtcNow, cancellationToken).ConfigureAwait(false);
            if (setup is null || !setup.Enrollment.IsBound || setup.Intent != setup.Enrollment.Kind)
            {
                return Changed();
            }
            StationAdmission admission = await services.GetRequiredService<StationAdmissionCoordinator>()
                .ObserveAsync(device.PropertyId, setup.StaffMemberId, setup.Enrollment, cancellationToken).ConfigureAwait(false);
            if (admission.State != StationAdmissionState.Current)
            {
                return FromAdmission(admission);
            }
            if (setup != await store.ReadSetupAsync(device, setupId, clock.UtcNow, cancellationToken).ConfigureAwait(false))
            {
                return Changed();
            }
            StationCoreResult? result = await store.ReadSetupOutcomeAsync(operationId, setup, device, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                StationPinMaterial? material = await services.GetRequiredService<IStationPinVerifier>().CreateAsync(pin, cancellationToken).ConfigureAwait(false);
                if (material is null)
                {
                    return new(StationSessionState.Unavailable);
                }
                // KDF work is intentionally outside the mutation; re-observe pinned owners after that delay.
                StationAdmission afterKdf = await services.GetRequiredService<StationAdmissionCoordinator>()
                    .ObserveAsync(device.PropertyId, setup.StaffMemberId, setup.Enrollment, cancellationToken).ConfigureAwait(false);
                if (afterKdf.State != StationAdmissionState.Current)
                {
                    return FromAdmission(afterKdf);
                }
                if (afterKdf.PropertyLocalDate != admission.PropertyLocalDate ||
                    setup != await store.ReadSetupAsync(device, setupId, clock.UtcNow, cancellationToken).ConfigureAwait(false))
                {
                    return Changed();
                }
                result = await services.GetRequiredService<IStationsStore>().RedeemSetupCoreAsync(operationId, setupId,
                    device.BrowserSessionId, material, clock.UtcNow, setup.Enrollment, opaqueCredential, cancellationToken).ConfigureAwait(false);
            }
            return await this.ResultAsync(services, device, store, opaqueCredential, result, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    private async Task<StationRuntimeResponse> ResultAsync(IServiceProvider services, StationDeviceReference device,
        IStationRuntimeStore store, string opaqueCredential, StationCoreResult result, CancellationToken ct)
    {
        if (result.Outcome == StationCoreOutcome.Conflict)
        {
            return Changed() with { Outcome = result.Outcome };
        }
        if (result.Outcome == StationCoreOutcome.Unavailable)
        {
            return new(StationSessionState.Unavailable, Outcome: result.Outcome);
        }
        StationRuntimeResponse current = await this.CurrentAsync(services, device, store, opaqueCredential, ct).ConfigureAwait(false);
        // A late result for A must never return B's actor after a concurrent switch/lock/re-enrollment.
        if ((current.State == StationSessionState.Active && (result.Outcome != StationCoreOutcome.Applied ||
                current.Session!.Actor!.ActorSessionId != result.ActorSessionId)) ||
            (result.Outcome == StationCoreOutcome.Applied && result.Generation.HasValue &&
                current.Session is not null && current.Session.Generation != result.Generation))
        {
            return Changed() with { Outcome = StationCoreOutcome.Conflict };
        }
        return current with { Outcome = result.Outcome, RetryAfterUtc = result.RetryAfterUtc };
    }

    private async Task<StationRuntimeResponse> CurrentAsync(IServiceProvider services, StationDeviceReference device,
        IStationRuntimeStore store, string opaqueCredential, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        StationRuntimeFacts? facts = await store.ReadAsync(device, opaqueCredential, null, now, ct).ConfigureAwait(false);
        if (facts is null)
        {
            return Invalid();
        }
        if (facts.Session.Actor is null || now >= facts.Session.ActorIdleExpiresAtUtc || now >= facts.Session.ActorAbsoluteExpiresAtUtc)
        {
            return new(StationSessionState.Locked, facts.Session with
            { Actor = null, ActorIdleExpiresAtUtc = null, ActorAbsoluteExpiresAtUtc = null });
        }
        if (!facts.ActorCurrent || facts.Credential is not { Enrollment.IsBound: true } credential)
        {
            return Changed();
        }
        StationAdmission admission = await services.GetRequiredService<StationAdmissionCoordinator>()
            .ObserveAsync(device.PropertyId, facts.Session.Actor.StaffMemberId, credential.Enrollment, ct).ConfigureAwait(false);
        if (admission.State != StationAdmissionState.Current)
        {
            return FromAdmission(admission);
        }
        StationRuntimeFacts? after = await store.ReadAsync(device, opaqueCredential, null, clock.UtcNow, ct).ConfigureAwait(false);
        return facts == after ? new(StationSessionState.Active, facts.Session) : Changed();
    }

    private async Task<StationRuntimeResponse> WithDeviceAsync(string opaqueCredential,
        Func<IServiceProvider, StationDeviceReference, IStationRuntimeStore, Task<StationRuntimeResponse>> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (StationCredentialEncoding.Digest(opaqueCredential) is null)
        {
            return Invalid();
        }
        try
        {
            StationDeviceReference? device = await bootstrap.FindAsync(opaqueCredential, ct).ConfigureAwait(false);
            if (device is null || !Guid.TryParseExact(device.ScopeId, "D", out Guid tenant) || tenant == Guid.Empty ||
                tenant.ToString("D") != device.ScopeId || device.BrowserSessionId == Guid.Empty || device.StationId == Guid.Empty || device.PropertyId == Guid.Empty)
            {
                return Invalid();
            }
            await using AsyncServiceScope child = scopes.CreateAsyncScope();
            IServiceProvider services = child.ServiceProvider;
            services.GetRequiredService<IScopeContextAccessor>().SetScope(device.ScopeId);
            IStationRuntimeStore store = services.GetRequiredService<IStationRuntimeStore>();
            if (await store.ReadAsync(device, opaqueCredential, null, clock.UtcNow, ct).ConfigureAwait(false) is null)
            {
                return Invalid();
            }
            return await action(services, device, store).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return new(StationSessionState.Unavailable); }
    }
    private static StationRuntimeResponse Invalid() => new(StationSessionState.Invalid);
    private static StationRuntimeResponse Changed() => new(StationSessionState.StateChanged);
    private static StationRuntimeResponse FromAdmission(StationAdmission admission) => admission.State == StationAdmissionState.Unavailable
        ? new(StationSessionState.Unavailable) : Changed();
}
