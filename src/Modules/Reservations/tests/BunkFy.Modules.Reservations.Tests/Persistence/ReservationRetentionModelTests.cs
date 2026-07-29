namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Retention;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationRetentionModelTests
{
    [Fact]
    public void Model_enforces_terminal_cursor_and_proof_shape()
    {
        using ReservationsDbContext dbContext = CreateDbContext();
        IModel designModel =
            dbContext.GetService<IDesignTimeModel>().Model;
        IEntityType reservation = designModel.FindEntityType(
            typeof(Reservation))!;
        IEntityType execution = designModel.FindEntityType(
            typeof(ReservationRetentionExecution))!;
        IEntityType checkpoint = designModel.FindEntityType(
            typeof(ReservationRetentionSweepCheckpoint))!;
        IEntityType receipt = designModel.FindEntityType(
            typeof(ReservationRetentionAnonymisationReceipt))!;
        IEntityType tombstone = designModel.FindEntityType(
            typeof(ReservationAnonymisationTombstone))!;

        Assert.Contains(
            reservation.GetCheckConstraints(),
            constraint =>
                constraint.Name == "CK_reservations_terminal_time");
        Assert.True(execution.FindProperty(
            nameof(ReservationRetentionExecution.Version))!
            .IsConcurrencyToken);
        Assert.Contains(
            execution.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_reservation_retention_executions_state");
        Assert.Contains(
            checkpoint.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(
                            ReservationRetentionSweepCheckpoint
                                .ScopeId),
                        nameof(
                            ReservationRetentionSweepCheckpoint
                                .DataClassKey),
                        nameof(
                            ReservationRetentionSweepCheckpoint
                                .ExecutionPolicyVersion)
                    ]));
        Assert.Equal(
            2,
            receipt.GetForeignKeys().Count(foreignKey =>
                foreignKey.PrincipalEntityType.ClrType is not null));
        Assert.Contains(
            receipt.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(
                            ReservationRetentionAnonymisationReceipt
                                .ScopeId),
                        nameof(
                            ReservationRetentionAnonymisationReceipt
                                .ReservationId)
                    ]));
        Assert.False(tombstone.FindProperty(
            nameof(ReservationAnonymisationTombstone.Authority))!
            .IsNullable);
    }

    [Fact]
    public async Task Retention_receipt_is_append_only()
    {
        await using ReservationsDbContext dbContext =
            CreateDbContext();
        Reservation reservation = CreateTerminalReservation(
            "tenant-a",
            Guid.NewGuid());
        DateTimeOffset now =
            ReservationRetentionTestData.Now;
        ReservationRetentionExecution execution =
            ReservationRetentionExecution.Start(
                Guid.NewGuid(),
                reservation.ScopeId,
                "reservation-operational",
                executionPolicyVersion: 1,
                attempt: 1,
                startingProjectionOrdinal: 0,
                now.AddMinutes(-1),
                now.AddMinutes(10)).Value;
        ReservationAnonymisationOutcome outcome =
            reservation.Anonymise(
                reservation.Version,
                reservation.DetailsRevision,
                "system:retention",
                Guid.NewGuid(),
                now).Value;
        ReservationRetentionAnonymisationReceipt receipt =
            ReservationRetentionAnonymisationReceipt.Create(
                Guid.NewGuid(),
                reservation.ScopeId,
                execution.Id,
                reservation.PropertyId,
                reservation.Id,
                outcome,
                reservation.TerminalAtUtc!.Value,
                now.AddDays(-1),
                new string('a', 64),
                redactedHistoryCount: 1,
                reducedExternalOperationCount: 0,
                suppressedReminderCount: 0).Value;
        dbContext.Reservations.Add(reservation);
        dbContext.RetentionExecutions.Add(execution);
        dbContext.RetentionAnonymisationReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        dbContext.Entry(receipt)
            .Property(nameof(
                ReservationRetentionAnonymisationReceipt
                    .PolicyEvidenceSha256))
            .CurrentValue = new string('b', 64);

        InvalidOperationException failure =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dbContext.SaveChangesAsync());
        Assert.Equal(
            "Reservation retention anonymisation receipts are append-only.",
            failure.Message);
    }

    [Fact]
    public async Task Checkpoint_lookup_isolated_by_execution_policy_version()
    {
        await using ReservationsDbContext dbContext =
            CreateDbContext();
        ReservationRetentionSweepCheckpoint versionOne =
            ReservationRetentionSweepCheckpoint.Create(
                Guid.NewGuid(),
                "tenant-a",
                "reservation-operational",
                executionPolicyVersion: 1,
                ReservationRetentionTestData.Now).Value;
        ReservationRetentionSweepCheckpoint versionTwo =
            ReservationRetentionSweepCheckpoint.Create(
                Guid.NewGuid(),
                "tenant-a",
                "reservation-operational",
                executionPolicyVersion: 2,
                ReservationRetentionTestData.Now).Value;
        dbContext.RetentionSweepCheckpoints.AddRange(
            versionOne,
            versionTwo);
        await dbContext.SaveChangesAsync();
        var repository =
            new ReservationRetentionExecutionRepository(dbContext);

        Assert.Same(
            versionOne,
            await repository.GetCheckpointAsync(
                "reservation-operational",
                executionPolicyVersion: 1,
                CancellationToken.None));
        Assert.Same(
            versionTwo,
            await repository.GetCheckpointAsync(
                "reservation-operational",
                executionPolicyVersion: 2,
                CancellationToken.None));
    }

    internal static Reservation CreateTerminalReservation(
        string tenantId,
        Guid propertyId,
        Guid? reservationId = null)
    {
        Reservation reservation = Reservation.Create(
            reservationId ?? Guid.NewGuid(),
            tenantId,
            propertyId,
            Guid.NewGuid(),
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 3),
            [Guid.NewGuid()],
            "Original Guest",
            "original@example.test",
            null,
            1,
            ReservationSource.Direct,
            null,
            null,
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationDetailsChangeOrigin.Staff,
            "user:creator",
            null,
            null,
            Guid.NewGuid(),
            ReservationRetentionTestData.Now.AddDays(-401),
            expectedArrivalTime: null,
            expectedDepartureTime: null).Value;
        Assert.True(reservation.RejectAllocation(
            reservation.AllocationRequestId,
            ReservationAllocationRejection.UnitNotSellable,
            Guid.NewGuid(),
            ReservationRetentionTestData.Now.AddDays(-400))
            .IsSuccess);
        reservation.ClearDomainEvents();
        return reservation;
    }

    private static ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase(
                    $"reservation-retention-model-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext("tenant-a"));
    }

    internal sealed class TestScopeContext(string scopeId)
        : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => scopeId;
    }
}
