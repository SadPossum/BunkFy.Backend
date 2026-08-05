namespace Integration.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class DataRightsPersistenceIntegrationTests
{
    private const string InitialMigration = "20260723052104_InitialDataRights";
    private const string BeforeExecutionBatchesMigration =
        "20260726022029_AddProcessingLedgerResultVersion";
    private const string BeforeStaffRightsMigration =
        "20260726214049_AddDataRightsExecutionBatches";
    private const string StaffRightsMigration =
        "20260727021342_SupportTenantScopedStaffRightsCases";
    private const string BeforeScopedAnonymisationExecutionMigration =
        "20260729131757_AllowStaffRestrictionCases";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Latest_migrations_upgrade_existing_cases_and_round_trip_approved_revisions()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_data_rights_decision_tests")
            .Build();
        await postgreSql.StartAsync();

        Guid caseId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid legacyRestrictionCaseId =
            Guid.Parse("10000000-0000-0000-0000-000000000002");
        Guid propertyId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid guestId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        Guid anonymisationCaseId =
            Guid.Parse("10000000-0000-0000-0000-000000000003");
        Guid executionWorkItemId =
            Guid.Parse("40000000-0000-0000-0000-000000000001");
        Guid executionBatchId =
            Guid.Parse("40000000-0000-0000-0000-000000000002");
        Guid executionIdempotencyKey =
            Guid.Parse("50000000-0000-0000-0000-000000000001");
        DateTimeOffset createdAtUtc = new(2026, 7, 23, 6, 0, 0, TimeSpan.Zero);
        await using (DataRightsDbContext initial = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await initial.Database.GetService<IMigrator>().MigrateAsync(InitialMigration);
            await initial.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "data-rights"."cases"
                    ("Id", "PropertyId", "Kind", "RequestedOperations",
                     "RequesterRelationship", "VerificationStatus", "RoutingStatus",
                     "Status", "DueAtUtc", "Version", "CreatedBy", "CreatedAtUtc",
                     "LastChangedBy", "LastChangedAtUtc", "ScopeId")
                VALUES
                    ({caseId}, {propertyId}, {(int)DataRightsCaseKind.GuestRights},
                     {(int)DataRightsCaseOperation.AccessExport},
                     {(int)DataRightsRequesterRelation.ControllerInitiated},
                     {(int)DataRightsVerificationState.NotRequired},
                     {(int)DataRightsRoutingState.NotRequired},
                     {(int)DataRightsCaseState.Draft}, NULL, 1, {"staff:privacy"},
                     {createdAtUtc}, {"staff:privacy"}, {createdAtUtc}, {"tenant-a"}),
                    ({legacyRestrictionCaseId}, {propertyId},
                     {(int)DataRightsCaseKind.GuestRights},
                     {(int)DataRightsCaseOperation.Restriction},
                     {(int)DataRightsRequesterRelation.ControllerInitiated},
                     {(int)DataRightsVerificationState.NotRequired},
                     {(int)DataRightsRoutingState.NotRequired},
                     {(int)DataRightsCaseState.Draft}, NULL, 1, {"staff:privacy"},
                     {createdAtUtc}, {"staff:privacy"}, {createdAtUtc}, {"tenant-a"})
                """);
        }

        DateTimeOffset selectedAtUtc = createdAtUtc.AddMinutes(10);
        await using (DataRightsDbContext upgraded = CreateDbContext(postgreSql.GetConnectionString()))
        {
            await upgraded.Database.MigrateAsync();
            DataRightsCase dataRightsCase = await upgraded.Cases.SingleAsync(item => item.Id == caseId);
            DataRightsCase legacyRestriction = await upgraded.Cases
                .SingleAsync(item => item.Id == legacyRestrictionCaseId);
            Assert.Equal(DataRightsRestrictionAction.None, legacyRestriction.RestrictionAction);

            Assert.True(dataRightsCase.BeginDiscovery(
                dataRightsCase.Version,
                "staff:privacy",
                selectedAtUtc).IsSuccess);
            Assert.True(dataRightsCase.SelectSubject(
                "guests",
                "guest-record",
                guestId,
                7,
                dataRightsCase.Version,
                "staff:privacy",
                selectedAtUtc).IsSuccess);
            Assert.True(dataRightsCase.RequireReview(
                dataRightsCase.Version,
                "staff:privacy",
                selectedAtUtc.AddMinutes(1)).IsSuccess);
            Assert.True(dataRightsCase.BeginDecision(
                dataRightsCase.Version,
                "staff:decision-maker",
                selectedAtUtc.AddMinutes(2)).IsSuccess);
            Assert.True(dataRightsCase.RecordDecision(
                DataRightsCaseDecision.Approved,
                DataRightsCaseDecisionReason.RequestValidated,
                dataRightsCase.Version,
                "staff:decision-maker",
                selectedAtUtc.AddMinutes(3)).IsSuccess);

            PropertyGovernancePolicyBinding governancePolicy = new(
                "GB",
                "integration-hostel-baseline",
                1,
                "integration-region",
                "integration-no-transfer",
                "integration-guest-operational",
                1,
                new string('d', 64),
                new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero),
                selectedAtUtc,
                [new("integration-operator-notice", 1)]);
            DataRightsPropertyProjection property = new(
                "tenant-a",
                propertyId,
                "Integration Hostel",
                "Europe/London",
                PropertyStatus.Active,
                8);
            property.ApplyPolicy(
                PropertyProcessingStatus.Enabled,
                governancePolicy,
                8);
            upgraded.PropertyProjections.Add(property);

            DataRightsCaseRequest anonymisationRequest = DataRightsCaseRequest.Create(
                propertyId,
                DataRightsCaseKind.GuestRights,
                DataRightsCaseOperation.Anonymisation,
                DataRightsRequesterRelation.ControllerInitiated).Value;
            DataRightsCase anonymisationCase = DataRightsCase.Create(
                anonymisationCaseId,
                "tenant-a",
                anonymisationRequest,
                "staff:privacy",
                selectedAtUtc).Value;
            Assert.True(anonymisationCase.BeginDiscovery(
                1,
                "staff:privacy",
                selectedAtUtc.AddMinutes(1)).IsSuccess);
            Assert.True(anonymisationCase.SelectSubject(
                "guests",
                "guest-profile",
                guestId,
                7,
                2,
                "staff:privacy",
                selectedAtUtc.AddMinutes(2)).IsSuccess);
            Assert.True(anonymisationCase.RequireReview(
                3,
                "staff:privacy",
                selectedAtUtc.AddMinutes(3)).IsSuccess);
            Assert.True(anonymisationCase.BeginDecision(
                4,
                "staff:decision-maker",
                selectedAtUtc.AddMinutes(4)).IsSuccess);
            DataRightsApprovalPolicyEvidence evidence =
                DataRightsApprovalPolicyEvidence.Create(
                    propertyId,
                    8,
                    "GB",
                    "integration-hostel-baseline",
                    1,
                    "integration-guest-operational",
                    1,
                    new string('d', 64),
                    "data-rights-anonymisation",
                    "erasure",
                    "authorized-workspace-operator",
                    selectedAtUtc.AddMinutes(5)).Value;
            Assert.True(anonymisationCase.RecordDecision(
                DataRightsCaseDecision.Approved,
                DataRightsCaseDecisionReason.RequestValidated,
                5,
                "staff:decision-maker",
                selectedAtUtc.AddMinutes(5),
                evidence).IsSuccess);
            Assert.True(anonymisationCase.BeginAnonymisationExecution(
                6,
                "staff:executor",
                selectedAtUtc.AddMinutes(6)).IsSuccess);
            DataRightsExecutionBatch executionBatch =
                DataRightsExecutionBatch.Prepare(
                    executionBatchId,
                    "tenant-a",
                    executionIdempotencyKey,
                    anonymisationCase.Id,
                    DataRightsExecutionScope.ForProperty(propertyId),
                    approvalRevision: 6,
                    executionRevision: 7,
                    selectedSubjectCount: 1,
                    "staff:executor",
                    selectedAtUtc.AddMinutes(6)).Value;
            DataRightsExecutionWorkItem executionWorkItem =
                DataRightsExecutionWorkItem.Prepare(
                    executionWorkItemId,
                    "tenant-a",
                    executionBatchId,
                    executionIdempotencyKey,
                    anonymisationCase.Id,
                    DataRightsExecutionScope.ForProperty(propertyId),
                    approvalRevision: 6,
                    executionRevision: 7,
                    DataRightsCaseOperation.Anonymisation,
                    Assert.Single(anonymisationCase.SelectedSubjects),
                    evidence,
                    "staff:executor",
                    selectedAtUtc.AddMinutes(6)).Value;
            upgraded.Cases.Add(anonymisationCase);
            upgraded.ExecutionBatches.Add(executionBatch);
            upgraded.ExecutionWorkItems.Add(executionWorkItem);

            await upgraded.SaveChangesAsync();
        }

        await using (DataRightsDbContext reloaded = CreateDbContext(postgreSql.GetConnectionString()))
        {
            DataRightsCase dataRightsCase = await reloaded.Cases
                .SingleAsync(item => item.Id == caseId);
            var selected = Assert.Single(dataRightsCase.SelectedSubjects);

            Assert.Equal("guests", selected.OwnerKey);
            Assert.Equal("guest-record", selected.RecordType);
            Assert.Equal(guestId, selected.RecordId);
            Assert.Equal(7, selected.RecordVersion);
            Assert.Equal("staff:privacy", selected.SelectedBy);
            Assert.Equal(selectedAtUtc, selected.SelectedAtUtc);
            Assert.Equal(DataRightsCaseState.Approved, dataRightsCase.Status);
            Assert.Equal(DataRightsCaseDecision.Approved, dataRightsCase.Decision);
            Assert.Equal(DataRightsCaseDecisionReason.RequestValidated, dataRightsCase.DecisionReason);
            Assert.Equal(dataRightsCase.Version, dataRightsCase.DecisionRevision);
            Assert.Equal("staff:decision-maker", dataRightsCase.DecidedBy);
            Assert.Equal(selectedAtUtc.AddMinutes(3), dataRightsCase.DecidedAtUtc);
            DataRightsCase anonymisationCase = await reloaded.Cases
                .SingleAsync(item => item.Id == anonymisationCaseId);
            DataRightsApprovalPolicyEvidence evidence =
                Assert.IsType<DataRightsApprovalPolicyEvidence>(
                    anonymisationCase.ApprovalPolicyEvidence);
            Assert.Equal("integration-hostel-baseline", evidence.PolicyId);
            Assert.Equal(8, evidence.PropertyVersion);
            Assert.True(evidence.RequiresDistinctExecutor);
            Assert.Equal(DataRightsCaseState.Executing, anonymisationCase.Status);
            Assert.Equal(7, anonymisationCase.ExecutionRevision);
            Assert.Equal("staff:executor", anonymisationCase.ExecutionStartedBy);
            DataRightsExecutionBatch executionBatch =
                await reloaded.ExecutionBatches.SingleAsync(
                    item => item.Id == executionBatchId);
            Assert.Equal(1, executionBatch.SelectedSubjectCount);
            Assert.Equal(executionIdempotencyKey, executionBatch.IdempotencyKey);
            DataRightsExecutionWorkItem executionWorkItem =
                await reloaded.ExecutionWorkItems.SingleAsync(
                    item => item.Id == executionWorkItemId);
            Assert.Equal(DataRightsExecutionWorkItemState.Prepared, executionWorkItem.State);
            Assert.Equal(executionBatch.Id, executionWorkItem.BatchId);
            Assert.Equal(anonymisationCase.Id, executionWorkItem.CaseId);
            Assert.Equal(6, executionWorkItem.ApprovalRevision);
            Assert.Equal(7, executionWorkItem.ExecutionRevision);
            Assert.True(executionWorkItem.HasIdempotencyKey(executionIdempotencyKey));
            Assert.Equal("integration-hostel-baseline", executionWorkItem.PolicyId);
            DataRightsPropertyProjection property = await reloaded.PropertyProjections
                .Include(item => item.GovernancePolicy)
                .ThenInclude(policy => policy!.Acknowledgements)
                .SingleAsync(item => item.Id == propertyId);
            Assert.Equal(PropertyProcessingStatus.Enabled, property.ProcessingStatus);
            Assert.Equal("integration-hostel-baseline", property.GovernancePolicy?.PolicyId);
            Assert.Single(property.GovernancePolicy?.Acknowledgements ?? []);

            Guid taskRunId = Guid.NewGuid();
            DateTimeOffset ownerCompletedAt = selectedAtUtc.AddMinutes(8);
            Assert.True(executionWorkItem.BeginProcessing(
                taskRunId,
                taskAttempt: 1,
                selectedAtUtc.AddMinutes(7)).IsSuccess);
            Assert.True(executionWorkItem.RecordOwnerProof(
                executionWorkItem.Version,
                taskRunId,
                taskAttempt: 1,
                receiptContractVersion: 1,
                Guid.NewGuid(),
                resultingRecordVersion: executionWorkItem.SelectedRecordVersion + 1,
                "guests.completed",
                "guests.profile-anonymised",
                new string('e', 64),
                ownerCompletedAt,
                ownerCompletedAt.AddMinutes(1)).IsSuccess);
            DataRightsProcessingLedgerEntry ledgerEntry =
                DataRightsProcessingLedgerEntry.Create(
                    Guid.NewGuid(),
                    tenantSequence: 1,
                    executionWorkItem,
                    DataRightsRecordPseudonym.Create(
                        1,
                        new string('f', 64)).Value,
                    DataRightsProcessingLedgerEntry.GenesisEntrySha256).Value;
            reloaded.ProcessingLedgerEntries.Add(ledgerEntry);
            await reloaded.SaveChangesAsync();
            Assert.True(ledgerEntry.HasValidCanonicalDigest());

            reloaded.ExecutionWorkItems.Remove(executionWorkItem);
            await reloaded.SaveChangesAsync();
            Assert.Empty(
                await reloaded.ExecutionWorkItems
                    .Where(item => item.Id == executionWorkItemId)
                    .ToArrayAsync());
            Assert.Single(
                await reloaded.ProcessingLedgerEntries
                    .Where(item => item.Id == ledgerEntry.Id)
                    .ToArrayAsync());

            reloaded.Cases.Remove(dataRightsCase);
            await reloaded.SaveChangesAsync();
            Assert.Empty(await reloaded.Database.SqlQuery<Guid>(
                $"""
                SELECT "RecordId" AS "Value"
                FROM "data-rights"."selected_subjects"
                WHERE "CaseId" = {caseId}
                """)
                .ToListAsync());
        }

        await using (DataRightsDbContext directUpdate = CreateDbContext(
            postgreSql.GetConnectionString()))
        {
            PostgresException failure = await Assert.ThrowsAsync<PostgresException>(
                () => directUpdate.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE "data-rights"."processing_ledger_entries"
                    SET "ReasonCode" = 'tampered'
                    """));
            Assert.Equal("P0001", failure.SqlState);
            Assert.Contains("append-only", failure.MessageText);
        }

        await using (DataRightsDbContext directDelete = CreateDbContext(
            postgreSql.GetConnectionString()))
        {
            PostgresException failure = await Assert.ThrowsAsync<PostgresException>(
                () => directDelete.Database.ExecuteSqlRawAsync(
                    """
                    DELETE FROM "data-rights"."processing_ledger_entries"
                    """));
            Assert.Equal("P0001", failure.SqlState);
            Assert.Contains("append-only", failure.MessageText);
        }
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Execution_batch_migration_promotes_only_ledger_proven_legacy_success()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_data_rights_execution_batch_migration_tests")
                .Build();
        await postgreSql.StartAsync();

        Guid caseId = Guid.Parse("11000000-0000-0000-0000-000000000001");
        Guid propertyId = Guid.Parse("21000000-0000-0000-0000-000000000001");
        Guid recordId = Guid.Parse("31000000-0000-0000-0000-000000000001");
        Guid workItemId = Guid.Parse("41000000-0000-0000-0000-000000000001");
        Guid idempotencyKey = Guid.Parse("51000000-0000-0000-0000-000000000001");
        Guid taskRunId = Guid.Parse("61000000-0000-0000-0000-000000000001");
        Guid ownerReceiptId = Guid.Parse("71000000-0000-0000-0000-000000000001");
        Guid ledgerEntryId = Guid.Parse("81000000-0000-0000-0000-000000000001");
        DateTimeOffset createdAtUtc =
            new(2026, 7, 26, 20, 0, 0, TimeSpan.Zero);
        DateTimeOffset decidedAtUtc = createdAtUtc.AddMinutes(5);
        DateTimeOffset executionStartedAtUtc = createdAtUtc.AddMinutes(6);
        DateTimeOffset ownerCompletedAtUtc = createdAtUtc.AddMinutes(7);
        DateTimeOffset outcomeAtUtc = createdAtUtc.AddMinutes(8);
        string policySha256 = new('a', 64);
        string ownerReceiptSha256 = new('b', 64);
        string recordPseudonymSha256 = new('c', 64);
        string entrySha256 = new('d', 64);

        await using (DataRightsDbContext legacy =
                     CreateDbContext(postgreSql.GetConnectionString()))
        {
            await legacy.Database.GetService<IMigrator>()
                .MigrateAsync(BeforeExecutionBatchesMigration);
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "data-rights"."cases" (
                    "Id", "PropertyId", "Kind", "RequestedOperations",
                    "RequesterRelationship", "VerificationStatus", "RoutingStatus",
                    "Status", "DueAtUtc", "Version", "CreatedBy", "CreatedAtUtc",
                    "LastChangedBy", "LastChangedAtUtc", "ScopeId",
                    "Decision", "DecisionReason", "DecisionRevision", "DecidedBy",
                    "DecidedAtUtc", "RestrictionDirective",
                    "ApprovalEvidenceSchemaVersion", "ApprovalEvidencePropertyId",
                    "ApprovalEvidencePropertyVersion",
                    "ApprovalEvidenceOperatingCountryCode", "ApprovalEvidencePolicyId",
                    "ApprovalEvidencePolicyVersion",
                    "ApprovalEvidenceRetentionPolicyId",
                    "ApprovalEvidenceRetentionPolicyVersion",
                    "ApprovalEvidenceContentSha256", "ApprovalEvidencePurposeCode",
                    "ApprovalEvidenceSurface", "ApprovalEvidenceSourceProvenance",
                    "ApprovalEvidenceEvaluatedAtUtc",
                    "ApprovalEvidenceRequiresDistinctExecutor",
                    "ExecutionRevision", "ExecutionStartedBy",
                    "ExecutionStartedAtUtc")
                VALUES (
                    {caseId}, {propertyId}, {(int)DataRightsCaseKind.GuestRights},
                    {(int)DataRightsCaseOperation.Anonymisation},
                    {(int)DataRightsRequesterRelation.ControllerInitiated},
                    {(int)DataRightsVerificationState.NotRequired},
                    {(int)DataRightsRoutingState.NotRequired},
                    {(int)DataRightsCaseState.Executing}, NULL, 7,
                    {"staff:privacy"}, {createdAtUtc}, {"staff:executor"},
                    {executionStartedAtUtc}, {"tenant-a"},
                    {(int)DataRightsCaseDecision.Approved},
                    {(int)DataRightsCaseDecisionReason.RequestValidated}, 6,
                    {"staff:decision-maker"}, {decidedAtUtc},
                    {(int)DataRightsRestrictionAction.None},
                    1, {propertyId}, 8, {"GB"}, {"integration-hostel-baseline"}, 1,
                    {"integration-guest-operational"}, 1, {policySha256},
                    {"data-rights-anonymisation"}, {"erasure"},
                    {"authorized-workspace-operator"}, {decidedAtUtc}, TRUE,
                    7, {"staff:executor"}, {executionStartedAtUtc});

                INSERT INTO "data-rights"."execution_work_items" (
                    "Id", "IdempotencyKey", "CaseId", "PropertyId",
                    "ApprovalRevision", "ExecutionRevision", "Operation", "OwnerKey",
                    "RecordType", "RecordId", "SelectedRecordVersion",
                    "PolicyEvidenceSchemaVersion", "PolicyId", "PolicyVersion",
                    "RetentionPolicyId", "RetentionPolicyVersion",
                    "PolicyContentSha256", "State", "AttemptCount", "CreatedBy",
                    "CreatedAtUtc", "Version", "ScopeId", "LastAttemptAtUtc",
                    "LastTaskAttempt", "OutcomeAtUtc", "OutcomeCode",
                    "OwnerCompletedAtUtc", "OwnerContractVersion",
                    "OwnerDispositionCode", "OwnerReasonCode",
                    "OwnerReceiptContractVersion", "OwnerReceiptId",
                    "OwnerReceiptSha256", "ResultingRecordVersion", "TaskRunId")
                VALUES (
                    {workItemId}, {idempotencyKey}, {caseId}, {propertyId}, 6, 7,
                    {(int)DataRightsCaseOperation.Anonymisation}, {"guests"},
                    {"guest-profile"}, {recordId}, 1, 1,
                    {"integration-hostel-baseline"}, 1,
                    {"integration-guest-operational"}, 1, {policySha256},
                    {(int)DataRightsExecutionWorkItemState.OwnerProofRecorded}, 1,
                    {"staff:executor"}, {executionStartedAtUtc}, 3, {"tenant-a"},
                    {executionStartedAtUtc}, 1, {outcomeAtUtc}, NULL,
                    {ownerCompletedAtUtc}, 1, {"completed"}, {"anonymised"},
                    1, {ownerReceiptId}, {ownerReceiptSha256}, 2, {taskRunId});

                INSERT INTO "data-rights"."processing_ledger_entries" (
                    "Id", "ContractVersion", "TenantSequence", "WorkItemId",
                    "CaseId", "ApprovalRevision", "OperationRevision", "Operation",
                    "RoutingPropertyId", "OwnerKey", "RecordType",
                    "RecordPseudonymKeyVersion", "RecordPseudonymSha256",
                    "DispositionCode", "ReasonCode", "CompletedAtUtc",
                    "PolicyEvidenceSchemaVersion", "PolicyId", "PolicyVersion",
                    "PolicyContentSha256", "RetentionPolicyId",
                    "RetentionPolicyVersion", "OwnerReceiptContractVersion",
                    "OwnerReceiptId", "OwnerReceiptSha256", "PreviousEntrySha256",
                    "EntrySha256", "ReplayOfLedgerEntryId",
                    "SupersedesLedgerEntryId", "ScopeId",
                    "ResultingRecordVersion")
                VALUES (
                    {ledgerEntryId}, 2, 1, {workItemId}, {caseId}, 6, 7,
                    {(int)DataRightsCaseOperation.Anonymisation}, {propertyId},
                    {"guests"}, {"guest-profile"}, 1, {recordPseudonymSha256},
                    {"completed"}, {"anonymised"}, {ownerCompletedAtUtc}, 1,
                    {"integration-hostel-baseline"}, 1, {policySha256},
                    {"integration-guest-operational"}, 1, 1, {ownerReceiptId},
                    {ownerReceiptSha256}, {new string('0', 64)}, {entrySha256},
                    NULL, NULL, {"tenant-a"}, 2);
                """);
        }

        await using (DataRightsDbContext upgraded =
                     CreateDbContext(postgreSql.GetConnectionString()))
        {
            await upgraded.Database.MigrateAsync();

            DataRightsExecutionBatch batch = await upgraded.ExecutionBatches
                .SingleAsync(item => item.Id == workItemId);
            DataRightsExecutionWorkItem workItem = await upgraded.ExecutionWorkItems
                .SingleAsync(item => item.Id == workItemId);
            DataRightsCase dataRightsCase = await upgraded.Cases
                .SingleAsync(item => item.Id == caseId);
            DataRightsProcessingLedgerEntry ledger =
                await upgraded.ProcessingLedgerEntries
                    .SingleAsync(item => item.Id == ledgerEntryId);

            Assert.Equal(idempotencyKey, batch.IdempotencyKey);
            Assert.Equal(1, batch.SelectedSubjectCount);
            Assert.Equal(DataRightsCaseKind.GuestRights, batch.CaseKind);
            Assert.Equal(DataRightsCaseScopeKind.Property, batch.ScopeKind);
            Assert.Equal(propertyId, batch.PropertyId);
            Assert.Equal(batch.Id, workItem.BatchId);
            Assert.Equal(DataRightsExecutionWorkItemState.Completed, workItem.State);
            Assert.Equal(DataRightsCaseKind.GuestRights, workItem.CaseKind);
            Assert.Equal(DataRightsCaseScopeKind.Property, workItem.ScopeKind);
            Assert.Equal(8, workItem.PolicyPropertyVersion);
            Assert.Equal("GB", workItem.PolicyOperatingCountryCode);
            Assert.Equal(
                "data-rights-anonymisation",
                workItem.PolicyPurposeCode);
            Assert.Equal("erasure", workItem.PolicySurface);
            Assert.Equal(
                "authorized-workspace-operator",
                workItem.PolicySourceProvenance);
            Assert.Equal("[]", workItem.PolicyStateBindingsJson);
            Assert.Equal(
                DataRightsApprovalEvidence.EmptyStateBindingsSha256,
                workItem.PolicyStateBindingsSha256);
            Assert.True(workItem.PolicyRequiresDistinctExecutor);
            Assert.Equal(DataRightsCaseState.Completed, dataRightsCase.Status);
            Assert.Equal("system:data-rights-migration", dataRightsCase.LastChangedBy);
            Assert.Equal(outcomeAtUtc, dataRightsCase.LastChangedAtUtc);
            Assert.Equal(8, dataRightsCase.Version);
            DataRightsApprovalPolicyEvidence approvalEvidence =
                Assert.IsType<DataRightsApprovalPolicyEvidence>(
                    dataRightsCase.ApprovalPolicyEvidence);
            Assert.Equal(DataRightsCaseKind.GuestRights, approvalEvidence.CaseKind);
            Assert.Equal(
                DataRightsCaseScopeKind.Property,
                approvalEvidence.ScopeKind);
            Assert.Equal("[]", approvalEvidence.StateBindingsJson);
            Assert.True(workItem.MatchesPolicyEvidence(approvalEvidence));
            Assert.Equal(
                DataRightsProcessingLedgerEntry
                    .GuestResultVersionContractVersion,
                ledger.ContractVersion);
            Assert.Equal(DataRightsCaseKind.Unknown, ledger.CaseKind);
            Assert.Equal(DataRightsCaseScopeKind.Unknown, ledger.ScopeKind);
            Assert.Null(ledger.PolicyPropertyVersion);
            Assert.Equal(entrySha256, ledger.EntrySha256);

            PostgresException appendOnly =
                await Assert.ThrowsAsync<PostgresException>(
                    () => upgraded.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "data-rights"."processing_ledger_entries"
                        SET "EntrySha256" = "EntrySha256"
                        WHERE "Id" = {ledgerEntryId}
                        """));
            Assert.Equal("P0001", appendOnly.SqlState);
            Assert.Contains("append-only", appendOnly.MessageText);

            IMigrator migrator = upgraded.Database.GetService<IMigrator>();
            upgraded.ChangeTracker.Clear();
            await migrator.MigrateAsync(
                BeforeScopedAnonymisationExecutionMigration);
            await migrator.MigrateAsync();

            DataRightsExecutionWorkItem roundTripped =
                await upgraded.ExecutionWorkItems
                    .SingleAsync(item => item.Id == workItemId);
            Assert.Equal(
                DataRightsApprovalEvidence.EmptyStateBindingsSha256,
                roundTripped.PolicyStateBindingsSha256);

            Guid scopedLedgerEntryId = Guid.NewGuid();
            Guid scopedWorkItemId = Guid.NewGuid();
            Guid scopedOwnerReceiptId = Guid.NewGuid();
            string scopedBindingsJson =
                """[{"key":"staff.record","version":1,"sha256":"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"}]""";
            await upgraded.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "data-rights"."processing_ledger_entries" (
                    "Id", "ContractVersion", "TenantSequence", "WorkItemId",
                    "CaseId", "ApprovalRevision", "OperationRevision", "Operation",
                    "CaseKind", "ScopeKind", "RoutingPropertyId", "OwnerKey",
                    "RecordType", "RecordPseudonymKeyVersion",
                    "RecordPseudonymSha256", "DispositionCode", "ReasonCode",
                    "CompletedAtUtc", "PolicyEvidenceSchemaVersion", "PolicyId",
                    "PolicyVersion", "PolicyContentSha256", "RetentionPolicyId",
                    "RetentionPolicyVersion", "PolicyPropertyVersion",
                    "PolicyOperatingCountryCode", "PolicyPurposeCode",
                    "PolicySurface", "PolicySourceProvenance",
                    "PolicyRetentionDataClass", "PolicyRetentionTrigger",
                    "PolicyRetentionTriggeredAtUtc", "PolicyRetentionDeadlineUtc",
                    "PolicyEvaluatedAtUtc", "PolicyStateBindingsJson",
                    "PolicyStateBindingsSha256", "PolicyRequiresDistinctExecutor",
                    "OwnerReceiptContractVersion", "OwnerReceiptId",
                    "OwnerReceiptSha256", "ResultingRecordVersion",
                    "PreviousEntrySha256", "EntrySha256",
                    "ReplayOfLedgerEntryId", "SupersedesLedgerEntryId", "ScopeId")
                VALUES (
                    {scopedLedgerEntryId}, 3, 2, {scopedWorkItemId}, {caseId},
                    6, 7, {(int)DataRightsCaseOperation.Anonymisation},
                    {(int)DataRightsCaseKind.StaffRights},
                    {(int)DataRightsCaseScopeKind.Tenant}, NULL, {"staff"},
                    {"staff-member"}, 1, {new string('e', 64)},
                    {"staff.completed"}, {"staff.member-anonymised"},
                    {outcomeAtUtc.AddDays(2)}, 2,
                    {"integration-hostel-baseline"}, 1, {policySha256},
                    {"integration-staff-employment"}, 1, 0, {"GB"},
                    {"staff-data-rights-anonymisation"}, {"erasure"},
                    {"authorized-workspace-operator"}, {"staff-employment"},
                    {"employment-ended"}, {outcomeAtUtc},
                    {outcomeAtUtc.AddDays(1)}, {outcomeAtUtc.AddDays(2)},
                    {scopedBindingsJson}, {new string('f', 64)}, TRUE, 1,
                    {scopedOwnerReceiptId}, {new string('b', 64)}, 2,
                    {entrySha256}, {new string('f', 64)}, NULL, NULL,
                    {"tenant-a"});
                """);

            PostgresException unsafeDowngrade =
                await Assert.ThrowsAsync<PostgresException>(
                    () => migrator.MigrateAsync(
                        BeforeScopedAnonymisationExecutionMigration));
            Assert.Equal("P0001", unsafeDowngrade.SqlState);
            Assert.Contains(
                "version 3 processing-ledger",
                unsafeDowngrade.MessageText);
        }
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Staff_rights_migration_preserves_existing_cases_and_enforces_tenant_scope()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_data_rights_staff_scope_migration_tests")
                .Build();
        await postgreSql.StartAsync();

        string connectionString = postgreSql.GetConnectionString();
        Guid propertyId = Guid.NewGuid();
        DataRightsCase guest = CreateCase(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsRequesterRelation.ControllerInitiated);
        DataRightsCase tenantTermination = CreateCase(
            propertyId: null,
            DataRightsCaseKind.TenantTermination,
            DataRightsRequesterRelation.TenantOwner);

        await using (DataRightsDbContext previous = CreateDbContext(connectionString))
        {
            await previous.Database.GetService<IMigrator>()
                .MigrateAsync(BeforeStaffRightsMigration);
            await SeedLegacyCaseAsync(
                previous,
                guest.Id,
                propertyId,
                DataRightsCaseKind.GuestRights,
                DataRightsCaseOperation.AccessExport,
                DataRightsRequesterRelation.ControllerInitiated);
            await SeedLegacyCaseAsync(
                previous,
                tenantTermination.Id,
                propertyId: null,
                DataRightsCaseKind.TenantTermination,
                DataRightsCaseOperation.Anonymisation,
                DataRightsRequesterRelation.TenantOwner);
        }

        await using DataRightsDbContext upgraded = CreateDbContext(connectionString);
        await upgraded.Database.GetService<IMigrator>()
            .MigrateAsync(StaffRightsMigration);

        LegacyCaseCoordinates[] preserved =
            await ReadLegacyCaseCoordinatesAsync(connectionString);
        Assert.Equal(2, preserved.Length);
        Assert.Contains(preserved, item =>
            item.Id == guest.Id &&
            item.Kind == DataRightsCaseKind.GuestRights &&
            item.PropertyId == propertyId);
        Assert.Contains(preserved, item =>
            item.Id == tenantTermination.Id &&
            item.Kind == DataRightsCaseKind.TenantTermination &&
            item.PropertyId is null);

        Guid staffCaseId = Guid.NewGuid();
        await SeedLegacyCaseAsync(
            upgraded,
            staffCaseId,
            propertyId: null,
            DataRightsCaseKind.StaffRights,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.DataSubject);
        LegacyCaseCoordinates[] withStaff =
            await ReadLegacyCaseCoordinatesAsync(connectionString);
        Assert.Contains(withStaff, item =>
            item.Id == staffCaseId &&
            item.Kind == DataRightsCaseKind.StaffRights &&
            item.PropertyId is null);

        await AssertConstraintViolationAsync(
            upgraded,
            Guid.NewGuid(),
            propertyId,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.DataSubject,
            "CK_data_rights_cases_property_scope");
        await AssertConstraintViolationAsync(
            upgraded,
            Guid.NewGuid(),
            propertyId: null,
            DataRightsCaseOperation.Correction,
            DataRightsRequesterRelation.DataSubject,
            "CK_data_rights_cases_operations");
        await AssertConstraintViolationAsync(
            upgraded,
            Guid.NewGuid(),
            propertyId: null,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.TenantOwner,
            "CK_data_rights_cases_requester_scope");
    }

    private static DataRightsCase CreateCase(
        Guid? propertyId,
        DataRightsCaseKind kind,
        DataRightsRequesterRelation requesterRelationship)
    {
        DataRightsCaseOperation requestedOperations =
            kind == DataRightsCaseKind.TenantTermination
                ? DataRightsCaseOperation.Anonymisation
                : DataRightsCaseOperation.AccessExport;
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            kind,
            requestedOperations,
            requesterRelationship).Value;
        return DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "staff:privacy",
            new DateTimeOffset(2026, 7, 27, 0, 0, 0, TimeSpan.Zero)).Value;
    }

    private static async Task AssertConstraintViolationAsync(
        DataRightsDbContext dbContext,
        Guid caseId,
        Guid? propertyId,
        DataRightsCaseOperation operations,
        DataRightsRequesterRelation requesterRelationship,
        string expectedConstraint)
    {
        DateTimeOffset createdAtUtc =
            new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "data-rights"."cases"
                    ("Id", "PropertyId", "Kind", "RequestedOperations",
                     "RequesterRelationship", "VerificationStatus", "RoutingStatus",
                     "Status", "DueAtUtc", "Version", "CreatedBy", "CreatedAtUtc",
                     "LastChangedBy", "LastChangedAtUtc", "ScopeId")
                VALUES
                    ({caseId}, {propertyId}, {(int)DataRightsCaseKind.StaffRights},
                     {(int)operations}, {(int)requesterRelationship},
                     {(int)DataRightsVerificationState.Pending},
                     {(int)DataRightsRoutingState.Pending},
                     {(int)DataRightsCaseState.Draft}, NULL, 1, {"staff:privacy"},
                     {createdAtUtc}, {"staff:privacy"}, {createdAtUtc}, {"tenant-a"})
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(expectedConstraint, exception.ConstraintName);
    }

    private static Task<int> SeedLegacyCaseAsync(
        DataRightsDbContext dbContext,
        Guid caseId,
        Guid? propertyId,
        DataRightsCaseKind kind,
        DataRightsCaseOperation operations,
        DataRightsRequesterRelation requesterRelationship)
    {
        DateTimeOffset createdAtUtc =
            new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "data-rights"."cases"
                ("Id", "PropertyId", "Kind", "RequestedOperations",
                 "RequesterRelationship", "VerificationStatus", "RoutingStatus",
                 "Status", "Decision", "DecisionReason", "RestrictionDirective",
                 "DueAtUtc", "Version", "CreatedBy", "CreatedAtUtc",
                 "LastChangedBy", "LastChangedAtUtc", "ScopeId")
            VALUES
                ({caseId}, {propertyId}, {(int)kind}, {(int)operations},
                 {(int)requesterRelationship},
                 {(int)DataRightsVerificationState.NotRequired},
                 {(int)DataRightsRoutingState.NotRequired},
                 {(int)DataRightsCaseState.Draft},
                 {(int)DataRightsCaseDecision.Unknown},
                 {(int)DataRightsCaseDecisionReason.Unknown}, 0, NULL, 1,
                 {"staff:privacy"}, {createdAtUtc}, {"staff:privacy"},
                 {createdAtUtc}, {"tenant-a"})
            """);
    }

    private static async Task<LegacyCaseCoordinates[]>
        ReadLegacyCaseCoordinatesAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT "Id", "Kind", "PropertyId"
            FROM "data-rights"."cases"
            ORDER BY "Id"
            """,
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        List<LegacyCaseCoordinates> cases = [];
        while (await reader.ReadAsync())
        {
            cases.Add(new LegacyCaseCoordinates(
                reader.GetGuid(0),
                (DataRightsCaseKind)reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2)));
        }

        return [.. cases];
    }

    private static DataRightsDbContext CreateDbContext(string connectionString)
    {
        DbContextOptions<DataRightsDbContext> options = new DbContextOptionsBuilder<DataRightsDbContext>()
            .UseNpgsql(connectionString, provider => provider
                .MigrationsAssembly(DataRightsMigrations.PostgreSqlAssembly)
                .MigrationsHistoryTable(DataRightsMigrations.HistoryTable, DataRightsMigrations.Schema))
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed record LegacyCaseCoordinates(
        Guid Id,
        DataRightsCaseKind Kind,
        Guid? PropertyId);
}
