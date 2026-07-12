namespace BunkFy.Modules.Guests.Tests;

using Gma.Framework.Scoping;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestsModelTests
{
    [Fact]
    public void Model_has_scoped_non_unique_contact_indexes_and_lifecycle_constraints()
    {
        using GuestsDbContext dbContext = CreateDbContext();
        IEntityType profile = dbContext.Model.FindEntityType(typeof(GuestProfile))!;
        IEntityType designProfile = dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(GuestProfile))!;

        Assert.True(profile.FindProperty(nameof(GuestProfile.Version))!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAdd, profile.FindProperty(nameof(GuestProfile.ProjectionOrdinal))!.ValueGenerated);
        Assert.Equal(GuestProfile.ActorIdMaxLength, profile.FindProperty(nameof(GuestProfile.CreatedBy))!.GetMaxLength());
        Assert.Contains(profile.GetIndexes(), index => !index.IsUnique && index.Properties.Select(item => item.Name)
            .SequenceEqual([nameof(GuestProfile.ScopeId), nameof(GuestProfile.EmailSearch)]));
        Assert.Contains(profile.GetIndexes(), index => !index.IsUnique && index.Properties.Select(item => item.Name)
            .SequenceEqual([nameof(GuestProfile.ScopeId), nameof(GuestProfile.PhoneSearch)]));
        Assert.Contains(designProfile.GetCheckConstraints(), constraint => constraint.Name == "CK_guest_profiles_lifecycle");
        Assert.Contains(designProfile.GetCheckConstraints(), constraint => constraint.Name == "CK_guest_profiles_created_by");
        IEntityType stay = dbContext.Model.FindEntityType(typeof(GuestStayHistoryEntry))!;
        Assert.True(stay.FindProperty(nameof(GuestStayHistoryEntry.ReservationVersion))!.IsConcurrencyToken);
        Assert.Contains(stay.GetIndexes(), index => index.Properties.Select(item => item.Name)
            .SequenceEqual([
                nameof(GuestStayHistoryEntry.ScopeId),
                nameof(GuestStayHistoryEntry.PropertyId),
                nameof(GuestStayHistoryEntry.IsCurrentParticipant),
                nameof(GuestStayHistoryEntry.GuestId),
                nameof(GuestStayHistoryEntry.Arrival)
            ]));
    }

    private static GuestsDbContext CreateDbContext()
    {
        DbContextOptions<GuestsDbContext> options = new DbContextOptionsBuilder<GuestsDbContext>()
            .UseInMemoryDatabase($"guests-model-{Guid.NewGuid():N}")
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
