namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Persistence;
using BunkFy.Modules.Workspaces.Persistence.Repositories;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class
    WorkspaceStaffRetentionCorrelationRepositoryTests
{
    private const string TenantId =
        "10000000-0000-0000-0000-000000000001";
    private const string SubjectId = "subject-a";
    private static readonly Guid StaffMemberId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Active_onboarding_blocks_before_any_scrub()
    {
        await using WorkspacesDbContext context = CreateContext();
        context.StaffOnboardingApplications.Add(
            CreateOnboarding());
        await context.SaveChangesAsync();
        WorkspaceStaffRetentionCorrelationRepository repository =
            new(context);

        Result<WorkspaceStaffRetentionCorrelationReceipt> result =
            await repository.ScrubAsync(
                CreateRequest(SubjectId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            WorkspaceStaffRetentionErrors.ActiveOnboarding,
            result.Error);
        Assert.Empty(context.StaffRetentionCorrelationReceipts);
    }

    [Fact]
    public async Task Stale_departure_mapping_fails_closed()
    {
        await using WorkspacesDbContext context = CreateContext();
        WorkspaceStaffAccessProcess process =
            CreateCompletedDeparture("other-subject");
        context.StaffAccessProcesses.Add(process);
        await context.SaveChangesAsync();
        WorkspaceStaffRetentionCorrelationRepository repository =
            new(context);

        Result<WorkspaceStaffRetentionCorrelationReceipt> result =
            await repository.ScrubAsync(
                CreateRequest(SubjectId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            WorkspaceStaffRetentionErrors.AccessMappingConflict,
            result.Error);
        Assert.Equal("other-subject", process.SubjectId);
        Assert.Empty(context.StaffRetentionCorrelationReceipts);
    }

    [Fact]
    public async Task Active_access_process_blocks_before_any_scrub()
    {
        await using WorkspacesDbContext context = CreateContext();
        context.StaffAccessProcesses.Add(
            CreateCompletedDeparture(SubjectId));
        context.StaffAccessProcesses.Add(
            CreateOpenSuspension());
        await context.SaveChangesAsync();
        WorkspaceStaffRetentionCorrelationRepository repository =
            new(context);

        Result<WorkspaceStaffRetentionCorrelationReceipt> result =
            await repository.ScrubAsync(
                CreateRequest(SubjectId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            WorkspaceStaffRetentionErrors.ActiveAccessProcess,
            result.Error);
        Assert.Empty(context.StaffRetentionCorrelationReceipts);
    }

    [Fact]
    public async Task Corrupted_existing_receipt_fails_closed()
    {
        await using WorkspacesDbContext context = CreateContext();
        WorkspaceStaffRetentionCorrelationReceipt receipt =
            CreateReceipt().Value;
        typeof(WorkspaceStaffRetentionCorrelationReceipt)
            .GetProperty(
                nameof(
                    WorkspaceStaffRetentionCorrelationReceipt
                        .CanonicalSha256))!
            .SetValue(
                receipt,
                new string(
                    'a',
                    WorkspaceStaffRetentionCorrelationReceipt
                        .Sha256Length));
        context.StaffRetentionCorrelationReceipts.Add(receipt);
        await context.SaveChangesAsync();
        WorkspaceStaffRetentionCorrelationRepository repository =
            new(context);

        Result<WorkspaceStaffRetentionCorrelationReceipt> result =
            await repository.ScrubAsync(
                CreateRequest(subjectId: null),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            WorkspaceStaffRetentionErrors.ReceiptInvalid,
            result.Error);
        Assert.Single(context.StaffRetentionCorrelationReceipts);
    }

    [Fact]
    public async Task Unlinked_departed_staff_produces_zero_count_proof()
    {
        await using WorkspacesDbContext context = CreateContext();
        WorkspaceStaffRetentionCorrelationRepository repository =
            new(context);

        Result<WorkspaceStaffRetentionCorrelationReceipt> result =
            await repository.ScrubAsync(
                CreateRequest(subjectId: null),
                CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(0, result.Value.OnboardingRecordsScrubbed);
        Assert.Equal(0, result.Value.AccessProcessRecordsScrubbed);
        Assert.Equal(0, result.Value.AccessPlanRecordsScrubbed);
        Assert.True(result.Value.HasValidCanonicalProof());
        Assert.Single(context.StaffRetentionCorrelationReceipts);
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task Receipt_mutation_is_rejected(
        EntityState requestedState)
    {
        await using WorkspacesDbContext context = CreateContext();
        WorkspaceStaffRetentionCorrelationReceipt receipt =
            CreateReceipt().Value;
        context.StaffRetentionCorrelationReceipts.Add(receipt);
        await context.SaveChangesAsync();
        context.Entry(receipt).State = requestedState;

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.SaveChangesAsync());

        Assert.Contains("append-only", error.Message);
    }

    private static WorkspacesDbContext CreateContext()
    {
        DbContextOptions<WorkspacesDbContext> options =
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new(options, new TestScopeContext());
    }

    private static WorkspaceStaffOnboarding CreateOnboarding() =>
        WorkspaceStaffOnboarding.Create(
            Guid.NewGuid(),
            TenantId,
            WorkspaceStaffOnboardingSource.Invitation,
            Guid.NewGuid(),
            SubjectId,
            "subject@example.test",
            "Subject A",
            legalName: null,
            workEmail: null,
            workPhone: null,
            employeeNumber: null,
            jobTitle: null,
            department: null,
            Now).Value;

    private static WorkspaceStaffAccessProcess
        CreateCompletedDeparture(string subjectId)
    {
        WorkspaceStaffAccessProcess process =
            WorkspaceStaffAccessProcess.Create(
                Guid.NewGuid(),
                TenantId,
                StaffMemberId,
                subjectId,
                WorkspaceStaffAccessTargetState.Departed,
                targetStaffVersion: 2,
                new DateOnly(2026, 7, 1),
                "system:test",
                [],
                Now).Value;
        Assert.True(
            process.MarkAwaitingStaffCommit(
                Now.AddSeconds(1)).IsSuccess);
        Assert.True(
            process.ObserveStaffCommit(
                Now.AddSeconds(2)).IsSuccess);
        return process;
    }

    private static WorkspaceStaffAccessProcess
        CreateOpenSuspension() =>
        WorkspaceStaffAccessProcess.Create(
            Guid.NewGuid(),
            TenantId,
            StaffMemberId,
            SubjectId,
            WorkspaceStaffAccessTargetState.Suspended,
            targetStaffVersion: 3,
            new DateOnly(2026, 6, 1),
            SubjectId,
            [],
            Now.AddDays(-30)).Value;

    private static WorkspaceStaffRetentionCorrelationScrubRequest
        CreateRequest(string? subjectId) =>
        new(
            ReceiptId: Guid.Parse(
                "30000000-0000-0000-0000-000000000001"),
            ExecutionId: Guid.Parse(
                "40000000-0000-0000-0000-000000000001"),
            TenantId,
            StaffMemberId,
            SelectedStaffVersion: 2,
            subjectId,
            Now.AddMinutes(1));

    private static Result<WorkspaceStaffRetentionCorrelationReceipt>
        CreateReceipt() =>
        WorkspaceStaffRetentionCorrelationReceipt.Create(
            Guid.Parse(
                "30000000-0000-0000-0000-000000000001"),
            TenantId,
            Guid.Parse(
                "40000000-0000-0000-0000-000000000001"),
            StaffMemberId,
            selectedStaffVersion: 2,
            onboardingRecordsScrubbed: 0,
            accessProcessRecordsScrubbed: 0,
            accessPlanRecordsScrubbed: 0,
            Now.AddMinutes(1));

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }
}
