namespace BunkFy.Modules.Staff.Tests.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Staff.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffDataRightsDiscoveryContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Exact_record_or_account_subject_finds_departed_staff_with_masked_hints()
    {
        await using StaffDbContext dbContext = CreateDbContext("tenant-a");
        StaffMember member = CreateMember(
            "tenant-a",
            "Maya Chen",
            "maya.chen@example.test",
            "+44 20 1234 5678",
            "account-maya");
        Assert.True(member.Depart(
            new DateOnly(2026, 7, 26),
            member.Version,
            "user:manager",
            "Employment ended.",
            Guid.NewGuid(),
            [],
            Now.AddDays(1)).IsSuccess);
        dbContext.StaffMembers.Add(member);
        await dbContext.SaveChangesAsync();
        StaffDataRightsDiscoveryContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));

        DataRightsSubjectDiscoveryResult byRecord = await contributor.DiscoverAsync(
            Request("tenant-a", new DataRightsSubjectLookup(
                member.Id,
                Email: null,
                Phone: null,
                Name: null,
                DateOfBirth: null)),
            CancellationToken.None);
        DataRightsSubjectDiscoveryResult byAccount = await contributor.DiscoverAsync(
            Request("tenant-a", new DataRightsSubjectLookup(
                RecordId: null,
                Email: null,
                Phone: null,
                Name: null,
                DateOfBirth: null,
                AccountSubjectId: " account-maya ")),
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectDiscoveryStatus.Succeeded, byRecord.Status);
        DataRightsSubjectCandidate recordCandidate = Assert.Single(byRecord.Candidates);
        DataRightsSubjectCandidate accountCandidate = Assert.Single(byAccount.Candidates);
        Assert.Equal(member.Id, recordCandidate.Coordinate.RecordId);
        Assert.Equal(member.Version, recordCandidate.Coordinate.RecordVersion);
        Assert.Equal(recordCandidate, accountCandidate);
        Assert.Equal("Maya Chen", recordCandidate.DisplayName);
        Assert.Equal("m***@example.test", recordCandidate.EmailHint);
        Assert.Equal("***5678", recordCandidate.PhoneHint);
    }

    [Fact]
    public async Task Discovery_rejects_weak_mixed_or_cross_scope_coordinates()
    {
        await using StaffDbContext dbContext = CreateDbContext("tenant-a");
        StaffDataRightsDiscoveryContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));
        DataRightsSubjectLookup emailOnly = new(
            RecordId: null,
            Email: "staff@example.test",
            Phone: null,
            Name: null,
            DateOfBirth: null);
        DataRightsSubjectLookup mixed = new(
            Guid.NewGuid(),
            Email: null,
            Phone: null,
            Name: null,
            DateOfBirth: null,
            AccountSubjectId: "account");

        DataRightsSubjectDiscoveryResult weak = await contributor.DiscoverAsync(
            Request("tenant-a", emailOnly),
            CancellationToken.None);
        DataRightsSubjectDiscoveryResult twoCoordinates = await contributor.DiscoverAsync(
            Request("tenant-a", mixed),
            CancellationToken.None);
        DataRightsSubjectDiscoveryResult wrongTenant = await contributor.DiscoverAsync(
            Request("tenant-b", new(Guid.NewGuid(), null, null, null, null)),
            CancellationToken.None);
        DataRightsSubjectDiscoveryResult propertyScoped = await contributor.DiscoverAsync(
            new(
                "tenant-a",
                DataRightsCaseType.StaffRights,
                Guid.NewGuid(),
                new(Guid.NewGuid(), null, null, null, null),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);
        DataRightsSubjectDiscoveryResult guestCase = await contributor.DiscoverAsync(
            new(
                "tenant-a",
                DataRightsCaseType.GuestRights,
                PropertyId: null,
                new(Guid.NewGuid(), null, null, null, null),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);

        Assert.All(
            [weak, twoCoordinates, wrongTenant, propertyScoped, guestCase],
            result => Assert.Equal(
                DataRightsSubjectDiscoveryStatus.ScopeUnavailable,
                result.Status));
    }

    [Fact]
    public async Task Selection_revalidation_returns_valid_stale_or_not_found()
    {
        await using StaffDbContext dbContext = CreateDbContext("tenant-a");
        StaffMember member = CreateMember(
            "tenant-a",
            "Staff member",
            null,
            null,
            "account-staff");
        dbContext.StaffMembers.Add(member);
        await dbContext.SaveChangesAsync();
        StaffDataRightsDiscoveryContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));

        DataRightsSubjectSelectionValidation valid = await contributor.ValidateSelectionAsync(
            Selection("tenant-a", member.Id, member.Version),
            CancellationToken.None);
        DataRightsSubjectSelectionValidation stale = await contributor.ValidateSelectionAsync(
            Selection("tenant-a", member.Id, member.Version + 1),
            CancellationToken.None);
        DataRightsSubjectSelectionValidation missing = await contributor.ValidateSelectionAsync(
            Selection("tenant-a", Guid.NewGuid(), 1),
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectSelectionValidationStatus.Valid, valid.Status);
        Assert.Equal(DataRightsSubjectSelectionValidationStatus.Stale, stale.Status);
        Assert.Equal(DataRightsSubjectSelectionValidationStatus.NotFound, missing.Status);
    }

    private static DataRightsSubjectDiscoveryRequest Request(
        string tenantId,
        DataRightsSubjectLookup lookup) =>
        new(
            tenantId,
            DataRightsCaseType.StaffRights,
            PropertyId: null,
            lookup,
            DataRightsSubjectDiscoveryLimits.MaxCandidates);

    private static DataRightsSubjectSelectionRequest Selection(
        string tenantId,
        Guid staffMemberId,
        long version) =>
        new(
            tenantId,
            DataRightsCaseType.StaffRights,
            PropertyId: null,
            new(
                StaffDataRightsDiscoveryContributor.Owner,
                StaffDataRightsDiscoveryContributor.ProfileRecordType,
                staffMemberId,
                version));

    private static StaffMember CreateMember(
        string tenantId,
        string displayName,
        string? workEmail,
        string? workPhone,
        string? authSubjectId) =>
        StaffMember.Create(
            Guid.NewGuid(),
            tenantId,
            displayName,
            legalName: "Maya Q. Chen",
            workEmail,
            workPhone,
            employeeNumber: "EMP-42",
            jobTitle: "Manager",
            department: "Operations",
            authSubjectId,
            "user:test",
            Guid.NewGuid(),
            Now).Value;

    private static StaffDbContext CreateDbContext(string tenantId)
    {
        DbContextOptions<StaffDbContext> options =
            new DbContextOptionsBuilder<StaffDbContext>()
                .UseInMemoryDatabase($"staff-data-rights-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext(tenantId));
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
