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
using BunkFy.Modules.Staff.Application.Handlers;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Errors;
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
        ICommandHandler<CreateStaffMemberCommand, StaffDirectoryMemberDto> handler = provider
            .GetRequiredService<ICommandHandler<CreateStaffMemberCommand, StaffDirectoryMemberDto>>();

        Result<StaffDirectoryMemberDto> result = await handler.HandleAsync(
            CreateCommand(), CancellationToken.None);

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
        ICommandHandler<CreateStaffMemberCommand, StaffDirectoryMemberDto> handler = provider
            .GetRequiredService<ICommandHandler<CreateStaffMemberCommand, StaffDirectoryMemberDto>>();

        Result<StaffDirectoryMemberDto> employeeConflict = await handler.HandleAsync(
            CreateCommand(employeeNumber: " EMP-100 ", authSubjectId: null), CancellationToken.None);
        Result<StaffDirectoryMemberDto> subjectConflict = await handler.HandleAsync(
            CreateCommand(employeeNumber: null, authSubjectId: " user-100 "), CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.EmployeeNumberConflict, employeeConflict.Error);
        Assert.Equal(StaffApplicationErrors.AuthSubjectConflict, subjectConflict.Error);
        Assert.Null(members.AddedMember);
    }

    [Fact]
    public async Task Create_uses_the_operation_id_and_replays_an_equivalent_normalized_profile()
    {
        Guid operationId = Guid.NewGuid();
        StaffMember existing = CreateMemberForCreation(operationId);
        FakeStaffMemberRepository members = new(existing);
        RecordingCreationOperationLock creationLock = new();
        using ServiceProvider provider = CreateProvider(
            members,
            new FakePropertyProjectionRepository(),
            creationLock: creationLock);
        var handler = provider.GetRequiredService<
            ICommandHandler<CreateStaffMemberCommand, StaffDirectoryMemberDto>>();
        CreateStaffMemberCommand command = CreateCommand(
            operationId,
            displayName: " Ada Operator ",
            workEmail: " ADA@EXAMPLE.TEST ",
            employeeNumber: " EMP-100 ",
            authSubjectId: " user-100 ");
        int eventCount = existing.DomainEvents.Count;

        Result<StaffDirectoryMemberDto> result = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(operationId, result.Value.StaffMemberId);
        Assert.Equal([("tenant-a", operationId)], creationLock.Acquisitions);
        Assert.Equal(0, members.AddCount);
        Assert.Equal(eventCount, existing.DomainEvents.Count);
    }

    [Fact]
    public async Task Create_rejects_reusing_an_operation_for_different_profile_data()
    {
        Guid operationId = Guid.NewGuid();
        FakeStaffMemberRepository members = new(
            CreateMemberForCreation(operationId));
        using ServiceProvider provider = CreateProvider(
            members,
            new FakePropertyProjectionRepository());
        var handler = provider.GetRequiredService<
            ICommandHandler<CreateStaffMemberCommand, StaffDirectoryMemberDto>>();

        Result<StaffDirectoryMemberDto> result = await handler.HandleAsync(
            CreateCommand(operationId, displayName: "Different operator"),
            CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.CreationOperationConflict, result.Error);
        Assert.Equal(0, members.AddCount);
    }

    [Fact]
    public async Task Create_does_not_disclose_or_reuse_a_hidden_operation_coordinate()
    {
        Guid operationId = Guid.NewGuid();
        FakeStaffMemberRepository members = new(
            CreateMemberForCreation(operationId))
        {
            OperationallyVisible = false
        };
        using ServiceProvider provider = CreateProvider(
            members,
            new FakePropertyProjectionRepository());
        var handler = provider.GetRequiredService<
            ICommandHandler<CreateStaffMemberCommand, StaffDirectoryMemberDto>>();

        Result<StaffDirectoryMemberDto> result = await handler.HandleAsync(
            CreateCommand(operationId),
            CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.StaffMemberNotFound, result.Error);
        Assert.Equal(1, members.OperationalGetCount);
        Assert.Equal(1, members.SafetyGetCount);
        Assert.Equal(0, members.AddCount);
    }

    [Fact]
    public async Task Create_acquires_the_operation_coordinate_before_any_member_read()
    {
        List<string> calls = [];
        FakeStaffMemberRepository members = new() { Calls = calls };
        RecordingCreationOperationLock creationLock = new(calls);
        using ServiceProvider provider = CreateProvider(
            members,
            new FakePropertyProjectionRepository(),
            creationLock: creationLock);
        var handler = provider.GetRequiredService<
            ICommandHandler<CreateStaffMemberCommand, StaffDirectoryMemberDto>>();
        Guid operationId = Guid.NewGuid();

        Result<StaffDirectoryMemberDto> result = await handler.HandleAsync(
            CreateCommand(operationId),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal("creation-lock", calls[0]);
        Assert.Equal("operational-read", calls[1]);
        Assert.Equal("safety-read", calls[2]);
        Assert.Equal(operationId, members.AddedMember?.Id);
    }

    [Fact]
    public async Task Assignment_rejects_a_property_that_is_not_active()
    {
        StaffMember member = CreateMember();
        FakeStaffMemberRepository members = new(member);
        using ServiceProvider provider = CreateProvider(members, new FakePropertyProjectionRepository());
        ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto> handler = provider
            .GetRequiredService<ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            new AssignStaffPropertyCommand(Guid.NewGuid(), member.Id, Guid.NewGuid(), null, false,
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
        ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto> handler = provider
            .GetRequiredService<ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            new AssignStaffPropertyCommand(Guid.NewGuid(), member.Id, propertyId, "Duty Manager", true,
                new DateOnly(2026, 7, 12), member.Version, " user:owner "),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(TestClock.Now, Assert.Single(member.Assignments).AssignedAtUtc);
        Assert.Equal("user:owner", Assert.Single(member.Assignments).AssignedBy);
    }

    [Fact]
    public async Task Assignment_replays_the_exact_operation_without_advancing_history()
    {
        StaffMember member = CreateMember();
        Guid propertyId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(propertyId),
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();
        AssignStaffPropertyCommand command = new(
            operationId,
            member.Id,
            propertyId,
            " Duty Manager ",
            true,
            new DateOnly(2026, 7, 12),
            member.Version,
            "user:owner");

        Result<StaffMemberMutationReceiptDto> first = await handler.HandleAsync(
            command,
            CancellationToken.None);
        int eventCount = member.DomainEvents.Count;
        Result<StaffMemberMutationReceiptDto> replay = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(2, member.Version);
        Assert.Single(member.Assignments);
        Assert.Equal(eventCount, member.DomainEvents.Count);
        Assert.Equal(
            StaffMemberMutationKind.AssignProperty,
            Assert.Single(operations.Records).Kind);
    }

    [Fact]
    public async Task Assignment_records_a_receipt_for_an_exact_current_state_no_op()
    {
        StaffMember member = CreateMember();
        Guid propertyId = Guid.NewGuid();
        Assert.True(member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            "Duty Manager",
            true,
            new DateOnly(2026, 7, 1),
            member.Version,
            "user:owner",
            Guid.NewGuid(),
            TestClock.Now).IsSuccess);
        long selectedVersion = member.Version;
        int eventCount = member.DomainEvents.Count;
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(propertyId),
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            new AssignStaffPropertyCommand(
                Guid.NewGuid(),
                member.Id,
                propertyId,
                " Duty Manager ",
                true,
                new DateOnly(2026, 7, 1),
                selectedVersion,
                "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(selectedVersion, result.Value.Version);
        Assert.Equal(selectedVersion, member.Version);
        Assert.Equal(eventCount, member.DomainEvents.Count);
        StaffMemberMutationOperationRecord operation = Assert.Single(operations.Records);
        Assert.Equal(selectedVersion, operation.ExpectedVersion);
        Assert.Equal(selectedVersion, operation.ResultVersion);
        Assert.Equal(StaffMemberMutationKind.AssignProperty, operation.Kind);
    }

    [Fact]
    public async Task Assignment_replay_does_not_bypass_current_property_visibility()
    {
        StaffMember member = CreateMember();
        Guid propertyId = Guid.NewGuid();
        RecordingMemberMutationOperations operations = new();
        AssignStaffPropertyCommand command = new(
            Guid.NewGuid(),
            member.Id,
            propertyId,
            null,
            false,
            new DateOnly(2026, 7, 12),
            member.Version,
            "user:owner");

        using (ServiceProvider available = CreateProvider(
                   new FakeStaffMemberRepository(member),
                   new FakePropertyProjectionRepository(propertyId),
                   memberMutationOperations: operations))
        {
            var handler = available.GetRequiredService<
                ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();
            Result<StaffMemberMutationReceiptDto> first = await handler.HandleAsync(
                command,
                CancellationToken.None);
            Assert.True(first.IsSuccess, first.Error.Code);
        }

        using ServiceProvider unavailable = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            memberMutationOperations: operations);
        var replay = unavailable.GetRequiredService<
            ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();
        Result<StaffMemberMutationReceiptDto> rejected = await replay.HandleAsync(
            command,
            CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.PropertyUnavailable, rejected.Error);
        Assert.Single(operations.Records);
    }

    [Fact]
    public async Task Assignment_rejects_changed_or_cross_kind_operation_reuse()
    {
        StaffMember member = CreateMember();
        Guid propertyId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        long selectedVersion = member.Version;
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(propertyId),
            memberMutationOperations: operations);
        var assign = provider.GetRequiredService<
            ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();
        var unassign = provider.GetRequiredService<
            ICommandHandler<UnassignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> first = await assign.HandleAsync(
            new AssignStaffPropertyCommand(
                operationId,
                member.Id,
                propertyId,
                "Duty Manager",
                false,
                new DateOnly(2026, 7, 12),
                selectedVersion,
                "user:owner"),
            CancellationToken.None);
        Result<StaffMemberMutationReceiptDto> changed = await assign.HandleAsync(
            new AssignStaffPropertyCommand(
                operationId,
                member.Id,
                propertyId,
                "Front Desk",
                false,
                new DateOnly(2026, 7, 12),
                selectedVersion,
                "user:owner"),
            CancellationToken.None);
        Result<StaffMemberMutationReceiptDto> crossKind = await unassign.HandleAsync(
            new UnassignStaffPropertyCommand(
                operationId,
                member.Id,
                propertyId,
                new DateOnly(2026, 7, 12),
                "Transferred",
                selectedVersion,
                "user:owner"),
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.Equal(StaffApplicationErrors.AssignmentOperationConflict, changed.Error);
        Assert.Equal(StaffApplicationErrors.AssignmentOperationConflict, crossKind.Error);
        Assert.Single(operations.Records);
        Assert.Equal(2, member.Version);
    }

    [Fact]
    public async Task Failed_assignment_attempt_does_not_bind_the_operation_id()
    {
        StaffMember member = CreateMember();
        Guid propertyId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        RecordingMemberMutationOperations operations = new();
        AssignStaffPropertyCommand command = new(
            operationId,
            member.Id,
            propertyId,
            null,
            false,
            new DateOnly(2026, 7, 12),
            member.Version,
            "user:owner");

        using (ServiceProvider unavailable = CreateProvider(
                   new FakeStaffMemberRepository(member),
                   new FakePropertyProjectionRepository(),
                   memberMutationOperations: operations))
        {
            var handler = unavailable.GetRequiredService<
                ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();
            Result<StaffMemberMutationReceiptDto> rejected = await handler.HandleAsync(
                command,
                CancellationToken.None);
            Assert.Equal(StaffApplicationErrors.PropertyUnavailable, rejected.Error);
        }

        using ServiceProvider available = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(propertyId),
            memberMutationOperations: operations);
        var retry = available.GetRequiredService<
            ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();
        Result<StaffMemberMutationReceiptDto> accepted = await retry.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(accepted.IsSuccess, accepted.Error.Code);
        Assert.Single(operations.Records);
    }

    [Fact]
    public async Task Unassignment_replays_only_the_matching_completed_operation()
    {
        StaffMember member = CreateMember();
        Guid propertyId = Guid.NewGuid();
        Assert.True(member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            null,
            false,
            new DateOnly(2026, 7, 1),
            member.Version,
            "user:owner",
            Guid.NewGuid(),
            TestClock.Now).IsSuccess);
        Guid operationId = Guid.NewGuid();
        long selectedVersion = member.Version;
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(propertyId),
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<UnassignStaffPropertyCommand, StaffMemberMutationReceiptDto>>();
        UnassignStaffPropertyCommand command = new(
            operationId,
            member.Id,
            propertyId,
            new DateOnly(2026, 7, 12),
            " Transferred ",
            selectedVersion,
            "user:owner");

        Result<StaffMemberMutationReceiptDto> first = await handler.HandleAsync(
            command,
            CancellationToken.None);
        int eventCount = member.DomainEvents.Count;
        Result<StaffMemberMutationReceiptDto> replay = await handler.HandleAsync(
            command,
            CancellationToken.None);
        Result<StaffMemberMutationReceiptDto> distinct = await handler.HandleAsync(
            command with
            {
                OperationId = Guid.NewGuid(),
                ExpectedVersion = member.Version
            },
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(StaffDomainErrors.AssignmentNotFound, distinct.Error);
        Assert.Equal(3, member.Version);
        Assert.Equal(eventCount, member.DomainEvents.Count);
        Assert.Equal(
            StaffMemberMutationKind.UnassignProperty,
            Assert.Single(operations.Records).Kind);
    }

    [Fact]
    public async Task Operational_mutation_rechecks_visibility_after_acquiring_the_member_lock()
    {
        StaffMember member = CreateMember();
        FakeStaffMemberRepository members = new(member);
        RecordingOperationLock operationLock = new(
            () => members.OperationallyVisible = false);
        using ServiceProvider provider = CreateProvider(
            members,
            new FakePropertyProjectionRepository(),
            operationLock: operationLock);
        var handler = provider.GetRequiredService<
            ICommandHandler<UpdateStaffMemberCommand, StaffMemberMutationReceiptDto>>();
        long selectedVersion = member.Version;

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            new UpdateStaffMemberCommand(
                Guid.NewGuid(),
                member.Id,
                "Changed after restriction",
                null,
                "changed@example.test",
                null,
                "EMP-100",
                "Manager",
                "Operations",
                selectedVersion,
                "user:owner"),
            CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.StaffMemberNotFound, result.Error);
        Assert.Equal(selectedVersion, member.Version);
        Assert.Equal("Ada Operator", member.DisplayName);
        Assert.Equal((member.ScopeId, member.Id), Assert.Single(operationLock.Acquisitions));
        Assert.Equal(1, members.OperationalGetCount);
    }

    [Fact]
    public async Task Profile_update_replays_an_equivalent_request_without_a_second_mutation()
    {
        StaffMember member = CreateMember();
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<UpdateStaffMemberCommand, StaffMemberMutationReceiptDto>>();
        Guid operationId = Guid.NewGuid();
        long expectedVersion = member.Version;
        UpdateStaffMemberCommand command = UpdateCommand(
            member,
            operationId,
            expectedVersion,
            displayName: "  Grace Operator  ");

        Result<StaffMemberMutationReceiptDto> first = await handler.HandleAsync(
            command,
            CancellationToken.None);
        int eventCount = member.DomainEvents.Count;
        Result<StaffMemberMutationReceiptDto> replay = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(expectedVersion + 1, member.Version);
        Assert.Equal("Grace Operator", member.DisplayName);
        Assert.Equal(eventCount, member.DomainEvents.Count);
        Assert.Single(operations.Records);
    }

    [Fact]
    public async Task Profile_update_rejects_changed_reuse_and_a_distinct_stale_operation()
    {
        StaffMember member = CreateMember();
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<UpdateStaffMemberCommand, StaffMemberMutationReceiptDto>>();
        Guid operationId = Guid.NewGuid();
        long expectedVersion = member.Version;

        Result<StaffMemberMutationReceiptDto> first = await handler.HandleAsync(
            UpdateCommand(member, operationId, expectedVersion, "Grace Operator"),
            CancellationToken.None);
        Result<StaffMemberMutationReceiptDto> changedReuse = await handler.HandleAsync(
            UpdateCommand(member, operationId, expectedVersion, "Different Operator"),
            CancellationToken.None);
        Result<StaffMemberMutationReceiptDto> stale = await handler.HandleAsync(
            UpdateCommand(member, Guid.NewGuid(), expectedVersion, "Different Operator"),
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.Equal(StaffApplicationErrors.ProfileUpdateOperationConflict, changedReuse.Error);
        Assert.Equal(StaffApplicationErrors.VersionConflict, stale.Error);
        Assert.Equal("Grace Operator", member.DisplayName);
        Assert.Single(operations.Records);
    }

    [Fact]
    public async Task Profile_update_records_an_exact_no_op_without_an_event_or_version_change()
    {
        StaffMember member = CreateMember();
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<UpdateStaffMemberCommand, StaffMemberMutationReceiptDto>>();
        int eventCount = member.DomainEvents.Count;
        long expectedVersion = member.Version;

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            UpdateCommand(member, Guid.NewGuid(), expectedVersion, member.DisplayName),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(expectedVersion, result.Value.Version);
        Assert.Equal(expectedVersion, member.Version);
        Assert.Equal(eventCount, member.DomainEvents.Count);
        Assert.Single(operations.Records);
    }

    [Fact]
    public async Task Profile_update_validates_actor_before_lock_or_replay_lookup()
    {
        StaffMember member = CreateMember();
        RecordingOperationLock operationLock = new();
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            operationLock: operationLock,
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<UpdateStaffMemberCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            UpdateCommand(
                member,
                Guid.NewGuid(),
                member.Version,
                member.DisplayName) with
            {
                ActorId = " "
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(operationLock.Acquisitions);
        Assert.Equal(0, operations.GetCount);
        Assert.Empty(operations.Records);
    }

    [Fact]
    public async Task Auth_subject_change_replays_normalized_input_without_a_second_event()
    {
        StaffMember member = CreateMember("user-100");
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<SetStaffAuthSubjectCommand, StaffMemberMutationReceiptDto>>();
        Guid operationId = Guid.NewGuid();
        long expectedVersion = member.Version;

        Result<StaffMemberMutationReceiptDto> first = await handler.HandleAsync(
            new SetStaffAuthSubjectCommand(
                operationId,
                member.Id,
                " user-200 ",
                expectedVersion,
                "user:owner"),
            CancellationToken.None);
        int eventCount = member.DomainEvents.Count;
        Result<StaffMemberMutationReceiptDto> replay = await handler.HandleAsync(
            new SetStaffAuthSubjectCommand(
                operationId,
                member.Id,
                "user-200",
                expectedVersion,
                "user:owner"),
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal("user-200", member.AuthSubjectId);
        Assert.Equal(expectedVersion + 1, member.Version);
        Assert.Equal(eventCount, member.DomainEvents.Count);
        StaffMemberMutationOperationRecord operation =
            Assert.Single(operations.Records);
        Assert.Equal(
            StaffMemberMutationKind.AuthSubjectChange,
            operation.Kind);
    }

    [Fact]
    public async Task Auth_subject_change_rejects_changed_or_cross_kind_operation_reuse()
    {
        StaffMember member = CreateMember("user-100");
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<SetStaffAuthSubjectCommand, StaffMemberMutationReceiptDto>>();
        Guid operationId = Guid.NewGuid();
        long expectedVersion = member.Version;

        Result<StaffMemberMutationReceiptDto> first = await handler.HandleAsync(
            new SetStaffAuthSubjectCommand(
                operationId,
                member.Id,
                "user-200",
                expectedVersion,
                "user:owner"),
            CancellationToken.None);
        Result<StaffMemberMutationReceiptDto> changed = await handler.HandleAsync(
            new SetStaffAuthSubjectCommand(
                operationId,
                member.Id,
                "user-300",
                expectedVersion,
                "user:owner"),
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.Equal(
            StaffApplicationErrors.AuthSubjectOperationConflict,
            changed.Error);

        Guid profileOperationId = Guid.NewGuid();
        operations.Records.Add(new StaffMemberMutationOperationRecord(
            profileOperationId,
            member.ScopeId,
            member.Id,
            StaffMemberMutationKind.ProfileUpdate,
            member.Version,
            new string('a', 64),
            StaffStatus.Active,
            member.Version,
            TestClock.Now));
        Result<StaffMemberMutationReceiptDto> crossKind =
            await handler.HandleAsync(
                new SetStaffAuthSubjectCommand(
                    profileOperationId,
                    member.Id,
                    "user-200",
                    member.Version,
                    "user:owner"),
                CancellationToken.None);

        Assert.Equal(
            StaffApplicationErrors.AuthSubjectOperationConflict,
            crossKind.Error);
    }

    [Fact]
    public async Task Auth_subject_no_op_records_a_receipt_but_a_distinct_stale_no_op_fails()
    {
        StaffMember member = CreateMember("user-100");
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<SetStaffAuthSubjectCommand, StaffMemberMutationReceiptDto>>();
        long selectedVersion = member.Version;
        int eventCount = member.DomainEvents.Count;

        Result<StaffMemberMutationReceiptDto> noOp = await handler.HandleAsync(
            new SetStaffAuthSubjectCommand(
                Guid.NewGuid(),
                member.Id,
                " user-100 ",
                selectedVersion,
                "user:owner"),
            CancellationToken.None);

        Assert.True(noOp.IsSuccess, noOp.Error.Code);
        Assert.Equal(selectedVersion, noOp.Value.Version);
        Assert.Equal(selectedVersion, member.Version);
        Assert.Equal(eventCount, member.DomainEvents.Count);

        Result changed = member.SetAuthSubject(
            "user-200",
            member.Version,
            "system:identity-sync",
            Guid.NewGuid(),
            TestClock.Now.AddMinutes(1));
        Result<StaffMemberMutationReceiptDto> staleNoOp =
            await handler.HandleAsync(
                new SetStaffAuthSubjectCommand(
                    Guid.NewGuid(),
                    member.Id,
                    "user-200",
                    selectedVersion,
                    "user:owner"),
                CancellationToken.None);

        Assert.True(changed.IsSuccess, changed.Error.Code);
        Assert.Equal(StaffApplicationErrors.VersionConflict, staleNoOp.Error);
        Assert.Single(operations.Records);
    }

    [Fact]
    public async Task Auth_subject_change_validates_actor_and_visibility_before_receipt_lookup()
    {
        StaffMember member = CreateMember("user-100");
        FakeStaffMemberRepository members = new(member)
        {
            OperationallyVisible = false
        };
        RecordingOperationLock operationLock = new();
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            members,
            new FakePropertyProjectionRepository(),
            operationLock: operationLock,
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<SetStaffAuthSubjectCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> invalidOperation =
            await handler.HandleAsync(
                new SetStaffAuthSubjectCommand(
                    Guid.Empty,
                    member.Id,
                    "user-200",
                    member.Version,
                    "user:owner"),
                CancellationToken.None);
        Result<StaffMemberMutationReceiptDto> invalidActor =
            await handler.HandleAsync(
                new SetStaffAuthSubjectCommand(
                    Guid.NewGuid(),
                    member.Id,
                    "user-200",
                    member.Version,
                    " "),
                CancellationToken.None);
        Result<StaffMemberMutationReceiptDto> hidden =
            await handler.HandleAsync(
                new SetStaffAuthSubjectCommand(
                    Guid.NewGuid(),
                    member.Id,
                    "user-200",
                    member.Version,
                    "user:owner"),
                CancellationToken.None);

        Assert.Equal(
            StaffApplicationErrors.AuthSubjectOperationInvalid,
            invalidOperation.Error);
        Assert.True(invalidActor.IsFailure);
        Assert.Equal(
            StaffApplicationErrors.StaffMemberNotFound,
            hidden.Error);
        Assert.Single(operationLock.Acquisitions);
        Assert.Equal(0, operations.GetCount);
        Assert.Empty(operations.Records);
    }

    [Fact]
    public async Task Self_service_profile_update_rechecks_auth_subject_after_locking()
    {
        StaffMember member = CreateMember("user-100");
        RecordingMemberMutationOperations operations = new();
        RecordingOperationLock operationLock = new(() =>
        {
            Result changed = member.SetAuthSubject(
                "user-200",
                member.Version,
                "system:identity-sync",
                Guid.NewGuid(),
                TestClock.Now.AddMinutes(1));
            Assert.True(changed.IsSuccess, changed.Error.Code);
        });
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            operationLock: operationLock,
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<UpdateCurrentStaffMemberCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            new UpdateCurrentStaffMemberCommand(
                Guid.NewGuid(),
                "user-100",
                "Changed after relink",
                member.LegalName,
                member.WorkEmail,
                member.WorkPhone,
                member.EmployeeNumber,
                member.JobTitle,
                member.Department,
                member.Version,
                "user:user-100"),
            CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.StaffMemberNotFound, result.Error);
        Assert.Equal("user-200", member.AuthSubjectId);
        Assert.Equal((member.ScopeId, member.Id), Assert.Single(operationLock.Acquisitions));
        Assert.Equal(0, operations.GetCount);
        Assert.Empty(operations.Records);
    }

    [Fact]
    public async Task Safety_transition_reloads_through_the_restriction_bypass_after_locking()
    {
        StaffMember member = CreateMember("member-100");
        FakeStaffMemberRepository members = new(member)
        {
            OperationallyVisible = false
        };
        RecordingOperationLock operationLock = new();
        using ServiceProvider provider = CreateProvider(
            members,
            new FakePropertyProjectionRepository(),
            lifecyclePolicy: new RecordingLifecyclePolicy(
                StaffLifecyclePolicyDecision.Allowed),
            operationLock: operationLock);
        var handler = provider.GetRequiredService<
            ICommandHandler<SuspendStaffMemberCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            new SuspendStaffMemberCommand(
                Guid.NewGuid(),
                member.Id,
                "Privacy-safe access reduction",
                member.Version,
                "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(StaffStatus.Suspended, result.Value.Status);
        Assert.Equal((member.ScopeId, member.Id), Assert.Single(operationLock.Acquisitions));
        Assert.Equal(1, members.SafetyGetCount);
        Assert.Equal(0, members.OperationalGetCount);
    }

    [Theory]
    [InlineData(typeof(UpdateStaffMemberCommandHandler))]
    [InlineData(typeof(UpdateCurrentStaffMemberCommandHandler))]
    [InlineData(typeof(SetStaffAuthSubjectCommandHandler))]
    [InlineData(typeof(AssignStaffPropertyCommandHandler))]
    [InlineData(typeof(ResumeStaffMemberCommandHandler))]
    [InlineData(typeof(SuspendStaffMemberCommandHandler))]
    [InlineData(typeof(UnassignStaffPropertyCommandHandler))]
    [InlineData(typeof(DepartStaffMemberCommandHandler))]
    [InlineData(typeof(ProvisionStaffOnboardingCommandHandler))]
    [InlineData(typeof(ReconcileStaffIdentityCommandHandler))]
    [InlineData(typeof(ReconcileStaffPropertyAssignmentsCommandHandler))]
    public void Existing_member_mutations_use_the_shared_serialization_boundary(
        Type handlerType)
    {
        System.Reflection.ConstructorInfo constructor = Assert.Single(
            handlerType.GetConstructors(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic));

        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType ==
                typeof(StaffMemberMutationCoordinator));
    }

    [Fact]
    public async Task Identity_reconciliation_creates_one_active_staff_profile_for_the_auth_subject()
    {
        FakeStaffMemberRepository members = new();
        using ServiceProvider provider = CreateProvider(members, new FakePropertyProjectionRepository());
        ICommandHandler<ReconcileStaffIdentityCommand, Unit> handler = provider
            .GetRequiredService<ICommandHandler<ReconcileStaffIdentityCommand, Unit>>();
        ReconcileStaffIdentityCommand command = new(
            "member-100", "ada@example.test", "ada@example.test", true,
            "integration:organizations", "Workspace membership changed.");

        Result<Unit> first = await handler.HandleAsync(command, CancellationToken.None);
        Result<Unit> repeated = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(repeated.IsSuccess, repeated.Error.Code);
        Assert.NotNull(members.AddedMember);
        Assert.Equal("member-100", members.AddedMember.AuthSubjectId);
        Assert.Equal(StaffMemberState.Active, members.AddedMember.Status);
    }

    [Fact]
    public async Task Identity_reconciliation_suspends_and_resumes_an_existing_staff_profile()
    {
        StaffMember member = CreateMember("member-100");
        FakeStaffMemberRepository members = new(member);
        using ServiceProvider provider = CreateProvider(members, new FakePropertyProjectionRepository());
        ICommandHandler<ReconcileStaffIdentityCommand, Unit> handler = provider
            .GetRequiredService<ICommandHandler<ReconcileStaffIdentityCommand, Unit>>();

        Result<Unit> suspended = await handler.HandleAsync(new(
            "member-100", "Ada Operator", "ada@example.test", false,
            "integration:organizations", "Workspace membership changed."), CancellationToken.None);
        Result<Unit> resumed = await handler.HandleAsync(new(
            "member-100", "Ada Operator", "ada@example.test", true,
            "integration:organizations", "Workspace membership changed."), CancellationToken.None);

        Assert.True(suspended.IsSuccess, suspended.Error.Code);
        Assert.True(resumed.IsSuccess, resumed.Error.Code);
        Assert.Equal(StaffMemberState.Active, member.Status);
        Assert.Equal(3, member.Version);
    }

    [Fact]
    public async Task Identity_reconciliation_rejects_a_hidden_existing_staff_profile()
    {
        StaffMember member = CreateMember("member-100");
        FakeStaffMemberRepository members = new(member)
        {
            OperationallyVisible = false
        };
        using ServiceProvider provider = CreateProvider(
            members,
            new FakePropertyProjectionRepository());
        var handler = provider.GetRequiredService<
            ICommandHandler<ReconcileStaffIdentityCommand, Unit>>();

        Result<Unit> result = await handler.HandleAsync(
            new ReconcileStaffIdentityCommand(
                "member-100",
                "Ada Operator",
                "ada@example.test",
                IsActive: true,
                "integration:organizations",
                "Workspace membership changed."),
            CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.StaffMemberNotFound, result.Error);
        Assert.Null(members.AddedMember);
    }

    [Fact]
    public async Task Onboarding_provisioning_is_replay_safe_for_an_existing_auth_subject()
    {
        FakeStaffMemberRepository members = new();
        using ServiceProvider provider = CreateProvider(members, new FakePropertyProjectionRepository());
        ICommandHandler<ProvisionStaffOnboardingCommand, StaffMemberDto> handler = provider
            .GetRequiredService<ICommandHandler<ProvisionStaffOnboardingCommand, StaffMemberDto>>();
        ProvisionStaffOnboardingCommand command = new(
            "member-100", "Ada Operator", "Ada Lovelace", "ada@example.test", null,
            "EMP-100", "Manager", "Operations", "integration:organizations", "Onboarding accepted.");

        Result<StaffMemberDto> first = await handler.HandleAsync(command, CancellationToken.None);
        Result<StaffMemberDto> replayed = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(replayed.IsSuccess, replayed.Error.Code);
        Assert.Equal(first.Value.StaffMemberId, replayed.Value.StaffMemberId);
        Assert.Equal(1, members.AddCount);
        Assert.Equal("Ada Lovelace", replayed.Value.LegalName);
        Assert.Equal("EMP-100", replayed.Value.EmployeeNumber);
    }

    [Fact]
    public async Task Property_plan_reconciles_the_exact_active_set_and_preserves_history()
    {
        StaffMember member = CreateMember("member-100");
        Guid retainedPropertyId = Guid.NewGuid();
        Guid removedPropertyId = Guid.NewGuid();
        Guid addedPropertyId = Guid.NewGuid();
        Assert.True(member.AssignProperty(
            Guid.NewGuid(), retainedPropertyId, null, false, new DateOnly(2026, 7, 1),
            member.Version, "user:owner", Guid.NewGuid(), TestClock.Now).IsSuccess);
        Assert.True(member.AssignProperty(
            Guid.NewGuid(), removedPropertyId, null, false, new DateOnly(2026, 7, 1),
            member.Version, "user:owner", Guid.NewGuid(), TestClock.Now).IsSuccess);
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(retainedPropertyId, addedPropertyId));
        var handler = provider.GetRequiredService<
            ICommandHandler<ReconcileStaffPropertyAssignmentsCommand, IReadOnlyCollection<Guid>>>();

        Result<IReadOnlyCollection<Guid>> result = await handler.HandleAsync(
            new ReconcileStaffPropertyAssignmentsCommand(
                member.Id,
                [addedPropertyId, retainedPropertyId, addedPropertyId],
                "integration:workspaces",
                "Workspace access plan changed."),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(
            new[] { addedPropertyId, retainedPropertyId }.Order(),
            result.Value.Order());
        Assert.Equal(
            new[] { addedPropertyId, retainedPropertyId }.Order(),
            member.Assignments.Where(assignment => assignment.IsCurrent)
                .Select(assignment => assignment.PropertyId).Order());
        Assert.Contains(member.Assignments, assignment =>
            assignment.PropertyId == removedPropertyId && !assignment.IsCurrent);
    }

    [Fact]
    public async Task Property_plan_replay_does_not_advance_staff_history()
    {
        StaffMember member = CreateMember("member-100");
        Guid propertyId = Guid.NewGuid();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(propertyId));
        var handler = provider.GetRequiredService<
            ICommandHandler<ReconcileStaffPropertyAssignmentsCommand, IReadOnlyCollection<Guid>>>();
        ReconcileStaffPropertyAssignmentsCommand command = new(
            member.Id,
            [propertyId],
            "integration:workspaces",
            "Workspace access plan applied.");

        Result<IReadOnlyCollection<Guid>> first = await handler.HandleAsync(
            command,
            CancellationToken.None);
        long appliedVersion = member.Version;
        Result<IReadOnlyCollection<Guid>> replayed = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(replayed.IsSuccess, replayed.Error.Code);
        Assert.Equal(appliedVersion, member.Version);
        Assert.Single(member.Assignments);
    }

    [Fact]
    public async Task Property_plan_rejects_an_inactive_property_before_mutating_staff()
    {
        StaffMember member = CreateMember("member-100");
        Guid currentPropertyId = Guid.NewGuid();
        Assert.True(member.AssignProperty(
            Guid.NewGuid(), currentPropertyId, null, false, new DateOnly(2026, 7, 1),
            member.Version, "user:owner", Guid.NewGuid(), TestClock.Now).IsSuccess);
        long originalVersion = member.Version;
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(currentPropertyId));
        var handler = provider.GetRequiredService<
            ICommandHandler<ReconcileStaffPropertyAssignmentsCommand, IReadOnlyCollection<Guid>>>();

        Result<IReadOnlyCollection<Guid>> result = await handler.HandleAsync(
            new ReconcileStaffPropertyAssignmentsCommand(
                member.Id,
                [Guid.NewGuid()],
                "integration:workspaces",
                "Workspace access plan changed."),
            CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.PropertyUnavailable, result.Error);
        Assert.Equal(originalVersion, member.Version);
        Assert.True(Assert.Single(member.Assignments).IsCurrent);
    }

    [Fact]
    public async Task Suspend_prepares_workspace_lifecycle_before_returning_success()
    {
        StaffMember member = CreateMember("member-100");
        RecordingLifecyclePolicy policy = new(StaffLifecyclePolicyDecision.Allowed);
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            lifecyclePolicy: policy);
        var handler = provider.GetRequiredService<
            ICommandHandler<SuspendStaffMemberCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            new SuspendStaffMemberCommand(
                Guid.NewGuid(), member.Id, "Leave", member.Version, "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.NotNull(policy.Context);
        Assert.Equal(StaffLifecycleTransition.Suspend, policy.Context.Transition);
        Assert.Equal(StaffStatus.Active, policy.Context.PreviousStatus);
        Assert.Equal(StaffStatus.Suspended, policy.Context.TargetStatus);
        Assert.Equal("member-100", policy.Context.AuthSubjectId);
        Assert.Equal(2, policy.Context.TargetVersion);
    }

    [Fact]
    public async Task Suspend_replays_an_equivalent_operation_without_repeating_policy_or_event()
    {
        StaffMember member = CreateMember("member-100");
        RecordingLifecyclePolicy policy = new(StaffLifecyclePolicyDecision.Allowed);
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            lifecyclePolicy: policy,
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<SuspendStaffMemberCommand, StaffMemberMutationReceiptDto>>();
        Guid operationId = Guid.NewGuid();
        long expectedVersion = member.Version;

        Result<StaffMemberMutationReceiptDto> first = await handler.HandleAsync(
            new SuspendStaffMemberCommand(
                operationId,
                member.Id,
                "  Approved leave  ",
                expectedVersion,
                "user:owner"),
            CancellationToken.None);
        int eventCount = member.DomainEvents.Count;
        Result<StaffMemberMutationReceiptDto> replay = await handler.HandleAsync(
            new SuspendStaffMemberCommand(
                operationId,
                member.Id,
                "Approved leave",
                expectedVersion,
                "user:owner"),
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(StaffStatus.Suspended, replay.Value.Status);
        Assert.Equal(eventCount, member.DomainEvents.Count);
        Assert.Single(policy.Contexts);
        Assert.Equal(
            StaffMemberMutationKind.Suspend,
            Assert.Single(operations.Records).Kind);
    }

    [Fact]
    public async Task Lifecycle_operation_rejects_changed_and_cross_kind_reuse()
    {
        StaffMember member = CreateMember("member-100");
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            lifecyclePolicy: new RecordingLifecyclePolicy(
                StaffLifecyclePolicyDecision.Allowed),
            memberMutationOperations: operations);
        var suspend = provider.GetRequiredService<
            ICommandHandler<SuspendStaffMemberCommand, StaffMemberMutationReceiptDto>>();
        var resume = provider.GetRequiredService<
            ICommandHandler<ResumeStaffMemberCommand, StaffMemberMutationReceiptDto>>();
        Guid operationId = Guid.NewGuid();
        long expectedVersion = member.Version;

        Result<StaffMemberMutationReceiptDto> first = await suspend.HandleAsync(
            new SuspendStaffMemberCommand(
                operationId,
                member.Id,
                "Approved leave",
                expectedVersion,
                "user:owner"),
            CancellationToken.None);
        Result<StaffMemberMutationReceiptDto> changed = await suspend.HandleAsync(
            new SuspendStaffMemberCommand(
                operationId,
                member.Id,
                "Investigation",
                expectedVersion,
                "user:owner"),
            CancellationToken.None);
        Result<StaffMemberMutationReceiptDto> crossKind = await resume.HandleAsync(
            new ResumeStaffMemberCommand(
                operationId,
                member.Id,
                "Returned",
                expectedVersion,
                "user:owner"),
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.Equal(StaffApplicationErrors.LifecycleOperationConflict, changed.Error);
        Assert.Equal(StaffApplicationErrors.LifecycleOperationConflict, crossKind.Error);
        Assert.Single(operations.Records);
    }

    [Fact]
    public async Task Failed_lifecycle_coordination_does_not_bind_the_operation()
    {
        Guid staffMemberId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        RecordingMemberMutationOperations operations = new();
        StaffMember rejectedMember = CreateMemberWithId(staffMemberId, "member-100");
        SuspendStaffMemberCommand command = new(
            operationId,
            staffMemberId,
            "Approved leave",
            rejectedMember.Version,
            "user:owner");
        using (ServiceProvider rejectedProvider = CreateProvider(
            new FakeStaffMemberRepository(rejectedMember),
            new FakePropertyProjectionRepository(),
            lifecyclePolicy: new RecordingLifecyclePolicy(
                StaffLifecyclePolicyDecision.RetryRequired),
            memberMutationOperations: operations))
        {
            var rejectedHandler = rejectedProvider.GetRequiredService<
                ICommandHandler<SuspendStaffMemberCommand, StaffMemberMutationReceiptDto>>();
            Result<StaffMemberMutationReceiptDto> rejected =
                await rejectedHandler.HandleAsync(command, CancellationToken.None);

            Assert.Equal(
                StaffApplicationErrors.LifecycleCoordinationPending,
                rejected.Error);
            Assert.Empty(operations.Records);
        }

        StaffMember retriedMember = CreateMemberWithId(staffMemberId, "member-100");
        using ServiceProvider retriedProvider = CreateProvider(
            new FakeStaffMemberRepository(retriedMember),
            new FakePropertyProjectionRepository(),
            lifecyclePolicy: new RecordingLifecyclePolicy(
                StaffLifecyclePolicyDecision.Allowed),
            memberMutationOperations: operations);
        var retriedHandler = retriedProvider.GetRequiredService<
            ICommandHandler<SuspendStaffMemberCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> retried =
            await retriedHandler.HandleAsync(command, CancellationToken.None);

        Assert.True(retried.IsSuccess, retried.Error.Code);
        Assert.Single(operations.Records);
    }

    [Fact]
    public async Task Resume_maps_retryable_workspace_coordination_to_a_stable_staff_error()
    {
        StaffMember member = CreateMember("member-100");
        Assert.True(member.Suspend(
            member.Version, "user:owner", "Leave", Guid.NewGuid(), TestClock.Now).IsSuccess);
        RecordingLifecyclePolicy policy = new(StaffLifecyclePolicyDecision.RetryRequired);
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            lifecyclePolicy: policy);
        var handler = provider.GetRequiredService<
            ICommandHandler<ResumeStaffMemberCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            new ResumeStaffMemberCommand(
                Guid.NewGuid(), member.Id, "Returned", member.Version, "user:owner"),
            CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.LifecycleCoordinationPending, result.Error);
        Assert.NotNull(policy.Context);
        Assert.Equal(StaffLifecycleTransition.Resume, policy.Context.Transition);
        Assert.Equal(StaffStatus.Suspended, policy.Context.PreviousStatus);
        Assert.Equal(StaffStatus.Active, policy.Context.TargetStatus);
    }

    [Fact]
    public async Task Resume_replays_without_repeating_workspace_preparation()
    {
        StaffMember member = CreateMember("member-100");
        Assert.True(member.Suspend(
            member.Version,
            "user:owner",
            "Leave",
            Guid.NewGuid(),
            TestClock.Now).IsSuccess);
        RecordingLifecyclePolicy policy = new(StaffLifecyclePolicyDecision.Allowed);
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            lifecyclePolicy: policy,
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<ResumeStaffMemberCommand, StaffMemberMutationReceiptDto>>();
        Guid operationId = Guid.NewGuid();
        long expectedVersion = member.Version;
        ResumeStaffMemberCommand command = new(
            operationId,
            member.Id,
            "Returned",
            expectedVersion,
            "user:owner");

        Result<StaffMemberMutationReceiptDto> first = await handler.HandleAsync(
            command,
            CancellationToken.None);
        int eventCount = member.DomainEvents.Count;
        Result<StaffMemberMutationReceiptDto> replay = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(eventCount, member.DomainEvents.Count);
        Assert.Single(policy.Contexts);
        Assert.Equal(
            StaffMemberMutationKind.Resume,
            Assert.Single(operations.Records).Kind);
    }

    [Fact]
    public async Task Depart_forwards_the_effective_date_to_workspace_lifecycle_policy()
    {
        StaffMember member = CreateMember("member-100");
        RecordingLifecyclePolicy policy = new(StaffLifecyclePolicyDecision.Allowed);
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            lifecyclePolicy: policy);
        var handler = provider.GetRequiredService<
            ICommandHandler<DepartStaffMemberCommand, StaffMemberMutationReceiptDto>>();
        DateOnly effectiveOn = new(2026, 7, 21);

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            new DepartStaffMemberCommand(
                Guid.NewGuid(), member.Id, effectiveOn, "Contract ended",
                member.Version, "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.NotNull(policy.Context);
        Assert.Equal(StaffLifecycleTransition.Depart, policy.Context.Transition);
        Assert.Equal(StaffStatus.Departed, policy.Context.TargetStatus);
        Assert.Equal(effectiveOn, policy.Context.EffectiveOn);
    }

    [Fact]
    public async Task Depart_replays_without_closing_assignments_or_emitting_events_twice()
    {
        StaffMember member = CreateMember("member-100");
        Guid propertyId = Guid.NewGuid();
        Assert.True(member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            null,
            false,
            new DateOnly(2026, 7, 1),
            member.Version,
            "user:owner",
            Guid.NewGuid(),
            TestClock.Now).IsSuccess);
        RecordingLifecyclePolicy policy = new(StaffLifecyclePolicyDecision.Allowed);
        RecordingMemberMutationOperations operations = new();
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(propertyId),
            lifecyclePolicy: policy,
            memberMutationOperations: operations);
        var handler = provider.GetRequiredService<
            ICommandHandler<DepartStaffMemberCommand, StaffMemberMutationReceiptDto>>();
        DepartStaffMemberCommand command = new(
            Guid.NewGuid(),
            member.Id,
            new DateOnly(2026, 7, 21),
            "Contract ended",
            member.Version,
            "user:owner");

        Result<StaffMemberMutationReceiptDto> first = await handler.HandleAsync(
            command,
            CancellationToken.None);
        int eventCount = member.DomainEvents.Count;
        Result<StaffMemberMutationReceiptDto> replay = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(StaffStatus.Departed, replay.Value.Status);
        Assert.Equal(eventCount, member.DomainEvents.Count);
        Assert.False(Assert.Single(member.Assignments).IsCurrent);
        Assert.Single(policy.Contexts);
        Assert.Equal(
            StaffMemberMutationKind.Depart,
            Assert.Single(operations.Records).Kind);
    }

    [Fact]
    public async Task Owner_protection_is_reported_without_exposing_cross_module_details()
    {
        StaffMember member = CreateMember("member-100");
        using ServiceProvider provider = CreateProvider(
            new FakeStaffMemberRepository(member),
            new FakePropertyProjectionRepository(),
            lifecyclePolicy: new RecordingLifecyclePolicy(StaffLifecyclePolicyDecision.OwnerProtected));
        var handler = provider.GetRequiredService<
            ICommandHandler<SuspendStaffMemberCommand, StaffMemberMutationReceiptDto>>();

        Result<StaffMemberMutationReceiptDto> result = await handler.HandleAsync(
            new SuspendStaffMemberCommand(
                Guid.NewGuid(), member.Id, "Leave", member.Version, "user:owner"),
            CancellationToken.None);

        Assert.Equal(StaffApplicationErrors.WorkspaceOwnerProtected, result.Error);
    }

    private static ServiceProvider CreateProvider(
        IStaffMemberRepository members,
        IStaffPropertyProjectionRepository properties,
        IScopeContext? scope = null,
        IStaffLifecyclePolicy? lifecyclePolicy = null,
        IStaffOperationLock? operationLock = null,
        IStaffCreationOperationLock? creationLock = null,
        IStaffMemberMutationOperationRepository? memberMutationOperations = null)
    {
        ServiceCollection services = new();
        services.AddSingleton(members);
        services.AddSingleton(properties);
        services.AddSingleton(scope ?? new TestScopeContext(true, "tenant-a"));
        services.AddSingleton<ISystemClock>(new TestClock());
        services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        services.AddSingleton(operationLock ?? new NoopStaffOperationLock());
        services.AddSingleton(
            creationLock ?? new NoopStaffCreationOperationLock());
        services.AddSingleton(
            memberMutationOperations ?? new RecordingMemberMutationOperations());
        if (lifecyclePolicy is not null)
        {
            services.AddSingleton(lifecyclePolicy);
        }
        services.AddStaffApplication();
        return services.BuildServiceProvider();
    }

    private static CreateStaffMemberCommand CreateCommand(
        Guid? operationId = null,
        string displayName = "Ada Operator",
        string workEmail = "ada@example.test",
        string? employeeNumber = "EMP-100",
        string? authSubjectId = "user-100") => new(
        operationId ?? Guid.NewGuid(), displayName, null, workEmail, null, employeeNumber,
        "Manager", "Operations", authSubjectId, "user:owner");

    private static UpdateStaffMemberCommand UpdateCommand(
        StaffMember member,
        Guid operationId,
        long expectedVersion,
        string displayName) => new(
        operationId,
        member.Id,
        displayName,
        member.LegalName,
        member.WorkEmail,
        member.WorkPhone,
        member.EmployeeNumber,
        member.JobTitle,
        member.Department,
        expectedVersion,
        "user:owner");

    private static StaffMember CreateMember(string? authSubjectId = null) => StaffMember.Create(
        Guid.NewGuid(), "tenant-a", "Ada Operator", null, "ada@example.test", null,
        "EMP-100", "Manager", "Operations", authSubjectId, "user:owner", Guid.NewGuid(), TestClock.Now).Value;

    private static StaffMember CreateMemberWithId(
        Guid staffMemberId,
        string? authSubjectId = null) => StaffMember.Create(
        staffMemberId, "tenant-a", "Ada Operator", null, "ada@example.test", null,
        "EMP-100", "Manager", "Operations", authSubjectId, "user:owner",
        Guid.NewGuid(), TestClock.Now).Value;

    private static StaffMember CreateMemberForCreation(Guid id) => StaffMember.Create(
        id,
        "tenant-a",
        "Ada Operator",
        legalName: null,
        "ada@example.test",
        workPhone: null,
        "EMP-100",
        "Manager",
        "Operations",
        "user-100",
        "user:original-owner",
        Guid.NewGuid(),
        TestClock.Now).Value;

    private sealed class FakeStaffMemberRepository(StaffMember? member = null) : IStaffMemberRepository
    {
        public string? ExistingEmployeeNumber { get; init; }
        public string? ExistingAuthSubjectId { get; init; }
        public StaffMember? AddedMember { get; private set; }
        public int AddCount { get; private set; }
        public bool OperationallyVisible { get; set; } = true;
        public int OperationalGetCount { get; private set; }
        public int SafetyGetCount { get; private set; }
        public List<string>? Calls { get; init; }

        public Task AddAsync(StaffMember value, CancellationToken cancellationToken)
        {
            this.AddedMember = value;
            this.AddCount++;
            return Task.CompletedTask;
        }

        public Task<StaffMember?> GetAsync(Guid staffMemberId, CancellationToken cancellationToken)
        {
            this.Calls?.Add("operational-read");
            this.OperationalGetCount++;
            return Task.FromResult(
                this.OperationallyVisible
                    ? this.Candidates().FirstOrDefault(candidate => candidate.Id == staffMemberId)
                    : null);
        }

        public Task<StaffMember?> GetForDataRightsAsync(
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Candidates().FirstOrDefault(
                candidate => candidate.Id == staffMemberId));

        public Task<StaffMember?> GetForSafetyTransitionAsync(
            Guid staffMemberId,
            CancellationToken cancellationToken)
        {
            this.Calls?.Add("safety-read");
            this.SafetyGetCount++;
            return Task.FromResult(this.Candidates().FirstOrDefault(
                candidate => candidate.Id == staffMemberId));
        }

        public Task<StaffMember?> GetByAuthSubjectAsync(string authSubjectId,
            CancellationToken cancellationToken)
        {
            this.OperationalGetCount++;
            return Task.FromResult(
                this.OperationallyVisible
                    ? this.Candidates().FirstOrDefault(candidate => string.Equals(
                        candidate.AuthSubjectId,
                        authSubjectId.Trim(),
                        StringComparison.Ordinal))
                    : null);
        }

        public Task<StaffDirectoryMemberDto?> GetDirectoryAsync(Guid staffMemberId,
            CancellationToken cancellationToken) => Task.FromResult(
            member?.Id == staffMemberId ? ToDirectory(member) : null);

        public Task<StaffDirectoryMemberDto?> GetDirectoryAtPropertyAsync(Guid propertyId,
            Guid staffMemberId, CancellationToken cancellationToken) => Task.FromResult(
            member?.Id == staffMemberId ? ToDirectory(member, propertyId) : null);

        public Task<StaffDirectoryListResponse> ListDirectoryAsync(string? search, StaffStatus? status,
            PageRequest pageRequest, CancellationToken cancellationToken) =>
            Task.FromResult(new StaffDirectoryListResponse(
                [], pageRequest.Page, pageRequest.PageSize, false));

        public Task<StaffPropertyDirectoryListResponse> ListDirectoryAtPropertyAsync(Guid propertyId,
            string? search, StaffStatus? status, PageRequest pageRequest,
            CancellationToken cancellationToken) => Task.FromResult(
            new StaffPropertyDirectoryListResponse(
                [], pageRequest.Page, pageRequest.PageSize, false));

        public Task<bool> EmployeeNumberExistsAsync(string employeeNumber, Guid? exceptStaffMemberId,
            CancellationToken cancellationToken) => Task.FromResult(
            string.Equals(employeeNumber, this.ExistingEmployeeNumber, StringComparison.Ordinal) ||
            this.Candidates().Any(candidate =>
                candidate.Id != exceptStaffMemberId &&
                string.Equals(
                    candidate.EmployeeNumber,
                    employeeNumber,
                    StringComparison.OrdinalIgnoreCase)));

        public Task<bool> AuthSubjectExistsAsync(string authSubjectId, Guid? exceptStaffMemberId,
            CancellationToken cancellationToken) => Task.FromResult(
            string.Equals(authSubjectId, this.ExistingAuthSubjectId, StringComparison.Ordinal) ||
            this.Candidates().Any(candidate =>
                candidate.Id != exceptStaffMemberId &&
                string.Equals(
                    candidate.AuthSubjectId,
                    authSubjectId,
                    StringComparison.Ordinal)));

        private IEnumerable<StaffMember> Candidates() =>
            new[] { member, this.AddedMember }.OfType<StaffMember>().Distinct();

        private static StaffDirectoryMemberDto ToDirectory(StaffMember value, Guid? propertyId = null) => new(
            value.Id,
            value.DisplayName,
            value.JobTitle,
            value.Department,
            (StaffStatus)value.Status,
            value.Version,
            value.Assignments
                .Where(assignment => assignment.IsCurrent &&
                    (!propertyId.HasValue || assignment.PropertyId == propertyId.Value))
                .Select(assignment => new StaffDirectoryAssignmentDto(
                    assignment.Id,
                    assignment.PropertyId,
                    assignment.PropertyJobTitle,
                    assignment.IsPrimary,
                    assignment.EffectiveFrom))
                .ToArray());
    }

    private sealed class FakePropertyProjectionRepository(params Guid[] activeProperties)
        : IStaffPropertyProjectionRepository
    {
        public Task<bool> IsActiveAsync(Guid propertyId, CancellationToken cancellationToken) =>
            Task.FromResult(activeProperties.Contains(propertyId));

        public Task<bool> AreAllActiveAsync(
            IReadOnlyCollection<Guid> propertyIds,
            CancellationToken cancellationToken) => Task.FromResult(
            propertyIds.All(activeProperties.Contains));

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

    private sealed class RecordingLifecyclePolicy(StaffLifecyclePolicyDecision decision)
        : IStaffLifecyclePolicy
    {
        public List<StaffLifecyclePolicyContext> Contexts { get; } = [];
        public StaffLifecyclePolicyContext? Context => this.Contexts.LastOrDefault();

        public ValueTask<StaffLifecyclePolicyDecision> PrepareAsync(
            StaffLifecyclePolicyContext context,
            CancellationToken cancellationToken = default)
        {
            this.Contexts.Add(context);
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class RecordingOperationLock(Action? onAcquire = null)
        : IStaffOperationLock
    {
        public List<(string TenantId, Guid StaffMemberId)> Acquisitions { get; } = [];

        public Task<long?> GetStaffMemberRevisionAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken) => Task.FromResult<long?>(1);

        public Task<bool> TryAcquireStaffMemberAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken)
        {
            this.Acquisitions.Add((tenantId, staffMemberId));
            onAcquire?.Invoke();
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingCreationOperationLock(
        ICollection<string>? calls = null) : IStaffCreationOperationLock
    {
        public List<(string TenantId, Guid OperationId)> Acquisitions { get; } = [];

        public Task AcquireAsync(
            string tenantId,
            Guid operationId,
            CancellationToken cancellationToken)
        {
            calls?.Add("creation-lock");
            this.Acquisitions.Add((tenantId, operationId));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMemberMutationOperations
        : IStaffMemberMutationOperationRepository
    {
        public List<StaffMemberMutationOperationRecord> Records { get; } = [];
        public int GetCount { get; private set; }

        public Task<StaffMemberMutationOperationRecord?> GetAsync(
            Guid staffMemberId,
            Guid operationId,
            CancellationToken cancellationToken)
        {
            this.GetCount++;
            return Task.FromResult(this.Records.SingleOrDefault(record =>
                record.StaffMemberId == staffMemberId &&
                record.OperationId == operationId));
        }

        public Task AddAsync(
            StaffMemberMutationOperationRecord operation,
            CancellationToken cancellationToken)
        {
            this.Records.Add(operation);
            return Task.CompletedTask;
        }

        public Task DeleteForStaffMemberAsync(
            Guid staffMemberId,
            CancellationToken cancellationToken)
        {
            this.Records.RemoveAll(record =>
                record.StaffMemberId == staffMemberId);
            return Task.CompletedTask;
        }
    }
}
