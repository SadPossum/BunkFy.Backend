namespace BunkFy.Modules.DataRights.Tests.Persistence;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.DataRights.Persistence.Repositories;
using Gma.Framework.Pagination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using DataRightsCaseListResponse =
    BunkFy.Modules.DataRights.Contracts.DataRightsCaseListResponse;

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
        Assert.Equal(
            "(\"ExecutionRevision\" IS NULL AND \"ExecutionStartedBy\" IS NULL AND " +
            "\"ExecutionStartedAtUtc\" IS NULL AND " +
            "(\"Status\" IN (1, 2, 3, 4, 5, 6, 11) OR " +
            "(\"Status\" = 9 AND \"Decision\" = 1 AND \"RequestedOperations\" = 1))) OR " +
            "(\"ExecutionRevision\" IS NOT NULL AND \"ExecutionRevision\" > \"DecisionRevision\" AND " +
            "\"ExecutionRevision\" <= \"Version\" AND \"ExecutionStartedBy\" IS NOT NULL AND " +
            "\"ExecutionStartedAtUtc\" IS NOT NULL AND \"Decision\" = 1 AND " +
            "\"Status\" IN (7, 8, 9, 10, 11))",
            designEntity.GetCheckConstraints().Single(
                constraint => constraint.Name == "CK_data_rights_cases_execution").Sql);
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_approval_policy_evidence");
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint =>
                constraint.Name == "CK_data_rights_cases_restriction_execution_proof");
        Assert.Equal(
            "\"Kind\" IN (1, 2, 3)",
            designEntity.GetCheckConstraints().Single(
                constraint => constraint.Name == "CK_data_rights_cases_kind").Sql);
        Assert.Equal(
            "(\"Kind\" <> 3 AND \"RequestedOperations\" BETWEEN 1 AND 31) OR " +
            "(\"Kind\" = 3 AND \"RequestedOperations\" = 1)",
            designEntity.GetCheckConstraints().Single(
                constraint => constraint.Name == "CK_data_rights_cases_operations").Sql);
        Assert.Equal(
            "(\"Kind\" = 1 AND \"PropertyId\" IS NOT NULL) OR " +
            "(\"Kind\" IN (2, 3) AND \"PropertyId\" IS NULL)",
            designEntity.GetCheckConstraints().Single(
                constraint => constraint.Name == "CK_data_rights_cases_property_scope").Sql);
        Assert.Equal(
            "(\"Kind\" IN (1, 3) AND \"RequesterRelationship\" IN (1, 2, 3)) OR " +
            "(\"Kind\" = 2 AND \"RequesterRelationship\" IN (3, 4))",
            designEntity.GetCheckConstraints().Single(
                constraint => constraint.Name == "CK_data_rights_cases_requester_scope").Sql);
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
        IEntityType correctionExecution =
            dbContext.Model.FindEntityType(typeof(DataRightsCorrectionExecution))!;
        IEntityType designCorrectionExecution = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(DataRightsCorrectionExecution))!;
        Assert.True(
            correctionExecution.FindProperty(nameof(DataRightsCorrectionExecution.Version))!
                .IsConcurrencyToken);
        Assert.Equal(
            DataRightsCorrectionExecution.FieldPolicyKeyMaxLength,
            correctionExecution
                .FindProperty(nameof(DataRightsCorrectionExecution.FieldPolicyKey))!
                .GetMaxLength());
        Assert.Contains(correctionExecution.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(DataRightsCorrectionExecution.ScopeId),
                nameof(DataRightsCorrectionExecution.CaseId)
            ]));
        Assert.Contains(correctionExecution.GetIndexes(), index =>
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(DataRightsCorrectionExecution.ScopeId),
                nameof(DataRightsCorrectionExecution.PropertyId),
                nameof(DataRightsCorrectionExecution.State),
                nameof(DataRightsCorrectionExecution.ExpiresAtUtc)
            ]));
        Assert.Contains(
            designCorrectionExecution.GetCheckConstraints(),
            constraint =>
                constraint.Name == "CK_data_rights_correction_executions_contract");
        Assert.Contains(
            designCorrectionExecution.GetCheckConstraints(),
            constraint =>
                constraint.Name == "CK_data_rights_correction_executions_state");
        Assert.Contains(
            designCorrectionExecution.GetForeignKeys(),
            foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.Properties.Select(property => property.Name).SequenceEqual([
                    nameof(DataRightsCorrectionExecution.ScopeId),
                    nameof(DataRightsCorrectionExecution.CaseId)
                ]));
        IEntityType batch =
            dbContext.Model.FindEntityType(typeof(DataRightsExecutionBatch))!;
        IEntityType designBatch = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(DataRightsExecutionBatch))!;
        Assert.True(
            batch.FindProperty(nameof(DataRightsExecutionBatch.Version))!
                .IsConcurrencyToken);
        Assert.Contains(batch.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(DataRightsExecutionBatch.ScopeId),
                nameof(DataRightsExecutionBatch.IdempotencyKey)
            ]));
        Assert.Contains(batch.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(DataRightsExecutionBatch.ScopeId),
                nameof(DataRightsExecutionBatch.CaseId)
            ]));
        Assert.Contains(
            designBatch.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_execution_batches_subject_count");
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
        Assert.Equal(
            DataRightsExecutionWorkItem.OwnerCodeMaxLength,
            workItem.FindProperty(nameof(DataRightsExecutionWorkItem.OwnerDispositionCode))!
                .GetMaxLength());
        Assert.Equal(
            DataRightsExecutionWorkItem.OwnerCodeMaxLength,
            workItem.FindProperty(nameof(DataRightsExecutionWorkItem.OwnerReasonCode))!
                .GetMaxLength());
        Assert.Equal(
            DataRightsExecutionWorkItem.Sha256Length,
            workItem.FindProperty(nameof(DataRightsExecutionWorkItem.OwnerReceiptSha256))!
                .GetMaxLength());
        Assert.Equal(
            DataRightsExecutionWorkItem.OutcomeCodeMaxLength,
            workItem.FindProperty(nameof(DataRightsExecutionWorkItem.OutcomeCode))!
                .GetMaxLength());
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
        Assert.Contains(workItem.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(DataRightsExecutionWorkItem.ScopeId),
                nameof(DataRightsExecutionWorkItem.TaskRunId)
            ]));
        Assert.Contains(
            designWorkItem.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_execution_work_items_revisions");
        Assert.Contains(
            designWorkItem.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_execution_work_items_policy");
        Assert.Contains(
            designWorkItem.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_execution_work_items_state");
        Assert.Contains(
            designWorkItem.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_execution_work_items_attempts");
        Assert.Contains(
            designWorkItem.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_execution_work_items_owner_contract");
        Assert.Contains(
            designWorkItem.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_execution_work_items_task");
        Assert.Contains(
            designWorkItem.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_execution_work_items_owner_outcome");
        Assert.Contains(
            designWorkItem.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_execution_work_items_timestamps");
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
    public void Export_artifact_model_enforces_scope_identity_and_state_shape()
    {
        using DataRightsDbContext dbContext = CreateDbContext(
            $"data-rights-export-model-{Guid.NewGuid():N}",
            new InMemoryDatabaseRoot(),
            "tenant-a");
        IEntityType artifact =
            dbContext.Model.FindEntityType(typeof(DataRightsExportArtifact))!;
        IEntityType designArtifact = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(DataRightsExportArtifact))!;

        Assert.True(
            artifact.FindProperty(nameof(DataRightsExportArtifact.Version))!
                .IsConcurrencyToken);
        Assert.False(
            artifact.FindProperty(nameof(DataRightsExportArtifact.ExpiresAtUtc))!
                .IsNullable);
        Assert.Equal(
            DataRightsExportArtifact.Sha256Length,
            artifact.FindProperty(nameof(DataRightsExportArtifact.SelectionSha256))!
                .GetMaxLength());
        Assert.Equal(
            DataRightsExportArtifact.ActorIdMaxLength,
            artifact.FindProperty(nameof(DataRightsExportArtifact.RequestedBy))!
                .GetMaxLength());
        Assert.Contains(artifact.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(DataRightsExportArtifact.ScopeId),
                nameof(DataRightsExportArtifact.IdempotencyKey)
            ]));
        Assert.Contains(artifact.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(DataRightsExportArtifact.ScopeId),
                nameof(DataRightsExportArtifact.CaseId)
            ]));
        Assert.Contains(
            designArtifact.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_export_artifacts_generation_shape");
        Assert.Contains(
            designArtifact.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_export_artifacts_storage_shape");
        Assert.Contains(
            designArtifact.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_export_artifacts_lifecycle_shape");
        Assert.Contains(
            designArtifact.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_export_artifacts_timestamps");
        Assert.Contains(
            designArtifact.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_export_artifacts_failure_shape");
        Assert.Contains(
            designArtifact.GetForeignKeys(),
            foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(DataRightsExportArtifact.ScopeId),
                        nameof(DataRightsExportArtifact.CaseId)
                    ]));
    }

    [Fact]
    public async Task Export_audit_model_is_scoped_bounded_and_append_only()
    {
        string databaseName = $"data-rights-export-audit-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        DataRightsExportAuditEntry entry =
            DataRightsExportAuditEntry.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                propertyId: null,
                DataRightsCaseKind.StaffRights,
                DataRightsExportAuditAction.Download,
                "user:privacy",
                "succeeded",
                new DateTimeOffset(
                    2026,
                    7,
                    27,
                    12,
                    0,
                    0,
                    TimeSpan.Zero)).Value;

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            IEntityType entity = writer.Model.FindEntityType(
                typeof(DataRightsExportAuditEntry))!;
            IEntityType designEntity = writer.GetService<IDesignTimeModel>()
                .Model
                .FindEntityType(typeof(DataRightsExportAuditEntry))!;
            Assert.Equal(
                DataRightsExportArtifact.ActorIdMaxLength,
                entity.FindProperty(nameof(DataRightsExportAuditEntry.ActorId))!
                    .GetMaxLength());
            Assert.Equal(
                DataRightsExportAuditEntry.OutcomeCodeMaxLength,
                entity.FindProperty(nameof(DataRightsExportAuditEntry.OutcomeCode))!
                    .GetMaxLength());
            Assert.Contains(
                designEntity.GetCheckConstraints(),
                constraint =>
                    constraint.Name == "CK_data_rights_export_audit_scope");
            Assert.Contains(
                designEntity.GetCheckConstraints(),
                constraint =>
                    constraint.Name == "CK_data_rights_export_audit_action");

            writer.ExportAuditEntries.Add(entry);
            await writer.SaveChangesAsync();
            writer.Entry(entry).State = EntityState.Modified;
            InvalidOperationException failure =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => writer.SaveChangesAsync());
            Assert.Contains("append-only", failure.Message);
        }

        await using DataRightsDbContext tenantB = CreateDbContext(
            databaseName,
            root,
            "tenant-b");
        Assert.Empty(await tenantB.ExportAuditEntries.ToArrayAsync());
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
    public async Task Case_repository_keeps_property_and_tenant_case_kinds_isolated()
    {
        string databaseName = $"data-rights-case-scope-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        Guid propertyA = Guid.NewGuid();
        Guid propertyB = Guid.NewGuid();
        DataRightsCase guestA = CreateCase("tenant-a", propertyA);
        DataRightsCase guestB = CreateCase("tenant-a", propertyB);
        DataRightsCase staff = CreateTenantCase(
            "tenant-a",
            DataRightsCaseKind.StaffRights,
            DataRightsRequesterRelation.DataSubject);
        DataRightsCase termination = CreateTenantCase(
            "tenant-a",
            DataRightsCaseKind.TenantTermination,
            DataRightsRequesterRelation.TenantOwner);

        await using DataRightsDbContext dbContext = CreateDbContext(
            databaseName,
            root,
            "tenant-a");
        dbContext.Cases.AddRange(guestA, guestB, staff, termination);
        await dbContext.SaveChangesAsync();
        DataRightsCaseRepository repository = new(dbContext);

        Assert.Same(
            guestA,
            await repository.GetAsync(
                DataRightsCaseScope.ForProperty(propertyA),
                guestA.Id,
                CancellationToken.None));
        Assert.Null(await repository.GetAsync(
            DataRightsCaseScope.ForProperty(propertyB),
            guestA.Id,
            CancellationToken.None));
        Assert.Same(
            staff,
            await repository.GetAsync(
                DataRightsCaseScope.Staff,
                staff.Id,
                CancellationToken.None));
        Assert.Null(await repository.GetAsync(
            DataRightsCaseScope.Staff,
            termination.Id,
            CancellationToken.None));
        Assert.Same(
            termination,
            await repository.GetAsync(
                DataRightsCaseScope.TenantTermination,
                termination.Id,
                CancellationToken.None));

        DataRightsCaseListResponse staffCases = await repository.ListAsync(
            DataRightsCaseScope.Staff,
            status: null,
            new PageRequest(1, 20),
            CancellationToken.None);
        Assert.Equal(staff.Id, Assert.Single(staffCases.Items).Id);
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
    public async Task Restriction_execution_proof_round_trips_with_the_case()
    {
        string databaseName = $"data-rights-restriction-proof-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        Guid propertyId = Guid.NewGuid();
        DateTimeOffset now =
            new(2026, 7, 27, 16, 30, 0, TimeSpan.Zero);
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.Restriction,
            DataRightsRequesterRelation.ControllerInitiated,
            DataRightsRestrictionAction.Apply).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:privacy",
            now.AddMinutes(-6)).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            dataRightsCase.Version,
            "user:privacy",
            now.AddMinutes(-5)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            recordVersion: 3,
            dataRightsCase.Version,
            "user:privacy",
            now.AddMinutes(-4)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            dataRightsCase.Version,
            "user:privacy",
            now.AddMinutes(-3)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            dataRightsCase.Version,
            "user:decision-maker",
            now.AddMinutes(-2)).IsSuccess);
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            dataRightsCase.Version,
            "user:decision-maker",
            now.AddMinutes(-1)).IsSuccess);
        DataRightsRestrictionExecutionProof proof =
            DataRightsRestrictionExecutionProof.Create(
                Guid.NewGuid(),
                dataRightsCase.DecisionRevision!.Value,
                DataRightsRestrictionAction.Apply,
                dataRightsCase.SelectedSubjects.Single(),
                receiptContractVersion: 1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                resultingOwnerRevision: 2,
                resultingProjectionRevision: 4,
                effectiveRestricted: true,
                new string('a', 64),
                "user:executor",
                now.AddSeconds(-5)).Value;
        Assert.True(dataRightsCase.CompleteRestrictionExecution(
            dataRightsCase.Version,
            proof,
            now).IsSuccess);

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
        Assert.Equal(DataRightsCaseState.Completed, restored.Status);
        Assert.NotNull(restored.RestrictionExecutionProof);
        Assert.Equal(proof.IdempotencyKey, restored.RestrictionExecutionProof.IdempotencyKey);
        Assert.Equal(proof.ReceiptSha256, restored.RestrictionExecutionProof.ReceiptSha256);
        Assert.True(restored.RestrictionExecutionProof.EffectiveRestricted);
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

    [Fact]
    public async Task Processing_ledger_model_is_scoped_unique_and_append_only()
    {
        string databaseName = $"data-rights-ledger-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        (DataRightsCase dataRightsCase, DataRightsExecutionWorkItem workItem) =
            CreatePreparedExecution();
        Guid taskRunId = Guid.NewGuid();
        DateTimeOffset proofAt = workItem.CreatedAtUtc.AddMinutes(2);
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            proofAt.AddMinutes(-1)).IsSuccess);
        Assert.True(workItem.RecordOwnerProof(
            workItem.Version,
            taskRunId,
            taskAttempt: 1,
            receiptContractVersion: 1,
            Guid.NewGuid(),
            resultingRecordVersion: workItem.SelectedRecordVersion + 1,
            "guests.completed",
            "guests.profile-anonymised",
            new string('d', 64),
            proofAt,
            proofAt.AddMinutes(1)).IsSuccess);
        DataRightsProcessingLedgerEntry ledgerEntry =
            DataRightsProcessingLedgerEntry.Create(
                Guid.NewGuid(),
                tenantSequence: 1,
                workItem,
                DataRightsRecordPseudonym.Create(
                    1,
                    new string('e', 64)).Value,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256).Value;

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            IEntityType entity = writer.Model.FindEntityType(
                typeof(DataRightsProcessingLedgerEntry))!;
            IEntityType designEntity = writer.GetService<IDesignTimeModel>()
                .Model
                .FindEntityType(typeof(DataRightsProcessingLedgerEntry))!;
            Assert.Contains(entity.GetIndexes(), index =>
                index.IsUnique &&
                index.Properties.Select(item => item.Name).SequenceEqual([
                    nameof(DataRightsProcessingLedgerEntry.ScopeId),
                    nameof(DataRightsProcessingLedgerEntry.TenantSequence)
                ]));
            Assert.Contains(entity.GetIndexes(), index =>
                index.IsUnique &&
                index.Properties.Select(item => item.Name).SequenceEqual([
                    nameof(DataRightsProcessingLedgerEntry.ScopeId),
                    nameof(DataRightsProcessingLedgerEntry.WorkItemId)
                ]));
            Assert.Contains(entity.GetIndexes(), index =>
                index.IsUnique &&
                index.Properties.Select(item => item.Name).SequenceEqual([
                    nameof(DataRightsProcessingLedgerEntry.ScopeId),
                    nameof(DataRightsProcessingLedgerEntry.OwnerReceiptId)
                ]));
            Assert.Contains(
                designEntity.GetCheckConstraints(),
                constraint =>
                    constraint.Name == "CK_data_rights_processing_ledger_chain");
            Assert.Empty(entity.GetForeignKeys());

            writer.Cases.Add(dataRightsCase);
            writer.ExecutionWorkItems.Add(workItem);
            writer.ProcessingLedgerEntries.Add(ledgerEntry);
            await writer.SaveChangesAsync();

            writer.Entry(ledgerEntry).State = EntityState.Modified;
            InvalidOperationException updateFailure =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => writer.SaveChangesAsync());
            Assert.Contains("append-only", updateFailure.Message);
        }

        await using (DataRightsDbContext cleanup = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            cleanup.ExecutionWorkItems.Remove(
                await cleanup.ExecutionWorkItems.SingleAsync());
            await cleanup.SaveChangesAsync();
            Assert.Empty(await cleanup.ExecutionWorkItems.ToArrayAsync());
            Assert.Single(
                await cleanup.ProcessingLedgerEntries.ToArrayAsync());
        }

        await using DataRightsDbContext tenantB = CreateDbContext(
            databaseName,
            root,
            "tenant-b");
        Assert.Empty(await tenantB.ProcessingLedgerEntries.ToArrayAsync());
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

    private static DataRightsCase CreateTenantCase(
        string tenantId,
        DataRightsCaseKind kind,
        DataRightsRequesterRelation requesterRelationship)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId: null,
            kind,
            DataRightsCaseOperation.AccessExport,
            requesterRelationship).Value;
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
