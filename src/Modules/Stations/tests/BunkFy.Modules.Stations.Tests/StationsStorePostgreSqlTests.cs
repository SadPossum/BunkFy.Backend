namespace BunkFy.Modules.Stations.Tests;

using System.Security.Cryptography;
using BunkFy.Modules.Stations.Application;
using BunkFy.Modules.Stations.Contracts;
using BunkFy.Modules.Stations.Domain;
using BunkFy.Modules.Stations.Persistence;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class StationsStorePostgreSqlTests
{
    private const string TenantA = StationDomainTests.Tenant;
    private const string TenantB = "bb000000-0000-0000-0000-000000000002";
    private static readonly DateTimeOffset Now = StationDomainTests.Now;

    [Fact]
    [Trait("Category", "Docker")]
    public async Task Actual_migration_and_registered_store_prove_concurrency_scope_reset_and_replay()
    {
        Assert.Equal("true", Environment.GetEnvironmentVariable("GMA_REQUIRE_DOCKER_TESTS"));
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithName("bunkfy-stations-p1-" + Guid.NewGuid().ToString("N"))
            .WithLabel("bunkfy.test.grant", "STAFF-PIN-FIRST-JOB-P1").WithDatabase("stations_core").Build();
        await postgres.StartAsync();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Persistence:Provider"] = "PostgreSql", ["ConnectionStrings:PostgreSql"] = postgres.GetConnectionString() });
        builder.Services.AddScoped<TestScope>();
        builder.Services.AddScoped<IScopeContext>(sp => sp.GetRequiredService<TestScope>());
        builder.Services.AddSingleton<IStationPepperProvider, StationPinVerifierTests.TestPeppers>();
        builder.Services.AddStationsCore(o => { o.ExternalEpoch = 1; o.PepperVersion = "test-v1"; });
        builder.AddStationsPersistence();
        await using ServiceProvider services = builder.Services.BuildServiceProvider();
        using (var scope = Scope(services, TenantA))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            await db.Database.MigrateAsync();
            Assert.Single(await db.Database.GetAppliedMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        }
        StationPinMaterial material = Assert.IsType<StationPinMaterial>(await services.GetRequiredService<IStationPinVerifier>().CreateAsync("000001"));
        Seed a = await SeedAsync(services, TenantA, material);
        Seed b = await SeedAsync(services, TenantB, material);
        // Actual independent scoped DbContexts/connections compete for one persisted credential budget.
        StationCoreResult[] failures = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Unlock(services, TenantA, a, Guid.NewGuid(), 1, 1, "000002")));
        Assert.Equal(4, failures.Count(x => x.Outcome == StationCoreOutcome.Rejected));
        Assert.Equal(4, failures.Count(x => x.Outcome == StationCoreOutcome.Throttled));
        using (var scope = Scope(services, TenantA))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            Assert.Equal(5, (await db.Credentials.SingleAsync()).FailureCount);
            Assert.Equal(5, (await db.BrowserSessions.SingleAsync()).AttemptCount);
            Assert.Equal(8, await db.OperationReceipts.CountAsync());
        }
        // Other tenant is unaffected; two simultaneous unlocks cannot both replace the same generation.
        Guid successfulOperation = Guid.NewGuid();
        StationCoreResult[] unlockRace = await Task.WhenAll(
            Unlock(services, TenantB, b, successfulOperation, 1, 1, "000001"),
            Unlock(services, TenantB, b, Guid.NewGuid(), 1, 1, "000001"));
        Assert.Single(unlockRace, x => x.Outcome == StationCoreOutcome.Applied);
        Assert.Single(unlockRace, x => x.Outcome == StationCoreOutcome.Conflict);
        StationCoreResult winner = unlockRace.Single(x => x.Outcome == StationCoreOutcome.Applied);
        using (var scope = Scope(services, TenantB))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            PostgresException missingCredentialRevision = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stations.browser_sessions SET \"CredentialRevision\" = NULL WHERE \"ScopeId\" = {TenantB} AND \"Id\" = {b.Browser}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, missingCredentialRevision.SqlState);
            Assert.Equal("CK_browser_actor", missingCredentialRevision.ConstraintName);
            PostgresException missingGrantRevision = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stations.browser_sessions SET \"GrantRevision\" = NULL WHERE \"ScopeId\" = {TenantB} AND \"Id\" = {b.Browser}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, missingGrantRevision.SqlState);
            Assert.Equal("CK_browser_actor", missingGrantRevision.ConstraintName);
        }
        using (var scope = Scope(services, TenantB))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            StationOperationReceipt receipt = await db.OperationReceipts.SingleAsync(x => x.Outcome == StationMutationOutcome.Applied);
            successfulOperation = receipt.Id;
        }
        StationCoreResult replay = await Unlock(services, TenantB, b, successfulOperation, 1, 1, "000001");
        Assert.Equal(winner, replay);
        Assert.Equal(StationCoreOutcome.Conflict, (await Unlock(services, TenantB, b, successfulOperation, 99, 1, "000001")).Outcome);
        // Wrong tenant sees no target; no cloned context or disabled tenant filter is used.
        Assert.Equal(StationCoreOutcome.Rejected, (await Unlock(services, TenantA, b, Guid.NewGuid(), 1, 1, "000001")).Outcome);
        using (var scope = Scope(services, TenantB))
        {
            var store = scope.ServiceProvider.GetRequiredService<IStationsStore>();
            Assert.Equal(StationCoreOutcome.Applied, (await store.LockCoreAsync(Guid.NewGuid(), b.Station, b.Browser, winner.Generation!.Value, Now)).Outcome);
        }
        using (var scope = Scope(services, TenantB))
        {
            StationBrowserSession device = await scope.ServiceProvider.GetRequiredService<StationsDbContext>().BrowserSessions.SingleAsync();
            Assert.True(device.PairingCurrent(Now, 1));
            Assert.Null(device.ActorSessionId);
            Assert.Equal(3, device.Generation);
        }
        Assert.Equal(StationCoreOutcome.Conflict, (await Unlock(services, TenantB, b, successfulOperation, 1, 1, "000001")).Outcome);
        StationCoreResult again = await Unlock(services, TenantB, b, Guid.NewGuid(), 3, 1, "000001");
        Assert.Equal(StationCoreOutcome.Applied, again.Outcome);
        // Reset serializes against unlock and invalidates every active session while preserving history.
        using (var scope = Scope(services, TenantB))
        {
            Assert.Equal(StationCoreOutcome.Applied, (await scope.ServiceProvider.GetRequiredService<IStationsStore>()
                .RevokeCredentialCoreAsync(Guid.NewGuid(), b.Staff, 1, Now)).Outcome);
        }

        using (var scope = Scope(services, TenantB))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            Assert.True((await db.Credentials.SingleAsync()).Revoked);
            Assert.Empty((await db.Credentials.SingleAsync()).Verifier);
            Assert.Null((await db.BrowserSessions.SingleAsync()).ActorSessionId);
        }
        Assert.Equal(StationCoreOutcome.Rejected, (await Unlock(services, TenantB, b, Guid.NewGuid(), 5, 2, "000001")).Outcome);
        // Redeem a one-use current revision grant in the actual database; second operation cannot consume it.
        Guid setupId = Guid.NewGuid();
        using (var scope = Scope(services, TenantB))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            db.SetupGrants.Add(new(setupId, TenantB, b.Station, b.Browser, b.Property, b.Staff,
                StationActorKind.StationOnly, 2, Now, Now.AddMinutes(10)));
            await db.SaveChangesAsync();
        }
        Guid redeemId = Guid.NewGuid();
        using (var scope = Scope(services, TenantB))
        {
            Assert.Equal(StationCoreOutcome.Applied, (await scope.ServiceProvider.GetRequiredService<IStationsStore>()
                .RedeemSetupCoreAsync(redeemId, setupId, b.Browser, material, Now)).Outcome);
        }

        using (var scope = Scope(services, TenantB))
        {
            var store = scope.ServiceProvider.GetRequiredService<IStationsStore>();
            Assert.Equal(StationCoreOutcome.Applied, (await store.RedeemSetupCoreAsync(redeemId, setupId, b.Browser, material, Now)).Outcome);
            Assert.Equal(StationCoreOutcome.Conflict, (await store.RedeemSetupCoreAsync(Guid.NewGuid(), setupId, b.Browser, material, Now)).Outcome);
        }
        StationCoreResult stationOnly = await Unlock(services, TenantB, b, Guid.NewGuid(), 5, 3, "000001");
        Assert.Equal(StationCoreOutcome.Applied, stationOnly.Outcome);
        using (var scope = Scope(services, TenantB))
        {
            Assert.Equal(StationCoreOutcome.Applied, (await scope.ServiceProvider.GetRequiredService<IStationsStore>()
                .RevokeGrantCoreAsync(Guid.NewGuid(), b.Property, b.Staff, 1, Now)).Outcome);
        }

        Assert.Equal(StationCoreOutcome.Conflict, (await Unlock(services, TenantB, b, Guid.NewGuid(), 7, 3, "000001")).Outcome);
        using (var scope = Scope(services, TenantB))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            db.BrowserSessions.Add(new(Guid.NewGuid(), TenantB, b.Station, Guid.NewGuid(), Digest(), Now, Now.AddDays(1), 1));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        using (var scope = Scope(services, TenantA))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            db.Stations.Add(new(Guid.NewGuid(), TenantB, Guid.NewGuid(), "Wrong tenant"));
            await Assert.ThrowsAsync<ScopeWriteGuardException>(() => db.SaveChangesAsync());
        }
        // Missing pepper and stale revisions do not consume either persisted failure budget.
        Seed unavailable = await SeedAsync(services, TenantA, material);
        var peppers = (StationPinVerifierTests.TestPeppers)services.GetRequiredService<IStationPepperProvider>();
        peppers.Missing = true;
        Assert.Equal(StationCoreOutcome.Unavailable, (await Unlock(services, TenantA, unavailable, Guid.NewGuid(), 1, 1, "000001")).Outcome);
        peppers.Missing = false;
        Assert.Equal(StationCoreOutcome.Conflict, (await Unlock(services, TenantA, unavailable, Guid.NewGuid(), 1, 99, "000001")).Outcome);
        using (var scope = Scope(services, TenantA))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            Assert.Equal(0, (await db.BrowserSessions.SingleAsync(x => x.Id == unavailable.Browser)).AttemptCount);
            Assert.Equal(0, (await db.Credentials.SingleAsync(x => x.StaffMemberId == unavailable.Staff)).FailureCount);
        }
        // Reset and unlock contend through independent real connections; reset always leaves no usable actor.
        Seed resetRace = await SeedAsync(services, TenantA, material);
        async Task<StationCoreResult> Reset()
        {
            using var scope = Scope(services, TenantA);
            return await scope.ServiceProvider.GetRequiredService<IStationsStore>().RevokeCredentialCoreAsync(Guid.NewGuid(), resetRace.Staff, 1, Now);
        }
        await Task.WhenAll(Unlock(services, TenantA, resetRace, Guid.NewGuid(), 1, 1, "000001"), Reset());
        using (var scope = Scope(services, TenantA))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            Assert.True((await db.Credentials.SingleAsync(x => x.StaffMemberId == resetRace.Staff)).Revoked);
            Assert.Null((await db.BrowserSessions.SingleAsync(x => x.Id == resetRace.Browser)).ActorSessionId);
        }
        // Independent device budget catches changing nonexistent identities, without touching another Staff's counter.
        Seed rotating = await SeedAsync(services, TenantA, material);
        for (int i = 0; i < 21; i++)
        {
            var unknown = rotating with { Staff = Guid.NewGuid() };
            StationCoreResult result = await Unlock(services, TenantA, unknown, Guid.NewGuid(), 1, 1, "000001");
            Assert.Equal(i >= 19 ? StationCoreOutcome.Throttled : StationCoreOutcome.Rejected, result.Outcome);
        }
        using (var scope = Scope(services, TenantA))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            StationBrowserSession browser = await db.BrowserSessions.SingleAsync(x => x.Id == rotating.Browser);
            Assert.Equal(20, browser.AttemptCount);
            Assert.Equal(Now.AddMinutes(15), browser.CooldownUntilUtc);
            Assert.Equal(0, (await db.Credentials.SingleAsync(x => x.StaffMemberId == rotating.Staff)).FailureCount);
        }
        using (var scope = Scope(services, TenantA))
        {
            var store = scope.ServiceProvider.GetRequiredService<IStationsStore>();
            Assert.Equal(StationCoreOutcome.Applied, (await store.RevokeStationCoreAsync(Guid.NewGuid(), rotating.Station, Now)).Outcome);
        }
        Assert.Equal(StationCoreOutcome.Rejected, (await Unlock(services, TenantA, rotating, Guid.NewGuid(), 2, 1, "000001")).Outcome);

        // Switching clears A even when B cannot unlock; returned generation makes a new attempt possible.
        foreach (string failure in new[] { "wrong", "missing", "revoked", "unavailable" })
        {
            Seed actorA = await SeedAsync(services, TenantA, material);
            Seed actorB = actorA with { Staff = Guid.NewGuid() };
            if (failure != "missing")
            {
                using var scope = Scope(services, TenantA);
                var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
                var credential = new StationStaffCredential(TenantA, actorB.Staff, material, Now);
                if (failure == "revoked")
                {
                    credential.Revoke(Now);
                }
                db.Credentials.Add(credential);
                db.CheckInGrants.Add(new(TenantA, actorB.Property, actorB.Staff));
                await db.SaveChangesAsync();
            }
            Guid originalAttempt = Guid.NewGuid();
            StationCoreResult activeA = await Unlock(services, TenantA, actorA, originalAttempt, 1, 1, "000001");
            Assert.Equal(StationCoreOutcome.Applied, activeA.Outcome);
            peppers.Missing = failure == "unavailable";
            Guid failedAttempt = Guid.NewGuid();
            StationCoreResult failedB = await Unlock(services, TenantA, actorB, failedAttempt, activeA.Generation!.Value,
                failure == "revoked" ? 2 : 1, failure == "wrong" ? "000002" : "000001");
            peppers.Missing = false;
            Assert.Equal(failure == "unavailable" ? StationCoreOutcome.Unavailable : StationCoreOutcome.Rejected, failedB.Outcome);
            Assert.Equal(3, failedB.Generation);
            using (var scope = Scope(services, TenantA))
            {
                var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
                StationBrowserSession device = await db.BrowserSessions.SingleAsync(x => x.Id == actorA.Browser);
                Assert.Null(device.StaffMemberId);
                Assert.Null(device.ActorSessionId);
                Assert.True(device.PairingCurrent(Now, 1));
                Assert.Equal(3, device.Generation);
                Assert.Equal(failure == "unavailable" ? 0 : 1, device.AttemptCount);
            }
            Assert.Equal(StationCoreOutcome.Conflict, (await Unlock(services, TenantA, actorA, originalAttempt, 1, 1, "000001")).Outcome);
            Assert.Equal(StationCoreOutcome.Conflict, (await Unlock(services, TenantA, actorA, Guid.NewGuid(), 2, 1, "000001")).Outcome);
            // Reusing an attempt ID with corrected digits is an outcome lookup, never another PIN verification.
            Assert.Equal(failedB, await Unlock(services, TenantA, actorB, failedAttempt, 2, failure == "revoked" ? 2 : 1, "000001"));
            Seed recovering = failure is "wrong" or "unavailable" ? actorB : actorA;
            StationCoreResult recovered = await Unlock(services, TenantA, recovering, Guid.NewGuid(), failedB.Generation!.Value, 1, "000001");
            Assert.Equal(StationCoreOutcome.Applied, recovered.Outcome);
            Assert.Equal(4, recovered.Generation);
            using (var scope = Scope(services, TenantA))
            {
                var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
                StationBrowserSession device = await db.BrowserSessions.SingleAsync(x => x.Id == actorA.Browser);
                Assert.Equal(recovering.Staff, device.StaffMemberId);
                Assert.Equal(failure == "unavailable" ? 0 : 1, device.AttemptCount);
                Assert.Equal(0, (await db.Credentials.SingleAsync(x => x.StaffMemberId == recovering.Staff)).FailureCount);
            }
        }

        // Independent connections with an earlier captured reset time have an explicit conflict, never a partial reset/500.
        Seed skew = await SeedAsync(services, TenantA, material);
        Guid staleSetup = Guid.NewGuid();
        using (var scope = Scope(services, TenantA))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            db.SetupGrants.Add(new(staleSetup, TenantA, skew.Station, skew.Browser, skew.Property, skew.Staff,
                StationActorKind.StationOnly, 1, Now, Now.AddMinutes(10)));
            await db.SaveChangesAsync();
            var store = scope.ServiceProvider.GetRequiredService<IStationsStore>();
            Assert.Equal(StationCoreOutcome.Applied, (await store.TryUnlockCoreAsync(Guid.NewGuid(), skew.Station,
                skew.Browser, skew.Staff, 1, 1, StationAuthorityKind.StationOnly, 1, "000001", Now.AddSeconds(2))).Outcome);
        }
        using (var scope = Scope(services, TenantA))
        {
            var store = scope.ServiceProvider.GetRequiredService<IStationsStore>();
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            StationBrowserSession beforeBrowser = await db.BrowserSessions.AsNoTracking().SingleAsync(x => x.Id == skew.Browser);
            StationStaffCredential beforeCredential = await db.Credentials.AsNoTracking().SingleAsync(x => x.StaffMemberId == skew.Staff);
            Assert.Equal(StationCoreOutcome.Conflict, (await store.TryUnlockCoreAsync(Guid.NewGuid(), skew.Station,
                skew.Browser, skew.Staff, 2, 99, StationAuthorityKind.StationOnly, 1, "000001", Now.AddSeconds(2))).Outcome);
            Assert.Equal(StationCoreOutcome.Conflict, (await store.TryUnlockCoreAsync(Guid.NewGuid(), skew.Station,
                skew.Browser, skew.Staff, 2, 1, StationAuthorityKind.StationOnly, 99, "000001", Now.AddSeconds(2))).Outcome);
            Assert.Equal(StationCoreOutcome.Conflict, (await store.TryUnlockCoreAsync(Guid.NewGuid(), skew.Station,
                skew.Browser, skew.Staff, 2, 1, StationAuthorityKind.StationOnly, 1, "000001", Now.AddSeconds(1))).Outcome);
            Assert.Equal(StationCoreOutcome.Conflict, (await store.LockCoreAsync(Guid.NewGuid(), skew.Station, skew.Browser, 2, Now.AddSeconds(1))).Outcome);
            Assert.Equal(StationCoreOutcome.Conflict, (await store.RedeemSetupCoreAsync(Guid.NewGuid(), staleSetup, skew.Browser, material, Now.AddSeconds(1))).Outcome);
            Assert.Equal(StationCoreOutcome.Conflict, (await store.RevokeCredentialCoreAsync(Guid.NewGuid(), skew.Staff, 1, Now.AddSeconds(1))).Outcome);
            Assert.Equal(StationCoreOutcome.Conflict, (await store.RevokeStationCoreAsync(Guid.NewGuid(), skew.Station, Now.AddSeconds(1))).Outcome);
            Assert.Equal(StationCoreOutcome.Conflict, (await store.RevokeGrantCoreAsync(Guid.NewGuid(), skew.Property, skew.Staff, 1, Now.AddSeconds(1))).Outcome);
            StationBrowserSession afterBrowser = await db.BrowserSessions.AsNoTracking().SingleAsync(x => x.Id == skew.Browser);
            StationStaffCredential afterCredential = await db.Credentials.AsNoTracking().SingleAsync(x => x.StaffMemberId == skew.Staff);
            Assert.Equal(beforeBrowser.Generation, afterBrowser.Generation);
            Assert.Equal(beforeBrowser.ActorSessionId, afterBrowser.ActorSessionId);
            Assert.Equal(beforeBrowser.StaffMemberId, afterBrowser.StaffMemberId);
            Assert.Equal(beforeBrowser.AttemptCount, afterBrowser.AttemptCount);
            Assert.Equal(beforeBrowser.ActorIdleExpiresAtUtc, afterBrowser.ActorIdleExpiresAtUtc);
            Assert.Equal(beforeCredential.Revision, afterCredential.Revision);
            Assert.Equal(beforeCredential.FailureCount, afterCredential.FailureCount);
            Assert.True(beforeCredential.Verifier == afterCredential.Verifier && beforeCredential.Salt == afterCredential.Salt);
            Assert.Null((await db.SetupGrants.AsNoTracking().SingleAsync(x => x.Id == staleSetup)).ConsumedAtUtc);
            Assert.False((await db.CheckInGrants.AsNoTracking().SingleAsync(x => x.StaffMemberId == skew.Staff)).Revoked);
            Assert.Equal(StationCoreOutcome.Applied, (await store.RevokeCredentialCoreAsync(Guid.NewGuid(), skew.Staff, 1, Now.AddSeconds(3))).Outcome);
        }
        using (var scope = Scope(services, TenantA))
        {
            var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
            Assert.True((await db.Credentials.SingleAsync(x => x.StaffMemberId == skew.Staff)).Revoked);
            Assert.Null((await db.BrowserSessions.SingleAsync(x => x.Id == skew.Browser)).ActorSessionId);
            Assert.False((await db.Stations.SingleAsync(x => x.Id == skew.Station)).Revoked);
            Assert.False((await db.CheckInGrants.SingleAsync(x => x.StaffMemberId == skew.Staff)).Revoked);
        }
    }

    [Fact]
    public void Unsupported_provider_is_rejected_before_context_registration()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Persistence:Provider"] = "SqlServer" });
        Assert.Throws<NotSupportedException>(() => builder.AddStationsPersistence());
        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(StationsDbContext));
    }
    private static async Task<Seed> SeedAsync(ServiceProvider services, string tenant, StationPinMaterial material)
    {
        var seed = new Seed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        using var scope = Scope(services, tenant);
        var db = scope.ServiceProvider.GetRequiredService<StationsDbContext>();
        db.Stations.Add(new(seed.Station, tenant, seed.Property, "Synthetic station"));
        db.BrowserSessions.Add(new(seed.Browser, tenant, seed.Station, seed.Property, Digest(), Now, Now.AddDays(30), 1));
        db.Credentials.Add(new(tenant, seed.Staff, material, Now));
        db.CheckInGrants.Add(new(tenant, seed.Property, seed.Staff));
        await db.SaveChangesAsync();
        return seed;
    }
    private static async Task<StationCoreResult> Unlock(ServiceProvider services, string tenant, Seed seed,
        Guid operation, long generation, long credentialRevision, string pin)
    {
        using var scope = Scope(services, tenant);
        return await scope.ServiceProvider.GetRequiredService<IStationsStore>().TryUnlockCoreAsync(operation, seed.Station,
            seed.Browser, seed.Staff, generation, credentialRevision, StationAuthorityKind.StationOnly, 1, pin, Now);
    }
    private static IServiceScope Scope(ServiceProvider provider, string tenant)
    {
        IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TestScope>().ScopeId = tenant;
        return scope;
    }
    private static string Digest() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private sealed record Seed(Guid Station, Guid Browser, Guid Property, Guid Staff);
    private sealed class TestScope : IScopeContext
    {
        public string? ScopeId { get; set; }
        public bool IsEnabled => true;
    }
}
