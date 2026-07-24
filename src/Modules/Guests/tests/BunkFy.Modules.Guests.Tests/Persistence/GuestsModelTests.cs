namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Guests.Persistence.Repositories;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Scoping;
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
        Assert.Contains(stay.GetIndexes(), index => index.Properties.Select(item => item.Name)
            .SequenceEqual([
                nameof(GuestStayHistoryEntry.ScopeId),
                nameof(GuestStayHistoryEntry.PropertyId),
                nameof(GuestStayHistoryEntry.GuestId)
            ]));
    }

    [Fact]
    public async Task Property_projection_orders_topology_and_policy_streams_independently()
    {
        await using GuestsDbContext dbContext = CreateDbContext();
        GuestPropertyProjectionRepository repository = new(dbContext);
        Guid propertyId = Guid.NewGuid();
        PropertyGovernancePolicyBinding binding = CreateGovernanceBinding();

        await repository.ApplyPolicyAsync(
            new("tenant-a", propertyId, PropertyProcessingStatus.Enabled, binding, 4),
            CancellationToken.None);
        await repository.ApplyTopologyAsync(
            new("tenant-a", propertyId, "Property", PropertyStatus.Active, 2),
            CancellationToken.None);
        await repository.ApplyPolicyAsync(
            new("tenant-a", propertyId, PropertyProcessingStatus.Suspended, binding, 3),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        GuestPropertyPolicySnapshot snapshot = Assert.IsType<GuestPropertyPolicySnapshot>(
            await repository.GetPolicyAsync(propertyId, CancellationToken.None));
        Assert.True(snapshot.IsKnown);
        Assert.True(snapshot.IsActive);
        Assert.Equal(PropertyProcessingStatus.Enabled, snapshot.ProcessingStatus);
        Assert.Equal(binding.ContentSha256, snapshot.GovernancePolicy!.ContentSha256);
        GuestPropertyProjection projection = await dbContext.PropertyProjections.SingleAsync();
        Assert.Equal(2, projection.TopologySourceVersion);
        Assert.Equal(4, projection.PolicySourceVersion);
    }

    private static PropertyGovernancePolicyBinding CreateGovernanceBinding()
    {
        DateTimeOffset now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        return new(
            "GB",
            "gb-hostel",
            1,
            "eu-west-2",
            "uk-no-transfer",
            "guest-operational",
            1,
            new string('a', PropertiesContractLimits.ContentSha256Length),
            now.AddDays(-1),
            now.AddDays(30),
            now,
            []);
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
