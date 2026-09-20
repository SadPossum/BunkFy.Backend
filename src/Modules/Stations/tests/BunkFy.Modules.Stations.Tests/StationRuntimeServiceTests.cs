namespace BunkFy.Modules.Stations.Tests;

using System.Security.Cryptography;
using BunkFy.Modules.Stations.Application;
using BunkFy.Modules.Stations.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class StationRuntimeServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA ")]
    public async Task Noncanonical_credentials_are_invalid_before_bootstrap(string credential)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var bootstrap = new Bootstrap(null);
        var runtime = new StationRuntimeService(services.GetRequiredService<IServiceScopeFactory>(), bootstrap, new Clock());
        Assert.Equal(new(StationSessionState.Invalid), await runtime.ReadAsync(credential));
        var roster = await runtime.RosterAsync(credential);
        Assert.Equal(StationSessionState.Invalid, roster.State);
        Assert.Empty(roster.Items);
        Assert.Equal(0, bootstrap.Reads);
    }

    [Fact]
    public async Task Canonical_random_bytes_are_hashed_and_unknown_lookup_returns_no_coordinates()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string credential = Encode(bytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), StationCredentialEncoding.Digest(credential));
        using var services = new ServiceCollection().BuildServiceProvider();
        var bootstrap = new Bootstrap(null);
        var runtime = new StationRuntimeService(services.GetRequiredService<IServiceScopeFactory>(), bootstrap, new Clock());
        Assert.Equal(new(StationSessionState.Invalid), await runtime.ReadAsync(credential));
        Assert.Equal(1, bootstrap.Reads);
    }

    [Fact]
    public async Task Bootstrap_failure_is_unavailable_and_cancellation_is_not_swallowed()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var bootstrap = new Bootstrap(null) { Unavailable = true };
        var runtime = new StationRuntimeService(services.GetRequiredService<IServiceScopeFactory>(), bootstrap, new Clock());
        string credential = Encode(new byte[32]);
        Assert.Equal(new(StationSessionState.Unavailable), await runtime.ReadAsync(credential));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runtime.ReadAsync(credential, cancellation.Token));
        Assert.Equal(1, bootstrap.Reads);
    }

    [Fact]
    public async Task Scoped_revalidation_uses_a_new_scope_and_never_overwrites_ambient_or_invokes_owners_after_rejection()
    {
        string discovered = Guid.NewGuid().ToString("D"), ambient = Guid.NewGuid().ToString("D");
        var device = new StationDeviceReference(discovered, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var readScopes = new List<string?>();
        var services = new ServiceCollection();
        services.AddScoped<TestScope>();
        services.AddScoped<IScopeContextAccessor>(sp => sp.GetRequiredService<TestScope>());
        services.AddScoped<IScopeContext>(sp => sp.GetRequiredService<TestScope>());
        services.AddScoped<IStationRuntimeStore>(sp => new InvalidStore(sp.GetRequiredService<IScopeContext>(), readScopes));
        using var provider = services.BuildServiceProvider();
        using var request = provider.CreateScope();
        var accessor = request.ServiceProvider.GetRequiredService<IScopeContextAccessor>();
        accessor.SetScope(ambient);
        var runtime = new StationRuntimeService(request.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), new Bootstrap(device), new Clock());
        Assert.Equal(new(StationSessionState.Invalid), await runtime.ReadAsync(Encode(new byte[32])));
        Assert.Equal(ambient, accessor.ScopeId);
        Assert.Equal(discovered, Assert.Single(readScopes));
        // No coordinator/owners are registered: invalid device revalidation must never resolve them.
    }

    internal static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private sealed class Bootstrap(StationDeviceReference? device) : IStationCredentialBootstrap
    {
        public int Reads { get; private set; }
        public bool Unavailable { get; init; }
        public Task<StationDeviceReference?> FindAsync(string opaqueCredential, CancellationToken cancellationToken = default)
        {
            this.Reads++;
            return this.Unavailable ? throw new InvalidOperationException("Synthetic unavailable") : Task.FromResult(device);
        }
    }
    private sealed class Clock : ISystemClock { public DateTimeOffset UtcNow => StationDomainTests.Now; }
    private sealed class TestScope : IScopeContextAccessor
    {
        public bool IsEnabled { get; private set; }
        public string? ScopeId { get; private set; }
        public void SetScope(string scopeId) { this.IsEnabled = true; this.ScopeId = scopeId; }
        public void ClearScope() { this.IsEnabled = false; this.ScopeId = null; }
    }
    private sealed class InvalidStore(IScopeContext scope, List<string?> reads) : IStationRuntimeStore
    {
        public Task<StationRuntimeFacts?> ReadAsync(StationDeviceReference device, string opaqueCredential, Guid? selectedStaff,
            DateTimeOffset now, CancellationToken cancellationToken = default)
        { reads.Add(scope.ScopeId); return Task.FromResult<StationRuntimeFacts?>(null); }
        public Task<StationSetupFacts?> ReadSetupAsync(StationDeviceReference device, Guid setupId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<StationCoreResult?> ReadSetupOutcomeAsync(Guid operationId, StationSetupFacts setup, StationDeviceReference device, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<StationCoreResult> ForegroundActivityAsync(Guid operationId, StationDeviceReference device, string opaqueCredential, StationActorCoordinate actor,
            StationCredentialFacts credential, long? grantRevision, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
