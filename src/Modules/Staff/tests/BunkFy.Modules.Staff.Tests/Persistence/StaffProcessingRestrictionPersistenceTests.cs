namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Staff.Persistence.Models;
using BunkFy.Modules.Staff.Persistence.Repositories;
using Gma.Framework.Pagination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffProcessingRestrictionPersistenceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Member_creation_initializes_projection_and_operation_lock()
    {
        TestScopeContext scope = new();
        await using StaffDbContext dbContext = CreateDbContext(scope);
        StaffMemberRepository members = new(dbContext);
        StaffMember member = CreateMember("Created", "auth-created");

        await members.AddAsync(member, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        StaffProcessingRestrictionProjection projection =
            await dbContext.ProcessingRestrictionProjections.SingleAsync();
        Assert.Equal(member.Id, projection.StaffMemberId);
        Assert.Equal(
            StaffProcessingRestrictionContract.CurrentVersion,
            projection.ContractVersion);
        Assert.False(projection.IsRestricted);
        Assert.Equal(0, projection.ActiveRestrictionCount);
        Assert.Equal(0, projection.Revision);
        StaffOperationLock resourceLock =
            await dbContext.OperationLocks.SingleAsync();
        Assert.Equal(member.Id, resourceLock.Id);
        Assert.Equal(member.Id, resourceLock.StaffMemberId);
        Assert.Equal(1, resourceLock.Revision);
    }

    [Fact]
    public async Task Operational_reads_fail_closed_while_rights_and_safety_reads_remain_available()
    {
        TestScopeContext scope = new();
        await using StaffDbContext dbContext = CreateDbContext(scope);
        StaffMemberRepository members = new(dbContext);
        StaffMember unrestricted =
            CreateMember("Unrestricted", "auth-unrestricted");
        StaffMember restricted =
            CreateMember("Restricted", "auth-restricted");
        StaffMember missing =
            CreateMember("Missing projection", "auth-missing");
        StaffMember future =
            CreateMember("Future projection", "auth-future");

        await members.AddAsync(unrestricted, CancellationToken.None);
        await members.AddAsync(restricted, CancellationToken.None);
        dbContext.StaffMembers.AddRange(missing, future);
        dbContext.ProcessingRestrictionProjections.Add(
            StaffProcessingRestrictionProjection.Create(
                scope.ScopeId,
                future.Id,
                StaffProcessingRestrictionContract.CurrentVersion + 1,
                Now).Value);

        StaffProcessingRestrictionProjection restrictedProjection =
            dbContext.ProcessingRestrictionProjections.Local.Single(
                projection =>
                    projection.StaffMemberId == restricted.Id);
        Assert.True(restrictedProjection.Apply(
            0,
            StaffProcessingRestrictionContract.CurrentVersion,
            Now.AddMinutes(1)).IsSuccess);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        Assert.NotNull(await members.GetAsync(
            unrestricted.Id,
            CancellationToken.None));
        Assert.Null(await members.GetAsync(
            restricted.Id,
            CancellationToken.None));
        Assert.Null(await members.GetAsync(
            missing.Id,
            CancellationToken.None));
        Assert.Null(await members.GetAsync(
            future.Id,
            CancellationToken.None));
        Assert.Null(await members.GetByAuthSubjectAsync(
            "auth-restricted",
            CancellationToken.None));
        Assert.Null(await members.GetDirectoryAsync(
            restricted.Id,
            CancellationToken.None));

        StaffDirectoryListResponse visible = await members.ListDirectoryAsync(
            search: null,
            status: null,
            new PageRequest(1, 20),
            CancellationToken.None);
        StaffDirectoryListItemDto onlyVisible = Assert.Single(visible.Items);
        Assert.Equal(unrestricted.Id, onlyVisible.StaffMemberId);

        Assert.NotNull(await members.GetForDataRightsAsync(
            restricted.Id,
            CancellationToken.None));
        Assert.NotNull(await members.GetForDataRightsAsync(
            missing.Id,
            CancellationToken.None));
        Assert.NotNull(await members.GetForDataRightsAsync(
            future.Id,
            CancellationToken.None));
        Assert.NotNull(await members.GetForSafetyTransitionAsync(
            restricted.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task Gate_distinguishes_allowed_restricted_unknown_and_unsupported_state()
    {
        TestScopeContext scope = new();
        await using StaffDbContext dbContext = CreateDbContext(scope);
        Guid allowedId = Guid.NewGuid();
        Guid restrictedId = Guid.NewGuid();
        Guid futureId = Guid.NewGuid();
        StaffProcessingRestrictionProjection allowed =
            CreateProjection(
                scope.ScopeId,
                allowedId,
                StaffProcessingRestrictionContract.CurrentVersion);
        StaffProcessingRestrictionProjection restricted =
            CreateProjection(
                scope.ScopeId,
                restrictedId,
                StaffProcessingRestrictionContract.CurrentVersion);
        Assert.True(restricted.Apply(
            0,
            StaffProcessingRestrictionContract.CurrentVersion,
            Now.AddMinutes(1)).IsSuccess);
        StaffProcessingRestrictionProjection future =
            CreateProjection(
                scope.ScopeId,
                futureId,
                StaffProcessingRestrictionContract.CurrentVersion + 1);
        dbContext.ProcessingRestrictionProjections.AddRange(
            allowed,
            restricted,
            future);
        await dbContext.SaveChangesAsync();
        StaffProcessingRestrictionGate gate = new(dbContext, scope);

        StaffProcessingRestrictionGateResult allowedResult =
            await gate.EvaluateAsync(
                new(scope.ScopeId, allowedId),
                CancellationToken.None);
        StaffProcessingRestrictionGateResult restrictedResult =
            await gate.EvaluateAsync(
                new(scope.ScopeId, restrictedId),
                CancellationToken.None);
        StaffProcessingRestrictionGateResult unknownResult =
            await gate.EvaluateAsync(
                new(scope.ScopeId, Guid.NewGuid()),
                CancellationToken.None);
        StaffProcessingRestrictionGateResult futureResult =
            await gate.EvaluateAsync(
                new(scope.ScopeId, futureId),
                CancellationToken.None);
        StaffProcessingRestrictionGateResult requestVersionResult =
            await gate.EvaluateAsync(
                new(
                    scope.ScopeId,
                    allowedId,
                    StaffProcessingRestrictionContract.CurrentVersion + 1),
                CancellationToken.None);
        StaffProcessingRestrictionGateResult tenantMismatch =
            await gate.EvaluateAsync(
                new("tenant-b", allowedId),
                CancellationToken.None);

        Assert.Equal(
            StaffProcessingRestrictionDecision.Allowed,
            allowedResult.Decision);
        Assert.True(allowedResult.IsAllowed);
        Assert.Equal(
            StaffProcessingRestrictionDecision.Restricted,
            restrictedResult.Decision);
        Assert.Equal(
            StaffProcessingRestrictionDecision.Unknown,
            unknownResult.Decision);
        Assert.Equal(
            StaffProcessingRestrictionDecision.UnsupportedContractVersion,
            futureResult.Decision);
        Assert.Equal(
            StaffProcessingRestrictionContract.CurrentVersion + 1,
            futureResult.ObservedContractVersion);
        Assert.Equal(
            StaffProcessingRestrictionDecision.UnsupportedContractVersion,
            requestVersionResult.Decision);
        Assert.Equal(
            StaffProcessingRestrictionDecision.Unknown,
            tenantMismatch.Decision);
    }

    private static StaffMember CreateMember(
        string displayName,
        string authSubjectId) =>
        StaffMember.Create(
            Guid.NewGuid(),
            "tenant-a",
            displayName,
            null,
            null,
            null,
            null,
            null,
            null,
            authSubjectId,
            "staff:test",
            Guid.NewGuid(),
            Now).Value;

    private static StaffProcessingRestrictionProjection CreateProjection(
        string tenantId,
        Guid staffMemberId,
        int contractVersion) =>
        StaffProcessingRestrictionProjection.Create(
            tenantId,
            staffMemberId,
            contractVersion,
            Now).Value;

    private static StaffDbContext CreateDbContext(IScopeContext scope) => new(
        new DbContextOptionsBuilder<StaffDbContext>()
            .UseInMemoryDatabase(
                $"staff-restriction-persistence-{Guid.NewGuid():N}")
            .Options,
        scope);

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
