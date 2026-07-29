namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationRetentionCandidateRepositoryTests
{
    [Fact]
    public async Task Scan_is_bounded_aggregated_and_tenant_scoped()
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase(
                    $"reservation-retention-candidates-" +
                    $"{Guid.NewGuid():N}")
                .Options;
        ReservationRetentionPolicyFixture policy =
            ReservationRetentionTestData.CreatePolicy();
        Guid tenantAReservationId = Guid.NewGuid();
        await SeedAsync(
            options,
            "tenant-a",
            tenantAReservationId,
            policy.Binding);
        await SeedAsync(
            options,
            "tenant-b",
            Guid.NewGuid(),
            policy.Binding);

        await using ReservationsDbContext dbContext = new(
            options,
            new ReservationRetentionModelTests.TestScopeContext(
                "tenant-a"));
        ReservationRetentionCandidateRepository repository =
            new(dbContext);

        ReservationRetentionScanPage page =
            await repository.ScanAsync(
                afterProjectionOrdinal: 0,
                limit: 1,
                CancellationToken.None);

        ReservationRetentionCandidateSnapshot candidate =
            Assert.Single(page.Candidates);
        Assert.Equal(tenantAReservationId, candidate.ReservationId);
        Assert.True(page.ReachedEnd);
        Assert.Equal(0, candidate.ActiveHoldCount);
        Assert.Equal(
            ReservationProcessingRestrictionContract.CurrentVersion,
            candidate.ProcessingRestrictionContractVersion);
        Assert.NotNull(candidate.Property);
        Assert.True(candidate.Property.IsKnown);
        Assert.NotNull(candidate.Property.GovernancePolicy);
    }

    private static async Task SeedAsync(
        DbContextOptions<ReservationsDbContext> options,
        string tenantId,
        Guid reservationId,
        PropertyGovernancePolicyBinding policy)
    {
        await using ReservationsDbContext dbContext = new(
            options,
            new ReservationRetentionModelTests.TestScopeContext(
                tenantId));
        Guid propertyId = Guid.NewGuid();
        Reservation reservation =
            ReservationRetentionModelTests.CreateTerminalReservation(
                tenantId,
                propertyId,
                reservationId);
        dbContext.Entry(reservation)
            .Property(item => item.ProjectionOrdinal)
            .CurrentValue = string.Equals(
                tenantId,
                "tenant-a",
                StringComparison.Ordinal)
                    ? 1
                    : 2;
        ReservationPropertyProjection property =
            ReservationPropertyProjection.Create(
                propertyId,
                tenantId);
        property.ApplyTopology(
            "UTC",
            isActive: true,
            sourceVersion: 1);
        property.ApplyPolicy(
            PropertyProcessingStatus.Enabled,
            policy,
            sourceVersion: 1);
        ReservationProcessingRestrictionProjection restriction =
            ReservationProcessingRestrictionProjection.Create(
                tenantId,
                propertyId,
                reservationId,
                ReservationProcessingRestrictionContract
                    .CurrentVersion,
                ReservationRetentionTestData.Now.AddDays(-401))
            .Value;

        dbContext.Reservations.Add(reservation);
        dbContext.PropertyProjections.Add(property);
        dbContext.ProcessingRestrictionProjections.Add(restriction);
        await dbContext.SaveChangesAsync();
    }
}
