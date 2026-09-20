namespace BunkFy.Modules.Stations.Persistence;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Stations.Application;
using BunkFy.Modules.Stations.Contracts;
using BunkFy.Modules.Stations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

/// <summary>Serialized local state only. External Staff/Auth/Access/property admission is a later mandatory caller.</summary>
internal sealed class StationsStore(StationsDbContext db, IStationPinVerifier verifier, IOptions<StationOptions> configured)
    : IStationsStore
{
    private readonly StationOptions options = configured.Value;
    public Task<StationCoreResult> TryUnlockCoreAsync(Guid operationId, Guid stationId, Guid browserId, Guid staffId,
        long expectedGeneration, long expectedCredentialRevision, StationAuthorityKind kind, long? expectedGrantRevision,
        string pin, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        this.RunAsync(operationId, StationOperationKind.Unlock,
            Fingerprint(stationId, browserId, staffId, expectedGeneration, expectedCredentialRevision, (int)kind, expectedGrantRevision),
            now, async () =>
            {
                StationBrowserSession? browser = await db.BrowserSessions.SingleOrDefaultAsync(x => x.Id == browserId && x.StationId == stationId, cancellationToken).ConfigureAwait(false);
                Station? station = await db.Stations.SingleOrDefaultAsync(x => x.Id == stationId, cancellationToken).ConfigureAwait(false);
                if (browser is not null && now < browser.LastObservedAtUtc)
                {
                    return Conflict(browser.Generation);
                }
                if (browser is null || station is null || station.Revoked || !browser.PairingCurrent(now, this.options.ExternalEpoch))
                {
                    return Rejected();
                }
                if (expectedGeneration != browser.Generation)
                {
                    return Conflict(browser.Generation);
                }

                StationStaffCredential? credential = await db.Credentials.SingleOrDefaultAsync(x => x.StaffMemberId == staffId, cancellationToken).ConfigureAwait(false);
                StationStaffCheckInGrant? grant = await db.CheckInGrants.SingleOrDefaultAsync(x =>
                    x.PropertyId == browser.PropertyId && x.StaffMemberId == staffId, cancellationToken).ConfigureAwait(false);
                // Stale coordinates never mutate A. Current authentication attempts deliberately lock A before checking B.
                if (credential is not null && (credential.Revision != expectedCredentialRevision || now < credential.LastObservedAtUtc))
                {
                    return Conflict(browser.Generation);
                }
                if (kind == StationAuthorityKind.StationOnly && grant is not null && grant.Revision != expectedGrantRevision)
                {
                    return Conflict(browser.Generation);
                }
                if (browser.StaffMemberId.HasValue)
                {
                    browser.Lock(now);
                }
                if (!browser.CanAttempt(now))
                {
                    return new(StationCoreOutcome.Throttled, Generation: browser.Generation, RetryAfterUtc: browser.CooldownUntilUtc);
                }
                if (credential is null || credential.Revoked ||
                    (kind == StationAuthorityKind.StationOnly && (grant is null || grant.Revoked)) ||
                    (kind == StationAuthorityKind.LinkedStation && expectedGrantRevision is not null) ||
                    kind is not (StationAuthorityKind.LinkedStation or StationAuthorityKind.StationOnly))
                {
                    browser.ReserveAttempt(now, this.options.DeviceAttempts, this.Window, this.Cooldown);
                    return DeviceFailure(browser);
                }
                if (!credential.CanAttempt(now))
                {
                    return new(StationCoreOutcome.Throttled, Generation: browser.Generation, RetryAfterUtc: credential.CooldownUntilUtc);
                }

                StationPinVerification pinResult = await verifier.VerifyAsync(pin, credential, cancellationToken).ConfigureAwait(false);
                if (pinResult is StationPinVerification.Unavailable or StationPinVerification.Busy)
                {
                    return new(StationCoreOutcome.Unavailable, Generation: browser.Generation);
                }

                if (pinResult != StationPinVerification.Valid)
                {
                    browser.ReserveAttempt(now, this.options.DeviceAttempts, this.Window, this.Cooldown);
                    credential.RecordFailure(now, this.options.CredentialFailures, this.Window, this.Cooldown);
                    return credential.CooldownUntilUtc.HasValue
                        ? new(StationCoreOutcome.Throttled, Generation: browser.Generation, RetryAfterUtc: credential.CooldownUntilUtc)
                        : DeviceFailure(browser);
                }
                credential.RecordSuccess(now);
                browser.Activate(staffId, Guid.NewGuid(), (StationActorKind)kind, credential.Revision,
                    kind == StationAuthorityKind.StationOnly ? grant!.Revision : null, now,
                    TimeSpan.FromMinutes(this.options.ActorIdleMinutes), TimeSpan.FromHours(this.options.ActorAbsoluteHours));
                return new(StationCoreOutcome.Applied, browser.ActorSessionId, browser.Generation);
            }, cancellationToken);

    public Task<StationCoreResult> LockCoreAsync(Guid operationId, Guid stationId, Guid browserId, long expectedGeneration,
        DateTimeOffset now, CancellationToken cancellationToken = default) =>
        this.RunAsync(operationId, StationOperationKind.Lock, Fingerprint(stationId, browserId, expectedGeneration), now, async () =>
        {
            StationBrowserSession? browser = await db.BrowserSessions.SingleOrDefaultAsync(x => x.Id == browserId && x.StationId == stationId, cancellationToken).ConfigureAwait(false);
            if (browser is not null && now < browser.LastObservedAtUtc)
            {
                return Conflict(browser.Generation);
            }
            if (browser is null || !browser.PairingCurrent(now, this.options.ExternalEpoch))
            {
                return Rejected();
            }

            if (browser.Generation != expectedGeneration)
            {
                return Conflict();
            }

            browser.Lock(now);
            return new(StationCoreOutcome.Applied, Generation: browser.Generation);
        }, cancellationToken);

    public Task<StationCoreResult> RevokeCredentialCoreAsync(Guid operationId, Guid staffId, long expectedRevision,
        DateTimeOffset now, CancellationToken cancellationToken = default) =>
        this.RunAsync(operationId, StationOperationKind.Reset, Fingerprint(staffId, expectedRevision), now, async () =>
        {
            StationStaffCredential? credential = await db.Credentials.SingleOrDefaultAsync(x => x.StaffMemberId == staffId, cancellationToken).ConfigureAwait(false);
            if (credential is null)
            {
                return Rejected();
            }

            if (credential.Revision != expectedRevision)
            {
                return Conflict();
            }

            StationBrowserSession[] sessions = await db.BrowserSessions.Where(x => x.StaffMemberId == staffId).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (now < credential.LastObservedAtUtc || sessions.Any(x => now < x.LastObservedAtUtc))
            {
                return Conflict();
            }

            credential.Revoke(now);
            foreach (StationBrowserSession session in sessions)
            {
                session.Lock(now);
            }

            foreach (StationSetupGrant grant in await db.SetupGrants.Where(x => x.StaffMemberId == staffId && !x.Revoked && x.ConsumedAtUtc == null).ToArrayAsync(cancellationToken).ConfigureAwait(false))
            {
                grant.Revoke();
            }

            return new(StationCoreOutcome.Applied);
        }, cancellationToken);

    public Task<StationCoreResult> RevokeStationCoreAsync(Guid operationId, Guid stationId, DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        this.RunAsync(operationId, StationOperationKind.RevokeStation, Fingerprint(stationId), now, async () =>
        {
            Station? station = await db.Stations.SingleOrDefaultAsync(x => x.Id == stationId, cancellationToken).ConfigureAwait(false);
            if (station is null)
            {
                return Rejected();
            }

            StationBrowserSession[] sessions = await db.BrowserSessions.Where(x => x.StationId == stationId).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (sessions.Any(x => now < x.LastObservedAtUtc))
            {
                return Conflict();
            }

            station.Revoke();
            foreach (StationBrowserSession session in sessions)
            {
                session.Revoke(now);
            }

            foreach (StationSetupGrant grant in await db.SetupGrants.Where(x => x.StationId == stationId && !x.Revoked && x.ConsumedAtUtc == null).ToArrayAsync(cancellationToken).ConfigureAwait(false))
            {
                grant.Revoke();
            }

            return new(StationCoreOutcome.Applied);
        }, cancellationToken);

    public Task<StationCoreResult> RevokeGrantCoreAsync(Guid operationId, Guid propertyId, Guid staffId, long expectedRevision,
        DateTimeOffset now, CancellationToken cancellationToken = default) =>
        this.RunAsync(operationId, StationOperationKind.RevokeGrant, Fingerprint(propertyId, staffId, expectedRevision), now, async () =>
        {
            StationStaffCheckInGrant? grant = await db.CheckInGrants.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.StaffMemberId == staffId, cancellationToken).ConfigureAwait(false);
            if (grant is null)
            {
                return Rejected();
            }

            if (grant.Revision != expectedRevision)
            {
                return Conflict();
            }

            StationBrowserSession[] sessions = await db.BrowserSessions.Where(x => x.PropertyId == propertyId && x.StaffMemberId == staffId && x.AuthorityKind == StationActorKind.StationOnly).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (sessions.Any(x => now < x.LastObservedAtUtc))
            {
                return Conflict();
            }

            grant.Revoke();
            foreach (StationBrowserSession session in sessions)
            {
                session.Lock(now);
            }

            return new(StationCoreOutcome.Applied);
        }, cancellationToken);

    public Task<StationCoreResult> RedeemSetupCoreAsync(Guid operationId, Guid setupId, Guid browserId, StationPinMaterial material,
        DateTimeOffset now, CancellationToken cancellationToken = default) =>
        this.RunAsync(operationId, StationOperationKind.RedeemSetup, Fingerprint(setupId, browserId, material.Salt, material.Verifier, material.PepperVersion), now, async () =>
        {
            StationSetupGrant? setup = await db.SetupGrants.SingleOrDefaultAsync(x => x.Id == setupId && x.BrowserSessionId == browserId, cancellationToken).ConfigureAwait(false);
            StationBrowserSession? browser = await db.BrowserSessions.SingleOrDefaultAsync(x => x.Id == browserId, cancellationToken).ConfigureAwait(false);
            if (browser is not null && now < browser.LastObservedAtUtc)
            {
                return Conflict(browser.Generation);
            }
            if (setup is null || browser is null || !browser.PairingCurrent(now, this.options.ExternalEpoch))
            {
                return Rejected();
            }

            Station? station = await db.Stations.SingleOrDefaultAsync(x => x.Id == setup.StationId, cancellationToken).ConfigureAwait(false);
            if (station is null || station.Revoked)
            {
                return Rejected();
            }

            StationStaffCredential? credential = await db.Credentials.SingleOrDefaultAsync(x => x.StaffMemberId == setup.StaffMemberId, cancellationToken).ConfigureAwait(false);
            if ((credential?.Revision ?? 0) != setup.ExpectedCredentialRevision)
            {
                return Conflict();
            }

            StationBrowserSession[] sessions = await db.BrowserSessions.Where(x => x.StaffMemberId == setup.StaffMemberId).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (now < credential?.LastObservedAtUtc || sessions.Any(x => now < x.LastObservedAtUtc))
            {
                return Conflict();
            }

            if (!setup.Consume(now))
            {
                return Rejected();
            }

            if (credential is null)
            {
                db.Credentials.Add(new StationStaffCredential(db.CurrentScopeId, setup.StaffMemberId, material, now));
            }
            else
            {
                credential.Replace(material, now);
            }

            foreach (StationBrowserSession session in sessions)
            {
                session.Lock(now);
            }

            foreach (StationSetupGrant other in await db.SetupGrants.Where(x => x.StaffMemberId == setup.StaffMemberId && x.Id != setupId && !x.Revoked && x.ConsumedAtUtc == null).ToArrayAsync(cancellationToken).ConfigureAwait(false))
            {
                other.Revoke();
            }

            return new(StationCoreOutcome.Applied);
        }, cancellationToken);

    private TimeSpan Window => TimeSpan.FromMinutes(this.options.AttemptWindowMinutes);
    private TimeSpan Cooldown => TimeSpan.FromMinutes(this.options.CooldownMinutes);
    private static StationCoreResult Rejected() => new(StationCoreOutcome.Rejected);
    private static StationCoreResult Conflict(long? generation = null) => new(StationCoreOutcome.Conflict, Generation: generation);
    private static StationCoreResult DeviceFailure(StationBrowserSession browser) => browser.CooldownUntilUtc.HasValue
        ? new(StationCoreOutcome.Throttled, Generation: browser.Generation, RetryAfterUtc: browser.CooldownUntilUtc)
        : new(StationCoreOutcome.Rejected, Generation: browser.Generation);

    private async Task<StationCoreResult> RunAsync(Guid operationId, StationOperationKind kind, string fingerprint,
        DateTimeOffset now, Func<Task<StationCoreResult>> apply, CancellationToken ct)
    {
        if (!this.options.IsValid())
        {
            throw new InvalidOperationException("Invalid station store configuration.");
        }

        StationRules.Coordinates(db.CurrentScopeId, operationId);
        StationRules.Utc(now);
        db.RequirePostgreSql();
        if (!db.ScopeFilterEnabled || db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Station core requires a clean scoped unit of work.");
        }

        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await StationsTenantMutationLock.AcquireAsync(db, db.CurrentScopeId, ct).ConfigureAwait(false);
        StationsTenantLifecycleState? lifecycle = await db.Lifecycle.SingleOrDefaultAsync(ct).ConfigureAwait(false);
        if (lifecycle?.Closed == true)
        {
            return Rejected();
        }

        StationOperationReceipt? existing = await db.OperationReceipts.SingleOrDefaultAsync(x => x.Id == operationId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!existing.Matches((StationMutationKind)kind, fingerprint))
            {
                return Conflict();
            }

            if (kind == StationOperationKind.Unlock && existing.Outcome == StationMutationOutcome.Applied)
            {
                StationBrowserSession? current = await db.BrowserSessions.SingleOrDefaultAsync(x => x.ActorSessionId == existing.ActorSessionId && x.Generation == existing.Generation, ct).ConfigureAwait(false);
                if (current is null || !current.ActorCurrent(now, this.options.ExternalEpoch))
                {
                    return Conflict();
                }
            }
            return new((StationCoreOutcome)existing.Outcome, existing.ActorSessionId, existing.Generation, existing.RetryAfterUtc);
        }
        StationCoreResult result = await apply().ConfigureAwait(false);
        // Rejections are receipts too: commit device and credential counters, never throw to signal a wrong PIN.
        db.OperationReceipts.Add(new(db.CurrentScopeId, operationId, (StationMutationKind)kind, fingerprint,
            new((StationMutationOutcome)result.Outcome, result.ActorSessionId, result.Generation, result.RetryAfterUtc), now));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return result;
    }
    private static string Fingerprint(params object?[] values)
    {
        string canonical = string.Join("|", values.Select(v =>
        {
            string value = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
            return value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
        }));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
