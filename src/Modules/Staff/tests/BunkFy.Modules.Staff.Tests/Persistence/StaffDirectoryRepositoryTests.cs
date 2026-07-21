namespace BunkFy.Modules.Staff.Tests.Persistence;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Staff.Persistence.Repositories;
using Gma.Framework.Pagination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffDirectoryRepositoryTests
{
    [Fact]
    public async Task Directory_projection_is_current_property_scoped_and_searches_display_name_only()
    {
        await using StaffDbContext dbContext = CreateDbContext();
        Guid firstPropertyId = Guid.NewGuid();
        Guid secondPropertyId = Guid.NewGuid();
        StaffMember member = CreateMember();
        Assign(member, firstPropertyId, isPrimary: true);
        Assign(member, secondPropertyId, isPrimary: false);
        dbContext.StaffMembers.Add(member);
        dbContext.PropertyProjections.AddRange(
            new StaffPropertyProjection("tenant-a", firstPropertyId, "First", PropertyStatus.Active, 1),
            new StaffPropertyProjection("tenant-a", secondPropertyId, "Second", PropertyStatus.Active, 1));
        await dbContext.SaveChangesAsync();
        StaffMemberRepository repository = new(dbContext);

        StaffDirectoryMemberDto tenantDirectory = (await repository.GetDirectoryAsync(
            member.Id,
            CancellationToken.None))!;
        StaffDirectoryMemberDto propertyDirectory = (await repository.GetDirectoryAtPropertyAsync(
            firstPropertyId,
            member.Id,
            CancellationToken.None))!;
        StaffDirectoryListResponse displayNameSearch = await repository.ListDirectoryAsync(
            "Ada",
            status: null,
            new PageRequest(PageRequest.DefaultPage, PageRequest.DefaultPageSize),
            CancellationToken.None);
        StaffDirectoryListResponse emailSearch = await repository.ListDirectoryAsync(
            "private@example.test",
            status: null,
            new PageRequest(PageRequest.DefaultPage, PageRequest.DefaultPageSize),
            CancellationToken.None);

        Assert.Equal(2, tenantDirectory.Assignments.Count);
        Assert.Equal(firstPropertyId, Assert.Single(propertyDirectory.Assignments).PropertyId);
        Assert.Single(displayNameSearch.Items);
        Assert.Empty(emailSearch.Items);
        Assert.DoesNotContain(
            typeof(StaffDirectoryMemberDto).GetProperties(),
            property => property.Name is "LegalName" or "WorkEmail" or "WorkPhone" or
                "EmployeeNumber" or "AuthSubjectId" or "CreatedBy" or "LastChangedBy");
    }

    private static StaffMember CreateMember() => StaffMember.Create(
        Guid.NewGuid(),
        "tenant-a",
        "Ada Operator",
        "Ada Private",
        "private@example.test",
        "+15550100",
        "EMP-PRIVATE",
        "Manager",
        "Operations",
        "auth-private",
        "user:owner",
        Guid.NewGuid(),
        new DateTimeOffset(2026, 7, 21, 9, 0, 0, TimeSpan.Zero)).Value;

    private static void Assign(StaffMember member, Guid propertyId, bool isPrimary) =>
        Assert.True(member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            propertyJobTitle: null,
            isPrimary,
            new DateOnly(2026, 7, 21),
            member.Version,
            "user:owner",
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 21, 9, 0, 0, TimeSpan.Zero)).IsSuccess);

    private static StaffDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<StaffDbContext>()
            .UseInMemoryDatabase($"staff-directory-{Guid.NewGuid():N}")
            .Options,
        new TestScopeContext());

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
