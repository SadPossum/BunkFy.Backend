namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Handlers;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CreatePropertyCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 7, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task New_creation_locks_reads_and_checks_code_before_adding()
    {
        List<string> sequence = [];
        RecordingPropertyRepository properties = new(sequence: sequence);
        RecordingCreationOperationLock creationLock = new(sequence);
        RecordingUniqueCoordinateLock uniqueCoordinates = new(sequence);
        Guid operationId = Guid.NewGuid();
        RecordingIdGenerator ids = new();
        CreatePropertyCommandHandler handler = CreateHandler(
            properties,
            creationLock,
            uniqueCoordinates,
            ids);

        Result<PropertyMutationReceiptDto> result = await handler.HandleAsync(
            CreateCommand(operationId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(operationId, result.Value.PropertyId);
        Assert.Equal(operationId, properties.Added?.Id);
        Assert.Equal("harbour-house", properties.Added?.Code.Value);
        Assert.Equal(
            ["creation-lock", "read", "code-lock", "code-exists", "add"],
            sequence);
        Assert.Equal((TestScopeContext.TenantId, operationId),
            Assert.Single(creationLock.Acquisitions));
        Assert.Equal(1, ids.Calls);
        Assert.Single(properties.Added!.DomainEvents);
    }

    [Fact]
    public async Task Normalized_retry_returns_current_receipt_without_recreating()
    {
        Guid operationId = Guid.NewGuid();
        Property existing = CreateProperty(operationId);
        Assert.True(existing.Retire(
            existing.Version,
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        List<string> sequence = [];
        RecordingPropertyRepository properties = new(
            existing,
            sequence: sequence);
        RecordingCreationOperationLock creationLock = new(sequence);
        RecordingUniqueCoordinateLock uniqueCoordinates = new(sequence);

        Result<PropertyMutationReceiptDto> result = await CreateHandler(
            properties,
            creationLock,
            uniqueCoordinates,
            new ThrowingIdGenerator()).HandleAsync(
                new CreatePropertyCommand(
                    operationId,
                    "  Harbour House  ",
                    " HARBOUR-HOUSE ",
                    " UTC "),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PropertyStatus.Retired, result.Value.Status);
        Assert.Equal(existing.Version, result.Value.Version);
        Assert.Equal(["creation-lock", "read"], sequence);
        Assert.Null(properties.Added);
        Assert.Empty(uniqueCoordinates.PropertyCodeAcquisitions);
    }

    [Fact]
    public async Task Reusing_operation_for_changed_details_conflicts()
    {
        Guid operationId = Guid.NewGuid();
        RecordingPropertyRepository properties = new(
            CreateProperty(operationId));

        Result<PropertyMutationReceiptDto> result = await CreateHandler(
            properties,
            new RecordingCreationOperationLock(),
            new RecordingUniqueCoordinateLock(),
            new ThrowingIdGenerator()).HandleAsync(
                CreateCommand(operationId) with
                {
                    Name = "Different House"
                },
                CancellationToken.None);

        Assert.Equal(
            PropertiesApplicationErrors.CreationOperationConflict,
            result.Error);
        Assert.Null(properties.Added);
    }

    [Fact]
    public async Task Invalid_operation_is_rejected_before_locking()
    {
        RecordingCreationOperationLock creationLock = new();
        RecordingPropertyRepository properties = new();

        Result<PropertyMutationReceiptDto> result = await CreateHandler(
            properties,
            creationLock,
            new RecordingUniqueCoordinateLock(),
            new ThrowingIdGenerator()).HandleAsync(
                CreateCommand(Guid.Empty),
                CancellationToken.None);

        Assert.Equal(
            PropertiesApplicationErrors.CreationOperationInvalid,
            result.Error);
        Assert.Empty(creationLock.Acquisitions);
        Assert.Equal(0, properties.Reads);
    }

    [Fact]
    public async Task Failed_code_conflict_does_not_bind_the_operation()
    {
        Guid operationId = Guid.NewGuid();
        RecordingPropertyRepository properties = new()
        {
            CodeExists = true
        };
        CreatePropertyCommandHandler handler = CreateHandler(
            properties,
            new RecordingCreationOperationLock(),
            new RecordingUniqueCoordinateLock(),
            new RecordingIdGenerator());

        Result<PropertyMutationReceiptDto> failed = await handler.HandleAsync(
            CreateCommand(operationId),
            CancellationToken.None);
        properties.CodeExists = false;
        Result<PropertyMutationReceiptDto> retry = await handler.HandleAsync(
            CreateCommand(operationId),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.PropertyCodeAlreadyExists, failed.Error);
        Assert.True(retry.IsSuccess);
        Assert.Equal(operationId, retry.Value.PropertyId);
        Assert.Equal(operationId, properties.Added?.Id);
    }

    private static CreatePropertyCommandHandler CreateHandler(
        RecordingPropertyRepository properties,
        IPropertiesCreationOperationLock creationLock,
        RecordingUniqueCoordinateLock uniqueCoordinates,
        IIdGenerator ids)
    {
        TestScopeContext scopeContext = new();
        return new(
            properties,
            PropertiesMutationTestSupport.Create(
                properties: properties,
                uniqueCoordinates: uniqueCoordinates,
                scopeContext: scopeContext),
            creationLock,
            scopeContext,
            new TestClock(),
            ids);
    }

    private static CreatePropertyCommand CreateCommand(Guid operationId) =>
        new(
            operationId,
            "Harbour House",
            "harbour-house",
            "UTC");

    private static Property CreateProperty(Guid propertyId) =>
        Property.Create(
            propertyId,
            TestScopeContext.TenantId,
            "Harbour House",
            "harbour-house",
            "UTC",
            Guid.NewGuid(),
            Now).Value;

    private sealed class RecordingPropertyRepository(
        Property? existing = null,
        List<string>? sequence = null)
        : IPropertyRepository
    {
        public Property? Existing { get; private set; } = existing;
        public Property? Added { get; private set; }
        public bool CodeExists { get; set; }
        public int Reads { get; private set; }

        public Task AddAsync(
            Property property,
            CancellationToken cancellationToken)
        {
            sequence?.Add("add");
            this.Added = property;
            this.Existing = property;
            return Task.CompletedTask;
        }

        public Task<Property?> GetAsync(
            Guid propertyId,
            CancellationToken cancellationToken)
        {
            sequence?.Add("read");
            this.Reads++;
            return Task.FromResult(
                this.Existing?.Id == propertyId
                    ? this.Existing
                    : null);
        }

        public Task<bool> CodeExistsAsync(
            string code,
            Guid? excludingPropertyId,
            CancellationToken cancellationToken)
        {
            sequence?.Add("code-exists");
            return Task.FromResult(this.CodeExists);
        }
    }

    private sealed class RecordingCreationOperationLock(
        List<string>? sequence = null)
        : IPropertiesCreationOperationLock
    {
        public List<(string TenantId, Guid OperationId)> Acquisitions { get; } = [];

        public Task AcquireAsync(
            string tenantId,
            Guid operationId,
            CancellationToken cancellationToken)
        {
            sequence?.Add("creation-lock");
            this.Acquisitions.Add((tenantId, operationId));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUniqueCoordinateLock(
        List<string>? sequence = null)
        : IPropertiesUniqueCoordinateLock
    {
        public List<(string TenantId, string Code)> PropertyCodeAcquisitions { get; } = [];

        public Task AcquirePropertyCodeAsync(
            string tenantId,
            string propertyCode,
            CancellationToken cancellationToken)
        {
            sequence?.Add("code-lock");
            this.PropertyCodeAcquisitions.Add((tenantId, propertyCode));
            return Task.CompletedTask;
        }

        public Task AcquireRoomNameAsync(
            string tenantId,
            Guid propertyId,
            string roomName,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public const string TenantId = "tenant-a";
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class RecordingIdGenerator : IIdGenerator
    {
        public int Calls { get; private set; }

        public Guid NewId()
        {
            this.Calls++;
            return Guid.NewGuid();
        }
    }

    private sealed class ThrowingIdGenerator : IIdGenerator
    {
        public Guid NewId() => throw new InvalidOperationException(
            "An exact replay must not allocate another event id.");
    }
}
