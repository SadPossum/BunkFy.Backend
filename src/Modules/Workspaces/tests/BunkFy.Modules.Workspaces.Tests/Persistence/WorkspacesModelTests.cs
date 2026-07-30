namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using BunkFy.Modules.Workspaces.Persistence;
using BunkFy.Modules.Workspaces.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspacesModelTests
{
    [Fact]
    public void Staff_onboarding_is_tenant_filtered_and_concurrency_protected()
    {
        DbContextOptions<WorkspacesDbContext> options = new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using WorkspacesDbContext context = new(options, new TestScopeContext());
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity = context.Model
            .FindEntityType(typeof(WorkspaceStaffOnboarding))!;

        Assert.NotNull(entity.FindProperty(nameof(WorkspaceStaffOnboarding.Version))?.IsConcurrencyToken);
        Assert.True(entity.FindProperty(nameof(WorkspaceStaffOnboarding.Version))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(WorkspaceStaffOnboarding.ScopeId),
                nameof(WorkspaceStaffOnboarding.SourceKind),
                nameof(WorkspaceStaffOnboarding.SourceId),
                nameof(WorkspaceStaffOnboarding.SubjectId)]));
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(WorkspaceStaffOnboarding.ScopeId),
                nameof(WorkspaceStaffOnboarding.SubjectId),
                nameof(WorkspaceStaffOnboarding.Status),
                nameof(WorkspaceStaffOnboarding.Id)]));
        Microsoft.EntityFrameworkCore.Metadata.IIndex staffLookupIndex =
            Assert.Single(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(WorkspaceStaffOnboarding.ScopeId),
                nameof(WorkspaceStaffOnboarding.StaffMemberId),
                nameof(WorkspaceStaffOnboarding.Id)]));
        Assert.Equal(
            "\"StaffMemberId\" IS NOT NULL",
            staffLookupIndex.GetFilter());
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Staff_access_process_is_tenant_filtered_versioned_and_staff_version_unique()
    {
        DbContextOptions<WorkspacesDbContext> options = new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using WorkspacesDbContext context = new(options, new TestScopeContext());
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity = context.Model
            .FindEntityType(typeof(WorkspaceStaffAccessProcess))!;

        Assert.True(entity.FindProperty(nameof(WorkspaceStaffAccessProcess.Version))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(WorkspaceStaffAccessProcess.ScopeId),
                nameof(WorkspaceStaffAccessProcess.StaffMemberId),
                nameof(WorkspaceStaffAccessProcess.TargetStaffVersion)]));
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(WorkspaceStaffAccessProcess.ScopeId),
                nameof(WorkspaceStaffAccessProcess.SubjectId),
                nameof(WorkspaceStaffAccessProcess.State),
                nameof(WorkspaceStaffAccessProcess.Id)]));
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(WorkspaceStaffAccessProcess.ScopeId),
                nameof(WorkspaceStaffAccessProcess.RequestedBy),
                nameof(WorkspaceStaffAccessProcess.Id)]));
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());

        Microsoft.EntityFrameworkCore.Metadata.IEntityType snapshot = context.Model
            .FindEntityType(typeof(WorkspaceStaffAccessProfileSnapshot))!;
        Assert.Equal(
            [
                "ProcessId",
                nameof(WorkspaceStaffAccessProfileSnapshot.ProfileId),
                nameof(WorkspaceStaffAccessProfileSnapshot.AssignmentScope)
            ],
            snapshot.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
        Assert.Equal(
            WorkspaceStaffAccessProfileSnapshot.AssignmentScopeMaxLength,
            snapshot.FindProperty(nameof(WorkspaceStaffAccessProfileSnapshot.AssignmentScope))!
                .GetMaxLength());
    }

    [Fact]
    public void Staff_access_plan_persists_source_expiry_and_is_concurrency_protected()
    {
        DbContextOptions<WorkspacesDbContext> options = new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using WorkspacesDbContext context = new(options, new TestScopeContext());
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity = context.Model
            .FindEntityType(typeof(WorkspaceStaffAccessPlan))!;

        Assert.True(entity.FindProperty(nameof(WorkspaceStaffAccessPlan.Version))!.IsConcurrencyToken);
        Assert.True(entity.FindProperty(nameof(WorkspaceStaffAccessPlan.SourceExpiredAtUtc))!.IsNullable);
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(WorkspaceStaffAccessPlan.ScopeId),
                nameof(WorkspaceStaffAccessPlan.SourceKind),
                nameof(WorkspaceStaffAccessPlan.SourceExpiredAtUtc),
                nameof(WorkspaceStaffAccessPlan.Id)]));
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(WorkspaceStaffAccessPlan.ScopeId),
                nameof(WorkspaceStaffAccessPlan.CreatedBySubjectId),
                nameof(WorkspaceStaffAccessPlan.Id)]));
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Staff_retention_correlation_receipt_is_tenant_filtered_and_unique_per_staff_version()
    {
        DbContextOptions<WorkspacesDbContext> options =
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        using WorkspacesDbContext context =
            new(options, new TestScopeContext());
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity =
            context.Model.FindEntityType(
                typeof(
                    WorkspaceStaffRetentionCorrelationReceipt))!;

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(
                        WorkspaceStaffRetentionCorrelationReceipt
                            .ScopeId),
                    nameof(
                        WorkspaceStaffRetentionCorrelationReceipt
                            .StaffMemberId),
                    nameof(
                        WorkspaceStaffRetentionCorrelationReceipt
                            .SelectedStaffVersion)]));
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());
    }

    [Fact]
    public async Task Staff_onboarding_correction_receipt_is_unique_and_append_only()
    {
        DbContextOptions<WorkspacesDbContext> options =
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        await using WorkspacesDbContext context =
            new(options, new TestScopeContext());
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity =
            context.Model.FindEntityType(
                typeof(WorkspaceStaffOnboardingCorrectionReceipt))!;

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(
                        WorkspaceStaffOnboardingCorrectionReceipt
                            .ScopeId),
                    nameof(
                        WorkspaceStaffOnboardingCorrectionReceipt
                            .ExecutionId)
                ]));
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(
                        WorkspaceStaffOnboardingCorrectionReceipt
                            .ScopeId),
                    nameof(
                        WorkspaceStaffOnboardingCorrectionReceipt
                            .ApplicationId),
                    nameof(
                        WorkspaceStaffOnboardingCorrectionReceipt
                            .CompletedAtUtc),
                    nameof(
                        WorkspaceStaffOnboardingCorrectionReceipt
                            .Id)
                ]));
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());

        WorkspaceStaffOnboardingCorrectionReceipt receipt =
            WorkspaceStaffOnboardingCorrectionReceipt.Create(
                Guid.NewGuid(),
                WorkspaceStaffOnboardingTests.OrganizationId.ToString("D"),
                Guid.NewGuid(),
                Guid.NewGuid(),
                approvalRevision: 1,
                Guid.NewGuid(),
                selectedRecordVersion: 1,
                currentRecordVersion: 2,
                [WorkspaceStaffOnboardingApplicantField.DisplayName],
                new string('a', 64),
                Guid.NewGuid(),
                Guid.NewGuid(),
                WorkspaceStaffOnboardingTests.Now).Value;
        context.StaffOnboardingCorrectionReceipts.Add(receipt);
        await context.SaveChangesAsync();
        context.Entry(receipt).State = EntityState.Modified;

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.SaveChangesAsync());
        Assert.Equal(
            "Workspace immutable receipts are append-only.",
            error.Message);
    }

    [Fact]
    public async Task Staff_onboarding_retention_candidates_are_tenant_isolated_ordered_and_bounded()
    {
        string databaseName = Guid.NewGuid().ToString("N");
        DbContextOptions<WorkspacesDbContext> options =
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
        Guid tenantA = WorkspaceStaffOnboardingTests.OrganizationId;
        Guid tenantB = Guid.Parse("10000000-0000-0000-0000-000000000099");
        DateTimeOffset nowUtc = WorkspaceStaffOnboardingTests.Now.AddDays(7);
        (WorkspaceStaffOnboarding First, WorkspaceStaffAccessPlan FirstPlan) =
            CreateRetentionPair(
                tenantA,
                Guid.Parse("30000000-0000-0000-0000-000000000001"),
                Guid.Parse("40000000-0000-0000-0000-000000000001"),
                nowUtc.AddHours(-5));
        (WorkspaceStaffOnboarding Second, WorkspaceStaffAccessPlan SecondPlan) =
            CreateRetentionPair(
                tenantA,
                Guid.Parse("30000000-0000-0000-0000-000000000002"),
                Guid.Parse("40000000-0000-0000-0000-000000000002"),
                nowUtc.AddHours(-4));
        (WorkspaceStaffOnboarding Fresh, WorkspaceStaffAccessPlan FreshPlan) =
            CreateRetentionPair(
                tenantA,
                Guid.Parse("30000000-0000-0000-0000-000000000003"),
                Guid.Parse("40000000-0000-0000-0000-000000000003"),
                nowUtc.AddHours(-1));
        (WorkspaceStaffOnboarding Other, WorkspaceStaffAccessPlan OtherPlan) =
            CreateRetentionPair(
                tenantB,
                Guid.Parse("30000000-0000-0000-0000-000000000004"),
                Guid.Parse("40000000-0000-0000-0000-000000000004"),
                nowUtc.AddHours(-8));

        await using (WorkspacesDbContext seed = new(
            options,
            new TestScopeContext(enabled: false, scopeId: null)))
        {
            seed.AddRange(
                First, FirstPlan,
                Second, SecondPlan,
                Fresh, FreshPlan,
                Other, OtherPlan);
            await seed.SaveChangesAsync();
        }

        await using WorkspacesDbContext context = new(
            options,
            new TestScopeContext(enabled: true, tenantA.ToString("D")));
        WorkspaceStaffOnboardingRetentionRepository repository = new(context);

        IReadOnlyList<WorkspaceStaffOnboardingRetentionCandidate> candidates =
            await repository.ListEligibleAsync(
                tenantA.ToString("D"),
                nowUtc.AddHours(-2),
                2,
                CancellationToken.None);
        IReadOnlyList<WorkspaceStaffOnboardingRetentionCandidate> otherTenant =
            await repository.ListEligibleAsync(
                tenantB.ToString("D"),
                nowUtc.AddHours(-2),
                2,
                CancellationToken.None);

        Assert.Collection(
            candidates,
            candidate => Assert.Equal(First.Id, candidate.ApplicationId),
            candidate => Assert.Equal(Second.Id, candidate.ApplicationId));
        Assert.Empty(otherTenant);
    }

    private static (
        WorkspaceStaffOnboarding Application,
        WorkspaceStaffAccessPlan Plan) CreateRetentionPair(
            Guid tenantId,
            Guid applicationId,
            Guid sourceId,
            DateTimeOffset sourceExpiredAtUtc)
    {
        string scopeId = tenantId.ToString("D");
        WorkspaceStaffOnboarding application = WorkspaceStaffOnboarding.Create(
            applicationId,
            scopeId,
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            sourceId,
            WorkspaceStaffOnboardingTests.SubjectId,
            "verified@example.test",
            "Ada Operator",
            null,
            null,
            null,
            null,
            null,
            null,
            WorkspaceStaffOnboardingTests.Now).Value;
        WorkspaceStaffAccessPlan plan = WorkspaceStaffAccessPlan.Create(
            sourceId,
            scopeId,
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            Guid.NewGuid(),
            "front-desk",
            [],
            WorkspaceStaffOnboardingTests.SubjectId,
            WorkspaceStaffOnboardingTests.Now).Value;
        Assert.True(plan.Activate(
            WorkspaceStaffOnboardingTests.Now.AddSeconds(1)).IsSuccess);
        Assert.True(plan.ObserveSourceExpired(
            sourceExpiredAtUtc,
            sourceExpiredAtUtc).IsSuccess);
        return (application, plan);
    }

    private sealed class TestScopeContext(
        bool enabled = true,
        string? scopeId = null)
        : IScopeContext
    {
        public bool IsEnabled => enabled;
        public string? ScopeId => scopeId ??
            (enabled
                ? WorkspaceStaffOnboardingTests.OrganizationId.ToString("D")
                : null);
    }
}
