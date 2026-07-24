namespace BunkFy.Modules.DataRights.Tests.Persistence;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsModelTests
{
    [Fact]
    public void Model_enforces_concurrency_scope_and_lifecycle_constraints()
    {
        using DataRightsDbContext dbContext = CreateDbContext(
            $"data-rights-model-{Guid.NewGuid():N}",
            new InMemoryDatabaseRoot(),
            "tenant-a");
        IEntityType entity = dbContext.Model.FindEntityType(typeof(DataRightsCase))!;
        IEntityType designEntity = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(DataRightsCase))!;

        Assert.True(entity.FindProperty(nameof(DataRightsCase.Version))!.IsConcurrencyToken);
        Assert.Equal(
            DataRightsCase.ActorIdMaxLength,
            entity.FindProperty(nameof(DataRightsCase.CreatedBy))!.GetMaxLength());
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Select(item => item.Name)
            .SequenceEqual([
                nameof(DataRightsCase.ScopeId),
                nameof(DataRightsCase.PropertyId),
                nameof(DataRightsCase.Status),
                nameof(DataRightsCase.CreatedAtUtc),
                nameof(DataRightsCase.Id)
            ]));
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_property_scope");
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_operations");
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_restriction_directive");
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_requester_scope");
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_decision_details");
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_decision_state");
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_approval_policy_evidence");
        Assert.Equal(
            DataRightsCase.ActorIdMaxLength,
            entity.FindProperty(nameof(DataRightsCase.DecidedBy))!.GetMaxLength());
        IEntityType selectedSubject =
            dbContext.Model.FindEntityType(typeof(DataRightsSubjectCoordinate))!;
        IEntityType designSelectedSubject = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(DataRightsSubjectCoordinate))!;
        Assert.Equal(
            DataRightsSubjectCoordinate.OwnerKeyMaxLength,
            selectedSubject.FindProperty(nameof(DataRightsSubjectCoordinate.OwnerKey))!.GetMaxLength());
        Assert.Equal(
            DataRightsCase.ActorIdMaxLength,
            selectedSubject.FindProperty(nameof(DataRightsSubjectCoordinate.SelectedBy))!.GetMaxLength());
        Assert.Equal(
            ["CaseId", "OwnerKey", "RecordType", "RecordId"],
            selectedSubject.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(
            designSelectedSubject.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_selected_subjects_record_version");
        Assert.Contains(
            designSelectedSubject.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_selected_subjects_selected_by");
        IEntityType workItem =
            dbContext.Model.FindEntityType(typeof(DataRightsExecutionWorkItem))!;
        IEntityType designWorkItem = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(DataRightsExecutionWorkItem))!;
        Assert.True(
            workItem.FindProperty(nameof(DataRightsExecutionWorkItem.Version))!
                .IsConcurrencyToken);
        Assert.Equal(
            DataRightsCase.ActorIdMaxLength,
            workItem.FindProperty(nameof(DataRightsExecutionWorkItem.CreatedBy))!.GetMaxLength());
        Assert.Contains(workItem.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(DataRightsExecutionWorkItem.ScopeId),
                nameof(DataRightsExecutionWorkItem.IdempotencyKey)
            ]));
        Assert.Contains(workItem.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(DataRightsExecutionWorkItem.ScopeId),
                nameof(DataRightsExecutionWorkItem.CaseId),
                nameof(DataRightsExecutionWorkItem.ApprovalRevision),
                nameof(DataRightsExecutionWorkItem.Operation),
                nameof(DataRightsExecutionWorkItem.OwnerKey),
                nameof(DataRightsExecutionWorkItem.RecordType),
                nameof(DataRightsExecutionWorkItem.RecordId)
            ]));
        Assert.Contains(
            designWorkItem.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_execution_work_items_revisions");
        Assert.Contains(
            designWorkItem.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_execution_work_items_policy");
        Assert.Contains(
            designWorkItem.GetForeignKeys(),
            foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.Properties.Select(property => property.Name).SequenceEqual([
                    nameof(DataRightsExecutionWorkItem.ScopeId),
                    nameof(DataRightsExecutionWorkItem.CaseId)
                ]));
    }

    [Fact]
    public async Task Scope_filter_hides_cases_from_another_tenant()
    {
        string databaseName = $"data-rights-scope-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateCase("tenant-a", propertyId);

        await using (DataRightsDbContext tenantA = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            tenantA.Cases.Add(dataRightsCase);
            await tenantA.SaveChangesAsync();
        }

        await using DataRightsDbContext tenantB = CreateDbContext(
            databaseName,
            root,
            "tenant-b");
        Assert.Empty(await tenantB.Cases.ToArrayAsync());
    }

    [Fact]
    public async Task Selected_subject_coordinates_round_trip_with_the_case()
    {
        string databaseName = $"data-rights-subjects-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        DataRightsCase dataRightsCase = CreateCase("tenant-a", Guid.NewGuid());
        DateTimeOffset discoveryAt = dataRightsCase.CreatedAtUtc.AddMinutes(1);
        Assert.True(dataRightsCase.BeginDiscovery(1, "user:operator", discoveryAt).IsSuccess);
        Guid guestId = Guid.NewGuid();
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            guestId,
            7,
            2,
            "user:operator",
            discoveryAt.AddMinutes(1)).IsSuccess);

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            writer.Cases.Add(dataRightsCase);
            await writer.SaveChangesAsync();
        }

        await using DataRightsDbContext reader = CreateDbContext(
            databaseName,
            root,
            "tenant-a");
        DataRightsCase restored = await reader.Cases.SingleAsync();
        DataRightsSubjectCoordinate coordinate = Assert.Single(restored.SelectedSubjects);
        Assert.Equal("guests", coordinate.OwnerKey);
        Assert.Equal("guest-profile", coordinate.RecordType);
        Assert.Equal(guestId, coordinate.RecordId);
        Assert.Equal(7, coordinate.RecordVersion);
        Assert.Equal("user:operator", coordinate.SelectedBy);
    }

    [Fact]
    public async Task Approved_decision_revision_and_attribution_round_trip_with_the_case()
    {
        string databaseName = $"data-rights-decision-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        DataRightsCase dataRightsCase = CreateCase("tenant-a", Guid.NewGuid());
        DateTimeOffset discoveryAt = dataRightsCase.CreatedAtUtc.AddMinutes(1);
        Assert.True(dataRightsCase.BeginDiscovery(1, "user:operator", discoveryAt).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            1,
            2,
            "user:operator",
            discoveryAt.AddMinutes(1)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            3,
            "user:operator",
            discoveryAt.AddMinutes(2)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:decision-maker",
            discoveryAt.AddMinutes(3)).IsSuccess);
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            5,
            "user:decision-maker",
            discoveryAt.AddMinutes(4)).IsSuccess);

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            writer.Cases.Add(dataRightsCase);
            await writer.SaveChangesAsync();
        }

        await using DataRightsDbContext reader = CreateDbContext(
            databaseName,
            root,
            "tenant-a");
        DataRightsCase restored = await reader.Cases.SingleAsync();
        Assert.Equal(DataRightsCaseDecision.Approved, restored.Decision);
        Assert.Equal(DataRightsCaseDecisionReason.RequestValidated, restored.DecisionReason);
        Assert.Equal(6, restored.DecisionRevision);
        Assert.Equal("user:decision-maker", restored.DecidedBy);
        Assert.Equal(discoveryAt.AddMinutes(4), restored.DecidedAtUtc);
    }

    [Fact]
    public async Task Restriction_directive_round_trips_with_the_case()
    {
        string databaseName = $"data-rights-restriction-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            Guid.NewGuid(),
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.Restriction,
            DataRightsRequesterRelation.ControllerInitiated,
            DataRightsRestrictionAction.Release).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:operator",
            new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero)).Value;

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            writer.Cases.Add(dataRightsCase);
            await writer.SaveChangesAsync();
        }

        await using DataRightsDbContext reader = CreateDbContext(
            databaseName,
            root,
            "tenant-a");
        DataRightsCase restored = await reader.Cases.SingleAsync();
        Assert.Equal(DataRightsRestrictionAction.Release, restored.RestrictionAction);
    }

    [Fact]
    public async Task Anonymisation_approval_evidence_round_trips_with_the_case()
    {
        string databaseName = $"data-rights-evidence-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        Guid propertyId = Guid.NewGuid();
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.Anonymisation,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        DateTimeOffset now = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:operator",
            now).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator",
            now.AddMinutes(1)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            4,
            2,
            "user:operator",
            now.AddMinutes(2)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            3,
            "user:operator",
            now.AddMinutes(3)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:decision-maker",
            now.AddMinutes(4)).IsSuccess);
        DataRightsApprovalPolicyEvidence evidence =
            DataRightsApprovalPolicyEvidence.Create(
                propertyId,
                12,
                "GB",
                "approved-policy",
                3,
                "guest-retention",
                2,
                new string('c', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                now.AddMinutes(5)).Value;
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            5,
            "user:decision-maker",
            now.AddMinutes(5),
            evidence).IsSuccess);

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            writer.Cases.Add(dataRightsCase);
            await writer.SaveChangesAsync();
        }

        await using DataRightsDbContext reader = CreateDbContext(
            databaseName,
            root,
            "tenant-a");
        DataRightsCase restored = await reader.Cases.SingleAsync();
        DataRightsApprovalPolicyEvidence restoredEvidence =
            Assert.IsType<DataRightsApprovalPolicyEvidence>(
                restored.ApprovalPolicyEvidence);
        Assert.Equal("approved-policy", restoredEvidence.PolicyId);
        Assert.Equal(12, restoredEvidence.PropertyVersion);
        Assert.Equal(new string('c', 64), restoredEvidence.ContentSha256);
        Assert.True(restoredEvidence.RequiresDistinctExecutor);
    }

    [Fact]
    public async Task Prepared_anonymisation_execution_round_trips_and_is_tenant_isolated()
    {
        string databaseName = $"data-rights-execution-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        (DataRightsCase dataRightsCase, DataRightsExecutionWorkItem workItem) =
            CreatePreparedExecution();

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            writer.Cases.Add(dataRightsCase);
            writer.ExecutionWorkItems.Add(workItem);
            await writer.SaveChangesAsync();
        }

        await using (DataRightsDbContext reader = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            DataRightsCase restoredCase = await reader.Cases.SingleAsync();
            DataRightsExecutionWorkItem restoredWorkItem =
                await reader.ExecutionWorkItems.SingleAsync();
            Assert.Equal(DataRightsCaseState.Executing, restoredCase.Status);
            Assert.Equal(7, restoredCase.ExecutionRevision);
            Assert.Equal("user:executor", restoredCase.ExecutionStartedBy);
            Assert.Equal(DataRightsExecutionWorkItemState.Prepared, restoredWorkItem.State);
            Assert.Equal(restoredCase.Id, restoredWorkItem.CaseId);
            Assert.Equal(restoredCase.ExecutionRevision, restoredWorkItem.ExecutionRevision);
            Assert.Equal("approved-policy", restoredWorkItem.PolicyId);
        }

        await using DataRightsDbContext tenantB = CreateDbContext(
            databaseName,
            root,
            "tenant-b");
        Assert.Empty(await tenantB.ExecutionWorkItems.ToArrayAsync());
    }

    private static DataRightsCase CreateCase(string tenantId, Guid propertyId)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        return DataRightsCase.Create(
            Guid.NewGuid(),
            tenantId,
            request,
            "user:operator",
            new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero)).Value;
    }

    private static (DataRightsCase Case, DataRightsExecutionWorkItem WorkItem)
        CreatePreparedExecution()
    {
        Guid propertyId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.Anonymisation,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:operator",
            now).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator",
            now.AddMinutes(1)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            4,
            2,
            "user:operator",
            now.AddMinutes(2)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            3,
            "user:operator",
            now.AddMinutes(3)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:decision-maker",
            now.AddMinutes(4)).IsSuccess);
        DataRightsApprovalPolicyEvidence evidence =
            DataRightsApprovalPolicyEvidence.Create(
                propertyId,
                12,
                "GB",
                "approved-policy",
                3,
                "guest-retention",
                2,
                new string('c', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                now.AddMinutes(5)).Value;
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            5,
            "user:decision-maker",
            now.AddMinutes(5),
            evidence).IsSuccess);
        Assert.True(dataRightsCase.BeginAnonymisationExecution(
            6,
            "user:executor",
            now.AddMinutes(6)).IsSuccess);
        DataRightsExecutionWorkItem workItem = DataRightsExecutionWorkItem.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            dataRightsCase.Id,
            propertyId,
            6,
            7,
            DataRightsCaseOperation.Anonymisation,
            Assert.Single(dataRightsCase.SelectedSubjects),
            evidence,
            "user:executor",
            now.AddMinutes(6)).Value;
        return (dataRightsCase, workItem);
    }

    private static DataRightsDbContext CreateDbContext(
        string databaseName,
        InMemoryDatabaseRoot root,
        string tenantId)
    {
        DbContextOptions<DataRightsDbContext> options =
            new DbContextOptionsBuilder<DataRightsDbContext>()
                .UseInMemoryDatabase(databaseName, root)
                .Options;
        return new(options, new TestScopeContext(tenantId));
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
