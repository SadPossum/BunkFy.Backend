namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Governance;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Staff.Persistence.Models;
using BunkFy.Modules.Staff.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffRetentionCandidateRepositoryTests
{
    [Fact]
    public async Task Scan_is_bounded_aggregated_and_tenant_scoped()
    {
        DbContextOptions<StaffDbContext> options =
            new DbContextOptionsBuilder<StaffDbContext>()
                .UseInMemoryDatabase(
                    $"staff-retention-candidates-{Guid.NewGuid():N}")
                .Options;
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        Guid tenantAStaffMemberId = Guid.NewGuid();
        await SeedAsync(
            options,
            "tenant-a",
            tenantAStaffMemberId,
            projectionOrdinal: 1,
            policy.Governance);
        await SeedAsync(
            options,
            "tenant-b",
            Guid.NewGuid(),
            projectionOrdinal: 2,
            policy.Governance);

        await using StaffDbContext dbContext = new(
            options,
            new StaffRetentionModelTests.TestScopeContext("tenant-a"));
        StaffRetentionCandidateRepository repository = new(dbContext);

        StaffRetentionScanPage page =
            await repository.ScanAsync(
                afterProjectionOrdinal: 0,
                limit: 1,
                CancellationToken.None);

        StaffRetentionCandidateSnapshot candidate =
            Assert.Single(page.Candidates);
        Assert.Equal(
            tenantAStaffMemberId,
            candidate.StaffMemberId);
        Assert.True(page.ReachedEnd);
        Assert.False(candidate.HasCurrentAssignments);
        Assert.Equal(0, candidate.ActiveHoldCount);
        Assert.Equal(
            StaffProcessingRestrictionContract.CurrentVersion,
            candidate.ProcessingRestrictionContractVersion);
        Assert.Equal(0, candidate.ProcessingRestrictionRevision);
        Assert.Equal(1, candidate.OperationLockRevision);
        Assert.NotNull(candidate.Governance);
        Assert.Equal(
            candidate.StaffVersion,
            candidate.Governance.SelectedStaffVersion);
    }

    private static async Task SeedAsync(
        DbContextOptions<StaffDbContext> options,
        string tenantId,
        Guid staffMemberId,
        long projectionOrdinal,
        StaffRetentionGovernanceSnapshot policy)
    {
        await using StaffDbContext dbContext = new(
            options,
            new StaffRetentionModelTests.TestScopeContext(tenantId));
        DateTimeOffset departedAtUtc =
            StaffRetentionTestData.Now.AddDays(-400);
        var member =
            StaffRetentionModelTests.CreateDepartedMember(
                tenantId,
                departedAtUtc,
                staffMemberId);
        dbContext.Entry(member)
            .Property(item => item.ProjectionOrdinal)
            .CurrentValue = projectionOrdinal;
        StaffEmploymentGovernanceBinding binding =
            StaffEmploymentGovernanceBinding.Create(
                policy.OperatingCountryCode,
                policy.PolicyId,
                policy.PolicyVersion,
                policy.DataRegionId,
                policy.TransferProfileId,
                policy.RetentionPolicyId,
                policy.RetentionPolicyVersion,
                policy.ContentSha256,
                policy.PolicyEffectiveAtUtc,
                policy.PolicyExpiresAtUtc,
                policy.EvaluatedAtUtc).Value;
        StaffEmploymentGovernance governance =
            StaffEmploymentGovernance.Configure(
                tenantId,
                staffMemberId,
                member.Version,
                binding,
                [],
                "user:privacy",
                policy.ConfiguredAtUtc).Value;
        StaffProcessingRestrictionProjection restriction =
            StaffProcessingRestrictionProjection.Create(
                tenantId,
                staffMemberId,
                StaffProcessingRestrictionContract.CurrentVersion,
                departedAtUtc).Value;
        StaffOperationLock operationLock = new(
            staffMemberId,
            tenantId,
            staffMemberId);

        dbContext.StaffMembers.Add(member);
        dbContext.EmploymentGovernance.Add(governance);
        dbContext.ProcessingRestrictionProjections.Add(restriction);
        dbContext.OperationLocks.Add(operationLock);
        await dbContext.SaveChangesAsync();
    }
}
