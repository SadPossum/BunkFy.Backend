namespace BunkFy.Modules.Guests.Tests.Persistence;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using BunkFy.Modules.Guests.Domain.Retention;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.TimeZones;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestRetentionModelTests
{
    [Fact]
    public void Model_enforces_scoped_execution_cursor_and_proof_shape()
    {
        using GuestsDbContext dbContext = CreateDbContext();
        IModel designModel =
            dbContext.GetService<IDesignTimeModel>().Model;
        IEntityType execution = designModel.FindEntityType(
            typeof(GuestRetentionExecution))!;
        IEntityType checkpoint = designModel.FindEntityType(
            typeof(GuestRetentionSweepCheckpoint))!;
        IEntityType receipt = designModel.FindEntityType(
            typeof(GuestRetentionAnonymisationReceipt))!;
        IEntityType tombstone = designModel.FindEntityType(
            typeof(GuestAnonymisationTombstone))!;

        Assert.True(execution.FindProperty(
            nameof(GuestRetentionExecution.Version))!.IsConcurrencyToken);
        Assert.Contains(
            execution.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_guest_retention_executions_state");
        Assert.Contains(
            execution.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_guest_retention_executions_version");
        Assert.Contains(
            receipt.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_guest_retention_receipts_actor");
        Assert.Contains(
            checkpoint.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(GuestRetentionSweepCheckpoint.ScopeId),
                        nameof(GuestRetentionSweepCheckpoint.DataClassKey)
                    ]));
        Assert.Equal(
            2,
            receipt.GetForeignKeys().Count(foreignKey =>
                foreignKey.PrincipalEntityType.ClrType is
                    not null));
        Assert.Contains(
            receipt.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(GuestRetentionAnonymisationReceipt.ScopeId),
                        nameof(GuestRetentionAnonymisationReceipt.GuestId)
                    ]));
        Assert.False(tombstone.FindProperty(
            nameof(GuestAnonymisationTombstone.Authority))!.IsNullable);
    }

    [Fact]
    public async Task Retention_receipt_is_append_only()
    {
        await using GuestsDbContext dbContext = CreateDbContext();
        GuestProfile profile = GuestProfile.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            "Guest",
            legalName: null,
            email: "guest@example.test",
            phone: null,
            dateOfBirth: null,
            nationalityCountryCode: null,
            preferredLanguageTag: null,
            notes: null,
            "user:creator",
            Guid.NewGuid(),
            new DateTimeOffset(
                2024,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero)).Value;
        DateTimeOffset now =
            new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
        GuestRetentionExecution execution =
            GuestRetentionExecution.Start(
                Guid.NewGuid(),
                profile.ScopeId,
                "guest-operational",
                executionPolicyVersion:
                    GuestRetentionExecution.MinimumRunningPolicyVersion,
                attempt: 1,
                startingProjectionOrdinal: 0,
                now.AddMinutes(-1),
                now.AddMinutes(10)).Value;
        GuestProfileAnonymisationOutcome outcome =
            profile.AnonymiseForRetention(
                profile.Version,
                "system:retention",
                Guid.NewGuid(),
                now).Value;
        GuestRetentionAnonymisationReceipt receipt =
            GuestRetentionAnonymisationReceipt.Create(
                Guid.NewGuid(),
                profile.ScopeId,
                execution.Id,
                profile.Id,
                outcome.PreviousVersion,
                outcome.CurrentVersion,
                affectedPropertyCount: 1,
                now.AddDays(-1),
                new string('a', 64),
                TimeZoneCatalog.Default.CatalogVersion,
                outcome.EventId,
                "system:retention",
                now).Value;
        dbContext.GuestProfiles.Add(profile);
        dbContext.RetentionExecutions.Add(execution);
        dbContext.RetentionAnonymisationReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        dbContext.Entry(receipt)
            .Property(nameof(
                GuestRetentionAnonymisationReceipt.PolicySetSha256))
            .CurrentValue = new string('b', 64);

        InvalidOperationException failure =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dbContext.SaveChangesAsync());
        Assert.Equal(
            "Guest anonymisation receipts are append-only.",
            failure.Message);
    }

    private static GuestsDbContext CreateDbContext()
    {
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseInMemoryDatabase(
                    $"guest-retention-model-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
