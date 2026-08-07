namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Guests.Persistence.Models;
using BunkFy.Modules.Guests.Persistence.Repositories;
using BunkFy.Modules.Guests.Persistence.TenantTermination;
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
    public void Tenant_revision_is_scope_keyed_and_concurrency_guarded()
    {
        using GuestsDbContext dbContext = CreateDbContext();

        IEntityType revisionEntity = dbContext.Model.FindEntityType(
            typeof(GuestsTenantRevision))!;
        IEntityType designRevisionEntity = dbContext
            .GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(GuestsTenantRevision))!;

        Assert.Equal(
            [nameof(GuestsTenantRevision.ScopeId)],
            revisionEntity.FindPrimaryKey()!.Properties.Select(
                property => property.Name));
        Assert.True(
            revisionEntity.FindProperty(
                nameof(GuestsTenantRevision.Revision))!
                .IsConcurrencyToken);
        Assert.NotEmpty(revisionEntity.GetDeclaredQueryFilters());
        Assert.Contains(
            designRevisionEntity.GetCheckConstraints(),
            constraint => string.Equals(
                constraint.Name,
                "CK_guests_tenant_revision_positive",
                StringComparison.Ordinal));
        Assert.Contains(
            designRevisionEntity.GetCheckConstraints(),
            constraint => string.Equals(
                constraint.Name,
                "CK_guests_tenant_revision_lifecycle",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Tenant_destruction_progress_and_receipt_are_scope_unique_and_constrained()
    {
        using GuestsDbContext dbContext = CreateDbContext();
        IModel designModel = dbContext.GetService<IDesignTimeModel>().Model;
        IEntityType operation = designModel.FindEntityType(
            typeof(GuestsTenantDestroyOperation))!;
        IEntityType receipt = designModel.FindEntityType(
            typeof(GuestsTenantDestroyReceipt))!;

        Assert.Equal(
            [nameof(GuestsTenantDestroyOperation.OperationId)],
            operation.FindPrimaryKey()!.Properties.Select(
                property => property.Name));
        Assert.True(operation.FindProperty(
            nameof(GuestsTenantDestroyOperation.ConcurrencyVersion))!
            .IsConcurrencyToken);
        Assert.Contains(
            operation.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(GuestsTenantDestroyOperation.ScopeId)
                    ]));
        Assert.Contains(
            operation.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_guests_tenant_destroy_operation_batch");
        Assert.Contains(
            operation.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_guests_tenant_destroy_operation_progress");

        Assert.Equal(
            [nameof(GuestsTenantDestroyReceipt.OperationId)],
            receipt.FindPrimaryKey()!.Properties.Select(
                property => property.Name));
        Assert.Contains(
            receipt.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(GuestsTenantDestroyReceipt.ScopeId)
                    ]));
        Assert.Contains(
            receipt.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_guests_tenant_destroy_receipt_progress");
    }

    [Fact]
    public void Model_has_scoped_non_unique_contact_indexes_and_lifecycle_constraints()
    {
        using GuestsDbContext dbContext = CreateDbContext();
        IEntityType profile = dbContext.Model.FindEntityType(typeof(GuestProfile))!;
        IEntityType designProfile = dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(GuestProfile))!;

        Assert.True(profile.FindProperty(nameof(GuestProfile.Version))!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAdd, profile.FindProperty(nameof(GuestProfile.ProjectionOrdinal))!.ValueGenerated);
        Assert.Equal(GuestProfile.ActorIdMaxLength, profile.FindProperty(nameof(GuestProfile.CreatedBy))!.GetMaxLength());
        Assert.NotNull(profile.FindProperty(nameof(GuestProfile.AnonymisedAtUtc)));
        Assert.Contains(profile.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestProfile.ScopeId),
                nameof(GuestProfile.CreationConfirmationId)
            ]));
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
        IEntityType designStay = dbContext.GetService<IDesignTimeModel>()
            .Model.FindEntityType(typeof(GuestStayHistoryEntry))!;
        Assert.Contains(
            designStay.GetCheckConstraints(),
            constraint => constraint.Name == "CK_guests_stay_history_contract_version");
        IEntityType dataHold = dbContext.Model.FindEntityType(typeof(GuestDataHold))!;
        Assert.True(dataHold.FindProperty(nameof(GuestDataHold.Version))!.IsConcurrencyToken);
        Assert.Contains(dataHold.GetIndexes(), index =>
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestDataHold.ScopeId),
                nameof(GuestDataHold.GuestId),
                nameof(GuestDataHold.State),
                nameof(GuestDataHold.PropertyId),
                nameof(GuestDataHold.Id)
            ]));
        Assert.Contains(dataHold.GetIndexes(), index =>
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestDataHold.ScopeId),
                nameof(GuestDataHold.PropertyId),
                nameof(GuestDataHold.GuestId),
                nameof(GuestDataHold.PlacedAtUtc),
                nameof(GuestDataHold.Id)
            ]));
        Assert.Contains(dataHold.GetIndexes(), index =>
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestDataHold.ScopeId),
                nameof(GuestDataHold.PropertyId),
                nameof(GuestDataHold.GuestId),
                nameof(GuestDataHold.State),
                nameof(GuestDataHold.PlacedAtUtc),
                nameof(GuestDataHold.Id)
            ]));
        IEntityType designDataHold = dbContext.GetService<IDesignTimeModel>()
            .Model.FindEntityType(typeof(GuestDataHold))!;
        Assert.Contains(
            designDataHold.GetCheckConstraints(),
            constraint => constraint.Name == "CK_guest_data_holds_lifecycle");
        IEntityType dataHoldReceipt =
            dbContext.Model.FindEntityType(typeof(GuestDataHoldReceipt))!;
        Assert.Contains(dataHoldReceipt.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestDataHoldReceipt.ScopeId),
                nameof(GuestDataHoldReceipt.IdempotencyKey)
            ]));
        Assert.Contains(dataHoldReceipt.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestDataHoldReceipt.ScopeId),
                nameof(GuestDataHoldReceipt.HoldId),
                nameof(GuestDataHoldReceipt.Action)
            ]));
        IEntityType correctionReceipt =
            dbContext.Model.FindEntityType(typeof(GuestDataRightsCorrectionReceipt))!;
        Assert.Contains(correctionReceipt.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestDataRightsCorrectionReceipt.ScopeId),
                nameof(GuestDataRightsCorrectionReceipt.IdempotencyKey)
            ]));
        IEntityType designReceipt = dbContext.GetService<IDesignTimeModel>()
            .Model.FindEntityType(typeof(GuestDataRightsCorrectionReceipt))!;
        Assert.Contains(
            designReceipt.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_guest_data_rights_correction_receipts_versions");
        IEntityType restrictionProjection =
            dbContext.Model.FindEntityType(typeof(GuestProcessingRestrictionProjection))!;
        Assert.True(
            restrictionProjection.FindProperty(
                nameof(GuestProcessingRestrictionProjection.Revision))!.IsConcurrencyToken);
        Assert.Contains(restrictionProjection.GetIndexes(), index =>
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestProcessingRestrictionProjection.ScopeId),
                nameof(GuestProcessingRestrictionProjection.PropertyId),
                nameof(GuestProcessingRestrictionProjection.IsRestricted),
                nameof(GuestProcessingRestrictionProjection.GuestId)
            ]));
        IEntityType designRestrictionProjection = dbContext.GetService<IDesignTimeModel>()
            .Model.FindEntityType(typeof(GuestProcessingRestrictionProjection))!;
        Assert.Contains(
            designRestrictionProjection.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_guest_processing_restrictions_effective_state");
        IEntityType restriction =
            dbContext.Model.FindEntityType(typeof(GuestProcessingRestriction))!;
        Assert.True(
            restriction.FindProperty(nameof(GuestProcessingRestriction.Version))!.IsConcurrencyToken);
        Assert.Contains(restriction.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestProcessingRestriction.ScopeId),
                nameof(GuestProcessingRestriction.PropertyId),
                nameof(GuestProcessingRestriction.GuestId),
                nameof(GuestProcessingRestriction.ApplyCaseId),
                nameof(GuestProcessingRestriction.ApplyApprovalRevision)
            ]));
        IEntityType designRestriction = dbContext.GetService<IDesignTimeModel>()
            .Model.FindEntityType(typeof(GuestProcessingRestriction))!;
        Assert.Contains(
            designRestriction.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_guest_processing_restrictions_lifecycle");
        IEntityType restrictionReceipt =
            dbContext.Model.FindEntityType(typeof(GuestProcessingRestrictionReceipt))!;
        Assert.Contains(restrictionReceipt.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestProcessingRestrictionReceipt.ScopeId),
                nameof(GuestProcessingRestrictionReceipt.IdempotencyKey)
            ]));
        IEntityType anonymisationReceipt =
            dbContext.Model.FindEntityType(typeof(GuestAnonymisationReceipt))!;
        Assert.Contains(anonymisationReceipt.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestAnonymisationReceipt.ScopeId),
                nameof(GuestAnonymisationReceipt.IdempotencyKey)
            ]));
        Assert.Contains(anonymisationReceipt.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestAnonymisationReceipt.ScopeId),
                nameof(GuestAnonymisationReceipt.CaseId),
                nameof(GuestAnonymisationReceipt.ApprovalRevision),
                nameof(GuestAnonymisationReceipt.OperationRevision),
                nameof(GuestAnonymisationReceipt.GuestId)
            ]));
        IEntityType anonymisationTombstone =
            dbContext.Model.FindEntityType(typeof(GuestAnonymisationTombstone))!;
        Assert.True(anonymisationTombstone.FindProperty(
            nameof(GuestAnonymisationTombstone.Revision))!.IsConcurrencyToken);
        IEntityType operationLock =
            dbContext.Model.FindEntityType(typeof(GuestOperationLock))!;
        Assert.True(operationLock.FindProperty(
            nameof(GuestOperationLock.Revision))!.IsConcurrencyToken);
        Assert.Contains(operationLock.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(GuestOperationLock.ScopeId),
                nameof(GuestOperationLock.ResourceKind),
                nameof(GuestOperationLock.ResourceId)
            ]));
        IModel designModel = dbContext.GetService<IDesignTimeModel>().Model;
        Assert.Contains(
            designModel.FindEntityType(typeof(GuestAnonymisationReceipt))!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_guest_anonymisation_receipts_versions");
        Assert.Contains(
            designModel.FindEntityType(typeof(GuestAnonymisationTombstone))!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_guest_anonymisation_tombstones_contract");
    }

    [Fact]
    public async Task Property_projection_orders_topology_and_policy_streams_independently()
    {
        await using GuestsDbContext dbContext = CreateDbContext();
        GuestPropertyProjectionRepository repository =
            new(dbContext, new NoopGuestOperationLock());
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

    [Fact]
    public async Task Data_holds_and_receipts_are_tenant_isolated_and_round_trip()
    {
        string databaseName = $"guests-holds-{Guid.NewGuid():N}";
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
        DateTimeOffset placedAtUtc =
            new(2026, 7, 25, 1, 0, 0, TimeSpan.Zero);
        GuestDataHold hold = GuestDataHold.Place(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "legal-obligation",
            "user:privacy",
            placedAtUtc).Value;
        GuestDataHoldReceipt receipt = GuestDataHoldReceipt.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            hold,
            GuestDataHoldAction.Place,
            selectedGuestVersion: 3,
            "user:privacy",
            placedAtUtc).Value;

        await using (GuestsDbContext tenantA =
            new(options, new TestScopeContext("tenant-a")))
        {
            tenantA.DataHolds.Add(hold);
            tenantA.DataHoldReceipts.Add(receipt);
            await tenantA.SaveChangesAsync();
        }

        await using (GuestsDbContext tenantB =
            new(options, new TestScopeContext("tenant-b")))
        {
            Assert.Empty(await tenantB.DataHolds.ToArrayAsync());
            Assert.Empty(await tenantB.DataHoldReceipts.ToArrayAsync());
        }

        await using GuestsDbContext reloaded =
            new(options, new TestScopeContext("tenant-a"));
        GuestDataHold persisted = Assert.Single(await reloaded.DataHolds.ToArrayAsync());
        GuestDataHoldReceipt persistedReceipt =
            Assert.Single(await reloaded.DataHoldReceipts.ToArrayAsync());
        Assert.Equal(hold.Id, persisted.Id);
        Assert.Equal(receipt.IdempotencyKey, persistedReceipt.IdempotencyKey);
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

    private sealed class TestScopeContext(string scopeId = "tenant-a") : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => scopeId;
    }
}
