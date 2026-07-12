namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Credentials;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Credentials;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterIngressCredentialTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Issued_token_authenticates_only_its_scope_connection_and_active_lifetime()
    {
        Guid connectionId = Guid.NewGuid();
        AdapterConnection connection = CreateConnection(connectionId);
        FakeCredentialRepository credentials = new();
        AdapterIngressTokenService tokens = new();
        TestClock clock = new();
        var created = await new CreateAdapterIngressCredentialCommandHandler(
            new FakeConnectionRepository(connection), credentials, tokens, new TestScope(), clock,
            new FixedIds()).HandleAsync(
            new CreateAdapterIngressCredentialCommand(
                connection.PropertyId, connection.Id, "primary", Now.AddDays(30), "user:operator"),
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.StartsWith("bfi_v1_", created.Value.Token, StringComparison.Ordinal);
        Assert.DoesNotContain(created.Value.Token, created.Value.Credential.ToString(), StringComparison.Ordinal);
        Assert.Single(credentials.Items);

        AdapterIngressAuthenticator authenticator = new(
            new FakeConnectionRepository(connection), credentials, tokens, new TestScope(), clock);
        var authenticated = await authenticator.AuthenticateAsync(
            connectionId, created.Value.Token, AdapterExecutionMode.Push, CancellationToken.None);
        var wrongMode = await authenticator.AuthenticateAsync(
            connectionId, created.Value.Token, AdapterExecutionMode.RemotePolling, CancellationToken.None);
        var wrongConnection = await authenticator.AuthenticateAsync(
            Guid.NewGuid(), created.Value.Token, AdapterExecutionMode.Push, CancellationToken.None);
        var malformed = await authenticator.AuthenticateAsync(
            connectionId, "bfi_v1_invalid", AdapterExecutionMode.Push, CancellationToken.None);
        string tamperedToken = created.Value.Token[..^1] +
            (created.Value.Token[^1] == 'A' ? "B" : "A");
        var tampered = await authenticator.AuthenticateAsync(
            connectionId, tamperedToken, AdapterExecutionMode.Push, CancellationToken.None);

        Assert.True(authenticated.IsSuccess);
        Assert.Equal(created.Value.Credential.CredentialId, authenticated.Value.CredentialId);
        Assert.Equal(1, credentials.AuthenticationMarks);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, wrongConnection.Error);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, wrongMode.Error);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, malformed.Error);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, tampered.Error);

        Assert.True(credentials.Items[0].Revoke(1, "user:operator", Now.AddMinutes(1)).IsSuccess);
        var revoked = await authenticator.AuthenticateAsync(
            connectionId, created.Value.Token, AdapterExecutionMode.Push, CancellationToken.None);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, revoked.Error);
    }

    [Fact]
    public async Task Remote_polling_credential_authenticates_only_the_remote_control_mode()
    {
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", Guid.NewGuid(), "fake.http", AdapterExecutionMode.RemotePolling,
            IngestionConflictPolicy.SuggestionsOnly, "configuration://remote", null, Now).Value;
        FakeCredentialRepository credentials = new();
        AdapterIngressTokenService tokens = new();
        TestClock clock = new();
        var created = await new CreateAdapterIngressCredentialCommandHandler(
            new FakeConnectionRepository(connection), credentials, tokens, new TestScope(), clock,
            new FixedIds()).HandleAsync(
            new CreateAdapterIngressCredentialCommand(
                connection.PropertyId, connection.Id, "remote", Now.AddDays(30), "user:operator"),
            CancellationToken.None);
        Assert.True(created.IsSuccess);

        AdapterIngressAuthenticator authenticator = new(
            new FakeConnectionRepository(connection), credentials, tokens, new TestScope(), clock);
        var remote = await authenticator.AuthenticateAsync(
            connection.Id, created.Value.Token, AdapterExecutionMode.RemotePolling, CancellationToken.None);
        var directPush = await authenticator.AuthenticateAsync(
            connection.Id, created.Value.Token, AdapterExecutionMode.Push, CancellationToken.None);

        Assert.True(remote.IsSuccess);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, directPush.Error);
        Assert.Equal(1, credentials.AuthenticationMarks);
    }

    [Fact]
    public async Task Creation_enforces_active_rotation_limit_before_issuing_material()
    {
        AdapterConnection connection = CreateConnection(Guid.NewGuid());
        FakeCredentialRepository credentials = new() { ActiveCount = 5 };
        var result = await new CreateAdapterIngressCredentialCommandHandler(
            new FakeConnectionRepository(connection), credentials, new AdapterIngressTokenService(),
            new TestScope(), new TestClock(), new FixedIds()).HandleAsync(
            new CreateAdapterIngressCredentialCommand(
                connection.PropertyId, connection.Id, "overflow", null, "user:operator"),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.IngressCredentialLimitReached, result.Error);
        Assert.Empty(credentials.Items);
    }

    [Fact]
    public async Task Direct_ingress_credentials_cannot_be_issued_for_polling_connections()
    {
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", Guid.NewGuid(), "fake.http", AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly, "configuration://main", null, Now).Value;
        FakeCredentialRepository credentials = new();
        var result = await new CreateAdapterIngressCredentialCommandHandler(
            new FakeConnectionRepository(connection), credentials, new AdapterIngressTokenService(),
            new TestScope(), new TestClock(), new FixedIds()).HandleAsync(
            new CreateAdapterIngressCredentialCommand(
                connection.PropertyId, connection.Id, "invalid", null, "user:operator"),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.IngressCredentialsRequirePushMode, result.Error);
        Assert.Empty(credentials.Items);
    }

    private static AdapterConnection CreateConnection(Guid id) => AdapterConnection.Create(
        id, "tenant-a", Guid.NewGuid(), "fake.http", AdapterExecutionMode.Push,
        IngestionConflictPolicy.SuggestionsOnly, "configuration://main", null, Now).Value;

    private sealed class FakeConnectionRepository(AdapterConnection connection) : IAdapterConnectionRepository
    {
        public Task<AdapterConnection?> GetAsync(Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<AdapterConnection?>(connection.Id == connectionId ? connection : null);

        public Task<AdapterConnection?> GetAsync(
            Guid propertyId, Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<AdapterConnection?>(
                connection.PropertyId == propertyId && connection.Id == connectionId ? connection : null);

        public Task AddAsync(AdapterConnection added, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeCredentialRepository : IAdapterIngressCredentialRepository
    {
        public List<AdapterIngressCredential> Items { get; } = [];
        public int ActiveCount { get; init; }
        public int AuthenticationMarks { get; private set; }

        public Task<int?> GetAvailableSlotAsync(
            Guid connectionId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            int active = this.ActiveCount + this.Items.Count(item =>
                item.ConnectionId == connectionId && item.CanAuthenticate(nowUtc));
            return Task.FromResult<int?>(active >= AdapterIngressCredential.MaximumActiveCredentialsPerConnection
                ? null
                : active + 1);
        }

        public Task<AdapterIngressCredential?> GetAsync(
            Guid connectionId, Guid credentialId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(item =>
                item.ConnectionId == connectionId && item.Id == credentialId));

        public Task<AdapterIngressCredential?> GetForAuthenticationAsync(
            Guid connectionId, Guid credentialId, CancellationToken cancellationToken) =>
            this.GetAsync(connectionId, credentialId, cancellationToken);

        public Task AddAsync(AdapterIngressCredential credential, CancellationToken cancellationToken)
        {
            this.Items.Add(credential);
            return Task.CompletedTask;
        }

        public Task MarkAuthenticatedAsync(
            Guid credentialId, DateTimeOffset authenticatedAtUtc, CancellationToken cancellationToken)
        {
            this.AuthenticationMarks++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestScope : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FixedIds : IIdGenerator
    {
        public Guid NewId() => Guid.Parse("c1000000-0000-0000-0000-000000000001");
    }
}
