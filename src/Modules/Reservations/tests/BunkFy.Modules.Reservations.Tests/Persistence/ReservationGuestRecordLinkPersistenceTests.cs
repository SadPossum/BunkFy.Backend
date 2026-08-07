namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Domain.GuestRecords;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationGuestRecordLinkPersistenceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Repository_reads_are_tenant_scoped()
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase($"reservation-guest-record-links-{Guid.NewGuid():N}")
                .Options;
        ReservationGuestRecordLinkProcess tenantA = Create("tenant-a");
        ReservationGuestRecordLinkProcess tenantB = Create("tenant-b");

        await using (ReservationsDbContext context = new(
            options,
            new TestScopeContext("tenant-a")))
        {
            context.GuestRecordLinkProcesses.Add(tenantA);
            await context.SaveChangesAsync();
        }

        await using (ReservationsDbContext context = new(
            options,
            new TestScopeContext("tenant-b")))
        {
            context.GuestRecordLinkProcesses.Add(tenantB);
            await context.SaveChangesAsync();
        }

        await using ReservationsDbContext tenantAContext = new(
            options,
            new TestScopeContext("tenant-a"));
        ReservationGuestRecordLinkProcessRepository repository = new(
            tenantAContext);

        Assert.NotNull(await repository.GetByOperationAsync(
            tenantA.Id,
            CancellationToken.None));
        Assert.Null(await repository.GetByOperationAsync(
            tenantB.Id,
            CancellationToken.None));
        Assert.Single(await tenantAContext.GuestRecordLinkProcesses
            .AsNoTracking()
            .ToArrayAsync());
    }

    [Fact]
    public void Model_has_tenant_first_uniqueness_and_no_guest_profile_fields()
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase($"reservation-guest-record-link-model-{Guid.NewGuid():N}")
                .Options;
        using ReservationsDbContext context = new(
            options,
            new TestScopeContext("tenant-a"));
        IEntityType entity = context.Model.FindEntityType(
            typeof(ReservationGuestRecordLinkProcess))!;

        Assert.Equal(
            "reservation_guest_record_link_processes",
            entity.GetTableName());
        Assert.True(entity.FindProperty(
            nameof(ReservationGuestRecordLinkProcess.Revision))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ReservationGuestRecordLinkProcess.ScopeId),
                nameof(ReservationGuestRecordLinkProcess.ReservationId)
            ]));
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ReservationGuestRecordLinkProcess.ScopeId),
                nameof(ReservationGuestRecordLinkProcess.CreationConfirmationId)
            ]));

        string[] forbidden =
        [
            "DisplayName",
            "LegalName",
            "Email",
            "Phone",
            "DateOfBirth",
            "NationalityCountryCode",
            "PreferredLanguageTag",
            "Notes"
        ];
        Assert.DoesNotContain(
            entity.GetProperties(),
            property => forbidden.Contains(property.Name, StringComparer.Ordinal));
    }

    private static ReservationGuestRecordLinkProcess Create(string tenantId) =>
        ReservationGuestRecordLinkProcess.Prepare(
            Guid.NewGuid(),
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            expectedReservationVersion: 1,
            "user:staff",
            Now).Value;

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => scopeId;
    }
}
