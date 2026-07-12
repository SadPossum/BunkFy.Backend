namespace BunkFy.Modules.Staff.Tests;

using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Persistence;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffModelTests
{
    [Fact]
    public void Model_has_scoped_unique_correlations_concurrency_and_assignment_constraints()
    {
        using StaffDbContext dbContext = CreateDbContext();
        IEntityType member = dbContext.Model.FindEntityType(typeof(StaffMember))!;
        IEntityType designMember = dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(StaffMember))!;
        IEntityType assignment = dbContext.Model.FindEntityType(typeof(StaffPropertyAssignment))!;
        IEntityType designAssignment = dbContext.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(StaffPropertyAssignment))!;

        Assert.True(member.FindProperty(nameof(StaffMember.Version))!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAdd, member.FindProperty(nameof(StaffMember.ProjectionOrdinal))!.ValueGenerated);
        Assert.Contains(member.GetIndexes(), index => index.IsUnique && index.Properties.Select(item => item.Name)
            .SequenceEqual([nameof(StaffMember.ScopeId), nameof(StaffMember.AuthSubjectId)]));
        Assert.Contains(member.GetIndexes(), index => index.IsUnique && index.Properties.Select(item => item.Name)
            .SequenceEqual([nameof(StaffMember.ScopeId), nameof(StaffMember.EmployeeNumberSearch)]));
        Assert.NotNull(assignment.FindProperty(nameof(StaffPropertyAssignment.ScopeId)));
        Assert.Contains(designMember.GetCheckConstraints(), item => item.Name == "CK_staff_members_lifecycle");
        Assert.Contains(designAssignment.GetCheckConstraints(), item => item.Name == "CK_staff_assignments_lifecycle");
    }

    private static StaffDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<StaffDbContext>().UseInMemoryDatabase($"staff-{Guid.NewGuid():N}").Options,
        new TestScopeContext());

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
