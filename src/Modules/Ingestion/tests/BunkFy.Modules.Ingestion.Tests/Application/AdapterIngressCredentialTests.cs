namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Credentials;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
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
            TestIngestionExecution.Create(new FakeConnectionRepository(connection)),
            credentials, new FakeConnectionManagementOperationRepository(),
            tokens, new TestDescriptors(),
            new TestScope(), clock).HandleAsync(
            new CreateAdapterIngressCredentialCommand(
                Guid.NewGuid(), connection.PropertyId, connection.Id,
                "primary", Now.AddDays(30), "user:operator"),
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.Equal(
            AdapterIngressCredentialIssuanceOutcome.Issued,
            created.Value.Outcome);
        string token = Assert.IsType<string>(created.Value.Token);
        Assert.StartsWith("bfi_v1_", token, StringComparison.Ordinal);
        Assert.DoesNotContain(token, created.Value.Credential.ToString(), StringComparison.Ordinal);
        Assert.Single(credentials.Items);

        AdapterIngressAuthenticator authenticator = new(
            new FakeConnectionRepository(connection), credentials, tokens, new TestDescriptors(),
            new TestScope(), clock);
        var authenticated = await authenticator.AuthenticateAsync(
            connectionId, token, AdapterExecutionMode.Push, CancellationToken.None);
        var wrongMode = await authenticator.AuthenticateAsync(
            connectionId, token, AdapterExecutionMode.RemotePolling, CancellationToken.None);
        var wrongConnection = await authenticator.AuthenticateAsync(
            Guid.NewGuid(), token, AdapterExecutionMode.Push, CancellationToken.None);
        var malformed = await authenticator.AuthenticateAsync(
            connectionId, "bfi_v1_invalid", AdapterExecutionMode.Push, CancellationToken.None);
        string tamperedToken = token[..^1] +
            (token[^1] == 'A' ? "B" : "A");
        var tampered = await authenticator.AuthenticateAsync(
            connectionId, tamperedToken, AdapterExecutionMode.Push, CancellationToken.None);

        Assert.True(authenticated.IsSuccess);
        Assert.Equal(created.Value.Credential.CredentialId, authenticated.Value.CredentialId);
        Assert.Equal("fake.http", authenticated.Value.AdapterType);
        Assert.Equal(1, authenticated.Value.AdapterProtocolVersion);
        Assert.Equal(1, authenticated.Value.ConfigurationSchemaVersion);
        Assert.Equal("fake.http", authenticated.Value.SourceSystem);
        Assert.Equal("user:operator", authenticated.Value.CustomerOwner);
        Assert.Equal(1, credentials.AuthenticationMarks);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, wrongConnection.Error);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, wrongMode.Error);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, malformed.Error);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, tampered.Error);

        Assert.True(credentials.Items[0].Revoke(1, "user:operator", Now.AddMinutes(1)).IsSuccess);
        var revoked = await authenticator.AuthenticateAsync(
            connectionId, token, AdapterExecutionMode.Push, CancellationToken.None);
        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, revoked.Error);
    }

    [Fact]
    public async Task Credential_and_connection_must_match_the_resolved_tenant()
    {
        Guid connectionId = Guid.NewGuid();
        AdapterConnection connection = CreateConnection(connectionId);
        FakeCredentialRepository credentials = new();
        AdapterIngressTokenService tokens = new();
        TestClock clock = new();
        var created =
            await new CreateAdapterIngressCredentialCommandHandler(
                TestIngestionExecution.Create(new FakeConnectionRepository(connection)),
                credentials,
                new FakeConnectionManagementOperationRepository(),
                tokens,
                new TestDescriptors(),
                new TestScope("tenant-a"),
                clock).HandleAsync(
                    new CreateAdapterIngressCredentialCommand(
                        Guid.NewGuid(),
                        connection.PropertyId,
                        connection.Id,
                        "primary",
                        Now.AddDays(30),
                        "user:operator"),
                    CancellationToken.None);
        Assert.True(created.IsSuccess);
        string token = Assert.IsType<string>(created.Value.Token);

        AdapterIngressAuthenticator authenticator = new(
            new FakeConnectionRepository(connection),
            credentials,
            tokens,
            new TestDescriptors(),
            new TestScope("tenant-b"),
            clock);

        var result = await authenticator.AuthenticateAsync(
            connectionId,
            token,
            AdapterExecutionMode.Push,
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, result.Error);
        Assert.Equal(0, credentials.AuthenticationMarks);
    }

    [Fact]
    public async Task Expired_credential_is_rejected_without_recording_authentication()
    {
        Guid connectionId = Guid.NewGuid();
        AdapterConnection connection = CreateConnection(connectionId);
        FakeCredentialRepository credentials = new();
        AdapterIngressTokenService tokens = new();
        TestClock clock = new();
        var created = await new CreateAdapterIngressCredentialCommandHandler(
            TestIngestionExecution.Create(new FakeConnectionRepository(connection)),
            credentials,
            new FakeConnectionManagementOperationRepository(),
            tokens,
            new TestDescriptors(),
            new TestScope(),
            clock).HandleAsync(
                new CreateAdapterIngressCredentialCommand(
                    Guid.NewGuid(),
                    connection.PropertyId,
                    connection.Id,
                    "short-lived",
                    Now.AddMinutes(5),
                    "user:operator"),
                CancellationToken.None);
        Assert.True(created.IsSuccess);
        string token = Assert.IsType<string>(created.Value.Token);
        clock.UtcNow = Now.AddMinutes(5);

        AdapterIngressAuthenticator authenticator = new(
            new FakeConnectionRepository(connection),
            credentials,
            tokens,
            new TestDescriptors(),
            new TestScope(),
            clock);
        var result = await authenticator.AuthenticateAsync(
            connection.Id,
            token,
            AdapterExecutionMode.Push,
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, result.Error);
        Assert.Equal(0, credentials.AuthenticationMarks);
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
            TestIngestionExecution.Create(new FakeConnectionRepository(connection)),
            credentials, new FakeConnectionManagementOperationRepository(),
            tokens, new TestDescriptors(),
            new TestScope(), clock).HandleAsync(
            new CreateAdapterIngressCredentialCommand(
                Guid.NewGuid(), connection.PropertyId, connection.Id,
                "remote", Now.AddDays(30), "user:operator"),
            CancellationToken.None);
        Assert.True(created.IsSuccess);
        string token = Assert.IsType<string>(created.Value.Token);

        AdapterIngressAuthenticator authenticator = new(
            new FakeConnectionRepository(connection), credentials, tokens, new TestDescriptors(),
            new TestScope(), clock);
        var remote = await authenticator.AuthenticateAsync(
            connection.Id, token, AdapterExecutionMode.RemotePolling, CancellationToken.None);
        var directPush = await authenticator.AuthenticateAsync(
            connection.Id, token, AdapterExecutionMode.Push, CancellationToken.None);

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
            TestIngestionExecution.Create(new FakeConnectionRepository(connection)),
            credentials, new FakeConnectionManagementOperationRepository(),
            new AdapterIngressTokenService(),
            new TestDescriptors(), new TestScope(), new TestClock()).HandleAsync(
            new CreateAdapterIngressCredentialCommand(
                Guid.NewGuid(), connection.PropertyId, connection.Id,
                "overflow", null, "user:operator"),
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
            TestIngestionExecution.Create(new FakeConnectionRepository(connection)),
            credentials, new FakeConnectionManagementOperationRepository(),
            new AdapterIngressTokenService(),
            new TestDescriptors(), new TestScope(), new TestClock()).HandleAsync(
            new CreateAdapterIngressCredentialCommand(
                Guid.NewGuid(), connection.PropertyId, connection.Id,
                "invalid", null, "user:operator"),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.IngressCredentialsRequirePushMode, result.Error);
        Assert.Empty(credentials.Items);
    }

    [Fact]
    public async Task Disabled_connection_stops_authentication_without_marking_credential_use()
    {
        AdapterConnection connection = CreateConnection(Guid.NewGuid());
        FakeCredentialRepository credentials = new();
        AdapterIngressTokenService tokens = new();
        TestClock clock = new();
        var created = await new CreateAdapterIngressCredentialCommandHandler(
            TestIngestionExecution.Create(new FakeConnectionRepository(connection)),
            credentials,
            new FakeConnectionManagementOperationRepository(),
            tokens,
            new TestDescriptors(),
            new TestScope(),
            clock).HandleAsync(
                new CreateAdapterIngressCredentialCommand(
                    Guid.NewGuid(),
                    connection.PropertyId,
                    connection.Id,
                    "primary",
                    Now.AddDays(30),
                    "user:operator"),
                CancellationToken.None);
        Assert.True(created.IsSuccess);
        string token = Assert.IsType<string>(created.Value.Token);
        Assert.True(connection.Disable(connection.Version, Now.AddMinutes(1)).IsSuccess);

        AdapterIngressAuthenticator authenticator = new(
            new FakeConnectionRepository(connection),
            credentials,
            tokens,
            new TestDescriptors(),
            new TestScope(),
            clock);
        var authenticated = await authenticator.AuthenticateAsync(
            connection.Id,
            token,
            AdapterExecutionMode.Push,
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, authenticated.Error);
        Assert.Equal(0, credentials.AuthenticationMarks);
    }

    [Fact]
    public async Task Descriptor_version_drift_requires_credential_rotation()
    {
        AdapterConnection connection = CreateConnection(Guid.NewGuid());
        FakeCredentialRepository credentials = new();
        AdapterIngressTokenService tokens = new();
        TestClock clock = new();
        var created = await new CreateAdapterIngressCredentialCommandHandler(
            TestIngestionExecution.Create(new FakeConnectionRepository(connection)),
            credentials,
            new FakeConnectionManagementOperationRepository(),
            tokens,
            new TestDescriptors(),
            new TestScope(),
            clock).HandleAsync(
                new CreateAdapterIngressCredentialCommand(
                    Guid.NewGuid(),
                    connection.PropertyId,
                    connection.Id,
                    "primary",
                    Now.AddDays(30),
                    "user:operator"),
                CancellationToken.None);
        Assert.True(created.IsSuccess);
        string token = Assert.IsType<string>(created.Value.Token);

        AdapterIngressAuthenticator authenticator = new(
            new FakeConnectionRepository(connection),
            credentials,
            tokens,
            new TestDescriptors(protocolVersion: 2),
            new TestScope(),
            clock);
        var authenticated = await authenticator.AuthenticateAsync(
            connection.Id,
            token,
            AdapterExecutionMode.Push,
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.IngressCredentialUnauthorized, authenticated.Error);
        Assert.Equal(0, credentials.AuthenticationMarks);
    }

    [Fact]
    public async Task Creation_requires_an_operation_id_and_lifecycle_admission_without_binding_state()
    {
        AdapterConnection connection = CreateConnection(Guid.NewGuid());
        FakeCredentialRepository credentials = new();
        FakeConnectionManagementOperationRepository operations = new();
        CreateAdapterIngressCredentialCommand command = new(
            Guid.Empty,
            connection.PropertyId,
            connection.Id,
            "primary",
            null,
            "user:operator");

        var missingOperation = await CreateCredentialHandler(
            connection,
            credentials,
            operations).HandleAsync(command, CancellationToken.None);
        var lifecycleDenied = await CreateCredentialHandler(
            connection,
            credentials,
            operations,
            lifecyclePolicies:
            [
                new TestLifecyclePolicy(
                    IngestionTenantLifecycleDecision.Restricted)
            ]).HandleAsync(
                command with { OperationId = Guid.NewGuid() },
                CancellationToken.None);
        var parentIdentity = await CreateCredentialHandler(
            connection,
            credentials,
            operations).HandleAsync(
                command with { OperationId = connection.Id },
                CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationInvalid,
            missingOperation.Error);
        Assert.Equal(
            IngestionApplicationErrors.TenantLifecycleRestricted,
            lifecycleDenied.Error);
        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationInvalid,
            parentIdentity.Error);
        Assert.Empty(credentials.Items);
        Assert.Empty(operations.Items);
    }

    [Fact]
    public async Task Creation_exact_retry_returns_no_recoverable_token_and_changed_intent_conflicts()
    {
        AdapterConnection connection = CreateConnection(Guid.NewGuid());
        FakeCredentialRepository credentials = new();
        FakeConnectionManagementOperationRepository operations = new();
        TestClock clock = new() { UtcNow = Now.AddTicks(7) };
        CreateAdapterIngressCredentialCommandHandler handler =
            CreateCredentialHandler(
                connection,
                credentials,
                operations,
                clock: clock);
        CreateAdapterIngressCredentialCommand command = new(
            Guid.NewGuid(),
            connection.PropertyId,
            connection.Id,
            " primary ",
            Now.AddDays(30).AddTicks(7),
            " user:operator ",
            " FAKE.HTTP ");

        var issued = await handler.HandleAsync(command, CancellationToken.None);
        var replayed = await handler.HandleAsync(
            command with
            {
                Label = "primary",
                CreatedBy = "user:operator",
                SourceSystem = "fake.http"
            },
            CancellationToken.None);
        var conflicted = await handler.HandleAsync(
            command with { Label = "replacement" },
            CancellationToken.None);

        Assert.True(issued.IsSuccess, issued.Error.Code);
        Assert.Equal(
            AdapterIngressCredentialIssuanceOutcome.Issued,
            issued.Value.Outcome);
        Assert.IsType<string>(issued.Value.Token);
        Assert.True(replayed.IsSuccess, replayed.Error.Code);
        Assert.Equal(
            AdapterIngressCredentialIssuanceOutcome.AlreadyIssued,
            replayed.Value.Outcome);
        Assert.Null(replayed.Value.Token);
        Assert.Equal(issued.Value.Credential, replayed.Value.Credential);
        Assert.Equal(0, issued.Value.Credential.CreatedAtUtc.Ticks % 10);
        Assert.Equal(0, issued.Value.Credential.ExpiresAtUtc.Ticks % 10);
        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationConflict,
            conflicted.Error);
        Assert.Single(credentials.Items);
        IngestionConnectionManagementOperationRecord operation =
            Assert.Single(operations.Items);
        Assert.Equal(command.OperationId, operation.OperationId);
        Assert.Equal(
            command.OperationId,
            issued.Value.Credential.CredentialId);
        Assert.Equal(1, operation.ResultVersion);
        Assert.Equal(
            IngestionConnectionManagementMutationKind
                .AdapterIngressCredentialCreate,
            operation.Kind);
    }

    [Fact]
    public async Task Creation_rejects_a_legacy_credential_without_its_receipt()
    {
        AdapterConnection connection = CreateConnection(Guid.NewGuid());
        FakeCredentialRepository credentials = new();
        FakeConnectionManagementOperationRepository operations = new();
        CreateAdapterIngressCredentialCommandHandler handler =
            CreateCredentialHandler(connection, credentials, operations);
        CreateAdapterIngressCredentialCommand command = new(
            Guid.NewGuid(),
            connection.PropertyId,
            connection.Id,
            "primary",
            Now.AddDays(30),
            "user:operator");
        var issued = await handler.HandleAsync(command, CancellationToken.None);
        Assert.True(issued.IsSuccess, issued.Error.Code);
        operations.Items.Clear();

        var replayed = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationConflict,
            replayed.Error);
        Assert.Single(credentials.Items);
        Assert.Empty(operations.Items);
    }

    [Fact]
    public async Task Creation_revalidates_lifecycle_and_adapter_capability_before_replay()
    {
        AdapterConnection connection = CreateConnection(Guid.NewGuid());
        FakeCredentialRepository credentials = new();
        FakeConnectionManagementOperationRepository operations = new();
        CreateAdapterIngressCredentialCommand command = new(
            Guid.NewGuid(),
            connection.PropertyId,
            connection.Id,
            "primary",
            Now.AddDays(30),
            "user:operator");
        var issued = await CreateCredentialHandler(
            connection,
            credentials,
            operations).HandleAsync(command, CancellationToken.None);
        Assert.True(issued.IsSuccess, issued.Error.Code);

        var lifecycleDenied = await CreateCredentialHandler(
            connection,
            credentials,
            operations,
            lifecyclePolicies:
            [
                new TestLifecyclePolicy(
                    IngestionTenantLifecycleDecision.Restricted)
            ]).HandleAsync(command, CancellationToken.None);
        var capabilityDenied = await CreateCredentialHandler(
            connection,
            credentials,
            operations,
            descriptors: new UnsupportedPushDescriptors()).HandleAsync(
                command,
                CancellationToken.None);
        var descriptorChanged = await CreateCredentialHandler(
            connection,
            credentials,
            operations,
            descriptors: new TestDescriptors(
                protocolVersion: 2)).HandleAsync(
                command,
                CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.TenantLifecycleRestricted,
            lifecycleDenied.Error);
        Assert.Equal(
            IngestionApplicationErrors.AdapterExecutionModeUnsupported,
            capabilityDenied.Error);
        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationConflict,
            descriptorChanged.Error);
        Assert.Single(credentials.Items);
        Assert.Single(operations.Items);
    }

    [Fact]
    public async Task Revocation_replays_exact_intent_and_rejects_changed_or_new_intent()
    {
        AdapterConnection connection = CreateConnection(Guid.NewGuid());
        FakeCredentialRepository credentials = new();
        FakeConnectionManagementOperationRepository operations = new();
        var issued = await CreateCredentialHandler(
            connection,
            credentials,
            operations).HandleAsync(
                new(
                    Guid.NewGuid(),
                    connection.PropertyId,
                    connection.Id,
                    "primary",
                    Now.AddDays(30),
                    "user:operator"),
                CancellationToken.None);
        Assert.True(issued.IsSuccess, issued.Error.Code);
        Guid credentialId = issued.Value.Credential.CredentialId;
        RevokeAdapterIngressCredentialCommand command = new(
            Guid.NewGuid(),
            connection.PropertyId,
            connection.Id,
            credentialId,
            ExpectedVersion: 1,
            " user:operator ");
        RevokeAdapterIngressCredentialCommandHandler handler = new(
            TestIngestionExecution.Create(
                new FakeConnectionRepository(connection)),
            credentials,
            operations,
            new TestScope(),
            new TestClock());

        var missingOperation = await handler.HandleAsync(
            command with { OperationId = Guid.Empty },
            CancellationToken.None);
        var revoked = await handler.HandleAsync(command, CancellationToken.None);
        var replayed = await handler.HandleAsync(
            command with { RevokedBy = "user:operator" },
            CancellationToken.None);
        var changed = await handler.HandleAsync(
            command with { RevokedBy = "user:other" },
            CancellationToken.None);
        var newAttempt = await handler.HandleAsync(
            command with
            {
                OperationId = Guid.NewGuid(),
                ExpectedVersion = 2,
                RevokedBy = "user:operator"
            },
            CancellationToken.None);
        var lifecycleDenied = await new RevokeAdapterIngressCredentialCommandHandler(
            TestIngestionExecution.Create(
                new FakeConnectionRepository(connection)),
            credentials,
            operations,
            new TestScope(),
            new TestClock(),
            [
                new TestLifecyclePolicy(
                    IngestionTenantLifecycleDecision.Restricted)
            ]).HandleAsync(command, CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationInvalid,
            missingOperation.Error);
        Assert.True(revoked.IsSuccess, revoked.Error.Code);
        Assert.Equal(revoked.Value, replayed.Value);
        Assert.Equal(2, revoked.Value.Version);
        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationConflict,
            changed.Error);
        Assert.Equal(
            BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors
                .IngressCredentialAlreadyRevoked,
            newAttempt.Error);
        Assert.Equal(
            IngestionApplicationErrors.TenantLifecycleRestricted,
            lifecycleDenied.Error);
        Assert.Equal(2, operations.Items.Count);
        Assert.Single(
            operations.Items,
            operation => operation.Kind ==
                IngestionConnectionManagementMutationKind
                    .AdapterIngressCredentialRevoke);
    }

    private static CreateAdapterIngressCredentialCommandHandler
        CreateCredentialHandler(
            AdapterConnection connection,
            FakeCredentialRepository credentials,
            FakeConnectionManagementOperationRepository operations,
            IAdapterDescriptorRegistry? descriptors = null,
            IEnumerable<IIngestionTenantLifecyclePolicy>?
                lifecyclePolicies = null,
            ISystemClock? clock = null) =>
        new(
            TestIngestionExecution.Create(
                new FakeConnectionRepository(connection)),
            credentials,
            operations,
            new AdapterIngressTokenService(),
            descriptors ?? new TestDescriptors(),
            new TestScope(),
            clock ?? new TestClock(),
            lifecyclePolicies);

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

        public Task<bool> IdExistsAsync(
            Guid credentialId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.Any(item => item.Id == credentialId));

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

    private sealed class FakeConnectionManagementOperationRepository
        : IIngestionConnectionManagementOperationRepository
    {
        public List<IngestionConnectionManagementOperationRecord> Items { get; } = [];

        public Task<IngestionConnectionManagementOperationRecord?> GetAsync(
            Guid connectionId,
            Guid operationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(item =>
                item.ConnectionId == connectionId &&
                item.OperationId == operationId));

        public Task AddAsync(
            IngestionConnectionManagementOperationRecord operation,
            CancellationToken cancellationToken)
        {
            this.Items.Add(operation);
            return Task.CompletedTask;
        }
    }

    private sealed class TestScope(string scopeId = "tenant-a") : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = Now;
    }

    private sealed class TestDescriptors(
        int protocolVersion = 1,
        int configurationSchemaVersion = 1)
        : IAdapterDescriptorRegistry
    {
        private readonly AdapterDescriptor descriptor = new(
            "fake.http",
            protocolVersion,
            configurationSchemaVersion,
            [AdapterExecutionMode.Push, AdapterExecutionMode.RemotePolling]);

        public IReadOnlyCollection<AdapterDescriptor> GetAll() => [this.descriptor];

        public bool TryGet(string adapterType, out AdapterDescriptor? descriptor)
        {
            descriptor = string.Equals(adapterType, this.descriptor.AdapterType, StringComparison.Ordinal)
                ? this.descriptor
                : null;
            return descriptor is not null;
        }
    }

    private sealed class UnsupportedPushDescriptors
        : IAdapterDescriptorRegistry
    {
        private static readonly AdapterDescriptor Descriptor = new(
            "fake.http",
            1,
            1,
            [AdapterExecutionMode.Polling]);

        public IReadOnlyCollection<AdapterDescriptor> GetAll() => [Descriptor];

        public bool TryGet(
            string adapterType,
            out AdapterDescriptor? descriptor)
        {
            descriptor = string.Equals(
                adapterType,
                Descriptor.AdapterType,
                StringComparison.Ordinal)
                    ? Descriptor
                    : null;
            return descriptor is not null;
        }
    }

    private sealed class TestLifecyclePolicy(
        IngestionTenantLifecycleDecision decision)
        : IIngestionTenantLifecyclePolicy
    {
        public ValueTask<IngestionTenantLifecycleDecision> AuthorizeAsync(
            string tenantId,
            IngestionTenantLifecycleOperation operation,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(decision);
    }

}
