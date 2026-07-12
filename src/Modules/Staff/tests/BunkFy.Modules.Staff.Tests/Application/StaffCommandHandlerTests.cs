namespace BunkFy.Modules.Staff.Tests;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.Extensions.DependencyInjection;
using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffCommandHandlerTests
{
    [Fact]
    public async Task Create_requires_an_enabled_tenant_scope()
    {
        FakeStaffMemberRepository members = new();
        using ServiceProvider provider = CreateProvider(members, new FakePropertyProjectionRepository(),
            new TestScopeContext(false, null));
        ICommandHandler<CreateStaffMemberCommand, StaffMemberDto> handler = provider
            .GetRequiredService<ICommandHandler<CreateStaffMemberCommand, StaffMemberDto>>();

        Result<StaffMemberDto> result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.TenantRequired, result.Error);
        Assert.Null(members.AddedMember);
    }

    [Fact]
    public async Task Create_rejects_duplicate_employee_number_or_auth_subject_before_adding()
    {
        FakeStaffMemberRepository members = new()
        {
            ExistingEmployeeNumber = "EMP-100",
            ExistingAuthSubjectId = "user-100"
        };
        using ServiceProvider provider = CreateProvider(members, new FakePropertyProjectionRepository());
        ICommandHandler<CreateStaffMemberCommand, StaffMemberDto> handler = provider
            .GetRequiredService<ICommandHandler<CreateStaffMemberCommand, StaffMemberDto>>();

        Result<StaffMemberDto> employeeConflict = await handler.HandleAsync(
            CreateCommand(employeeNumber: " EMP-100 ", authSubjectId: null), CancellationToken.None);
        Result<StaffMemberDto> subjectConflict = await handler.HandleAsync(
            CreateCommand(employeeNumber: null, authSubjectId: " user-100 "), CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.EmployeeNumberConflict, employeeConflict.Error);
        Assert.Equal(StaffApplicationErrors.AuthSubjectConflict, subjectConflict.Error);
        Assert.Null(members.AddedMember);
    }

    [Fact]
    public async Task Assignment_rejects_a_property_that_is_not_active()
    {
        StaffMember member = CreateMember();
        FakeStaffMemberRepository members = new(member);
        using ServiceProvider provider = CreateProvider(members, new FakePropertyProjectionRepository());
        ICommandHandler<AssignStaffPropertyCommand, StaffMemberDto> handler = provider
            .GetRequiredService<ICommandHandler<AssignStaffPropertyCommand, StaffMemberDto>>();

        Result<StaffMemberDto> result = await handler.HandleAsync(
            new AssignStaffPropertyCommand(member.Id, Guid.NewGuid(), null, false,
                new DateOnly(2026, 7, 12), member.Version, "user:owner"),
            CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.PropertyUnavailable, result.Error);
        Assert.Empty(member.Assignments);
    }

    [Fact]
    public async Task Assignment_uses_the_projected_property_and_preserves_actor_and_time_provenance()
    {
        StaffMember member = CreateMember();
        Guid propertyId = Guid.NewGuid();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(propertyId));
        ICommandHandler<AssignStaffPropertyCommand, StaffMemberDto> handler = provider
            .GetRequiredService<ICommandHandler<AssignStaffPropertyCommand, StaffMemberDto>>();

        Result<StaffMemberDto> result = await handler.HandleAsync(
            new AssignStaffPropertyCommand(member.Id, propertyId, "Duty Manager", true,
                new DateOnly(2026, 7, 12), member.Version, " user:owner "),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(TestClock.Now, Assert.Single(result.Value.Assignments).AssignedAtUtc);
        Assert.Equal("user:owner", Assert.Single(member.Assignments).AssignedBy);
    }

    private static ServiceProvider CreateProvider(
        IStaffMemberRepository members,
        IStaffPropertyProjectionRepository properties,
        IScopeContext? scope = null)
    {
        ServiceCollection services = new();
        services.AddSingleton(members);
        services.AddSingleton(properties);
        services.AddSingleton(scope ?? new TestScopeContext(true, "tenant-a"));
        services.AddSingleton<ISystemClock>(new TestClock());
        services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        services.AddStaffApplication();
        return services.BuildServiceProvider();
    }

    private static CreateStaffMemberCommand CreateCommand(
        string? employeeNumber = "EMP-100",
        string? authSubjectId = "user-100") => new(
        "Ada Operator", null, "ada@example.test", null, employeeNumber,
        "Manager", "Operations", authSubjectId, "user:owner");

    private static StaffMember CreateMember() => StaffMember.Create(
        Guid.NewGuid(), "tenant-a", "Ada Operator", null, "ada@example.test", null,
        "EMP-100", "Manager", "Operations", null, "user:owner", Guid.NewGuid(), TestClock.Now).Value;

    private sealed class FakeStaffMemberRepository(StaffMember? member = null) : IStaffMemberRepository
    {
        public string? ExistingEmployeeNumber { get; init; }
        public string? ExistingAuthSubjectId { get; init; }
        public StaffMember? AddedMember { get; private set; }

        public Task AddAsync(StaffMember value, CancellationToken cancellationToken)
        {
            this.AddedMember = value;
            return Task.CompletedTask;
        }

        public Task<StaffMember?> GetAsync(Guid staffMemberId, CancellationToken cancellationToken) =>
            Task.FromResult(member?.Id == staffMemberId ? member : null);

        public Task<StaffMember?> GetAtPropertyAsync(Guid propertyId, Guid staffMemberId,
            CancellationToken cancellationToken) => Task.FromResult<StaffMember?>(null);

        public Task<StaffListResponse> ListAsync(string? search, StaffStatus? status,
            PageRequest pageRequest, CancellationToken cancellationToken) =>
            Task.FromResult(new StaffListResponse([], pageRequest.Page, pageRequest.PageSize));

        public Task<StaffListResponse> ListAtPropertyAsync(Guid propertyId, string? search,
            StaffStatus? status, PageRequest pageRequest, CancellationToken cancellationToken) =>
            Task.FromResult(new StaffListResponse([], pageRequest.Page, pageRequest.PageSize));

        public Task<bool> EmployeeNumberExistsAsync(string employeeNumber, Guid? exceptStaffMemberId,
            CancellationToken cancellationToken) => Task.FromResult(
            string.Equals(employeeNumber, this.ExistingEmployeeNumber, StringComparison.Ordinal));

        public Task<bool> AuthSubjectExistsAsync(string authSubjectId, Guid? exceptStaffMemberId,
            CancellationToken cancellationToken) => Task.FromResult(
            string.Equals(authSubjectId, this.ExistingAuthSubjectId, StringComparison.Ordinal));
    }

    private sealed class FakePropertyProjectionRepository(params Guid[] activeProperties)
        : IStaffPropertyProjectionRepository
    {
        public Task<bool> IsActiveAsync(Guid propertyId, CancellationToken cancellationToken) =>
            Task.FromResult(activeProperties.Contains(propertyId));

        public Task ApplyAsync(StaffPropertyProjectionWriteModel property,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestScopeContext(bool enabled, string? scopeId) : IScopeContext
    {
        public bool IsEnabled => enabled;
        public string? ScopeId => scopeId;
    }

    private sealed class TestClock : ISystemClock
    {
        public static DateTimeOffset Now { get; } = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
