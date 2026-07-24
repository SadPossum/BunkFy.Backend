namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Guests.Persistence.Repositories;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestProcessingRestrictionGateTests
{
    [Fact]
    public async Task Gate_distinguishes_allowed_restricted_unknown_and_unsupported_state()
    {
        TestScopeContext scope = new();
        await using GuestsDbContext dbContext = CreateDbContext(scope);
        Guid propertyId = Guid.NewGuid();
        Guid allowedGuestId = Guid.NewGuid();
        Guid restrictedGuestId = Guid.NewGuid();
        Guid futureGuestId = Guid.NewGuid();
        DateTimeOffset nowUtc = new(2026, 7, 24, 17, 30, 0, TimeSpan.Zero);
        GuestProcessingRestrictionProjection allowed = CreateProjection(
            scope.ScopeId,
            propertyId,
            allowedGuestId,
            GuestProcessingRestrictionContract.CurrentVersion,
            nowUtc);
        GuestProcessingRestrictionProjection restricted = CreateProjection(
            scope.ScopeId,
            propertyId,
            restrictedGuestId,
            GuestProcessingRestrictionContract.CurrentVersion,
            nowUtc);
        Assert.True(restricted.Apply(
            0,
            GuestProcessingRestrictionContract.CurrentVersion,
            nowUtc.AddMinutes(1)).IsSuccess);
        GuestProcessingRestrictionProjection future = CreateProjection(
            scope.ScopeId,
            propertyId,
            futureGuestId,
            GuestProcessingRestrictionContract.CurrentVersion + 1,
            nowUtc);
        dbContext.ProcessingRestrictionProjections.AddRange(allowed, restricted, future);
        await dbContext.SaveChangesAsync();
        GuestProcessingRestrictionGate gate = new(dbContext, scope);

        GuestProcessingRestrictionGateResult allowedResult = await gate.EvaluateAsync(
            new(scope.ScopeId, propertyId, allowedGuestId),
            CancellationToken.None);
        GuestProcessingRestrictionGateResult restrictedResult = await gate.EvaluateAsync(
            new(scope.ScopeId, propertyId, restrictedGuestId),
            CancellationToken.None);
        GuestProcessingRestrictionGateResult unknownResult = await gate.EvaluateAsync(
            new(scope.ScopeId, propertyId, Guid.NewGuid()),
            CancellationToken.None);
        GuestProcessingRestrictionGateResult futureResult = await gate.EvaluateAsync(
            new(scope.ScopeId, propertyId, futureGuestId),
            CancellationToken.None);
        GuestProcessingRestrictionGateResult requestVersionResult = await gate.EvaluateAsync(
            new(
                scope.ScopeId,
                propertyId,
                allowedGuestId,
                GuestProcessingRestrictionContract.CurrentVersion + 1),
            CancellationToken.None);
        GuestProcessingRestrictionGateResult tenantMismatch = await gate.EvaluateAsync(
            new("tenant-b", propertyId, allowedGuestId),
            CancellationToken.None);

        Assert.Equal(GuestProcessingRestrictionDecision.Allowed, allowedResult.Decision);
        Assert.True(allowedResult.IsAllowed);
        Assert.Equal(GuestProcessingRestrictionDecision.Restricted, restrictedResult.Decision);
        Assert.Equal(GuestProcessingRestrictionDecision.Unknown, unknownResult.Decision);
        Assert.Equal(
            GuestProcessingRestrictionDecision.UnsupportedContractVersion,
            futureResult.Decision);
        Assert.Equal(
            GuestProcessingRestrictionContract.CurrentVersion + 1,
            futureResult.ObservedContractVersion);
        Assert.Equal(
            GuestProcessingRestrictionDecision.UnsupportedContractVersion,
            requestVersionResult.Decision);
        Assert.Equal(GuestProcessingRestrictionDecision.Unknown, tenantMismatch.Decision);
    }

    [Fact]
    public async Task Restriction_projection_export_uses_a_bounded_monotonic_cursor()
    {
        TestScopeContext scope = new();
        await using GuestsDbContext dbContext = CreateDbContext(scope);
        Guid propertyId = Guid.NewGuid();
        DateTimeOffset nowUtc = new(2026, 7, 24, 17, 30, 0, TimeSpan.Zero);
        GuestProcessingRestrictionProjection firstProjection = CreateProjection(
            scope.ScopeId,
            propertyId,
            Guid.NewGuid(),
            GuestProcessingRestrictionContract.CurrentVersion,
            nowUtc);
        GuestProcessingRestrictionProjection secondProjection = CreateProjection(
            scope.ScopeId,
            propertyId,
            Guid.NewGuid(),
            GuestProcessingRestrictionContract.CurrentVersion,
            nowUtc);
        dbContext.ProcessingRestrictionProjections.AddRange(firstProjection, secondProjection);
        dbContext.Entry(firstProjection)
            .Property(projection => projection.ProjectionOrdinal)
            .CurrentValue = 1;
        dbContext.Entry(secondProjection)
            .Property(projection => projection.ProjectionOrdinal)
            .CurrentValue = 2;
        await dbContext.SaveChangesAsync();
        GuestProcessingRestrictionProjectionExportSource source = new(dbContext);
        ProjectionRebuildRequest request = new(
            "guest-processing-restrictions",
            1,
            batchSize: 1,
            dryRun: false,
            cursor: null);

        ProjectionReadBatch<GuestProcessingRestrictionProjectionExport> first =
            await source.ReadAsync(request, null, CancellationToken.None);
        ProjectionReadBatch<GuestProcessingRestrictionProjectionExport> second =
            await source.ReadAsync(request, first.NextCursor, CancellationToken.None);

        Assert.Single(first.Snapshots);
        Assert.True(first.HasMore);
        Assert.NotNull(first.NextCursor);
        Assert.Single(second.Snapshots);
        Assert.False(second.HasMore);
        Assert.NotEqual(first.Snapshots.Single().GuestId, second.Snapshots.Single().GuestId);
    }

    private static GuestProcessingRestrictionProjection CreateProjection(
        string tenantId,
        Guid propertyId,
        Guid guestId,
        int contractVersion,
        DateTimeOffset nowUtc) => GuestProcessingRestrictionProjection.Create(
        tenantId,
        propertyId,
        guestId,
        contractVersion,
        nowUtc).Value;

    private static GuestsDbContext CreateDbContext(IScopeContext scope)
    {
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseInMemoryDatabase($"guest-restriction-gate-{Guid.NewGuid():N}")
                .Options;
        return new(options, scope);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
