namespace BunkFy.Modules.Staff.Tests;

using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Domain.Models;
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
        IEntityType receipt =
            dbContext.Model.FindEntityType(typeof(StaffDataRightsCorrectionReceipt))!;
        IEntityType designReceipt = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(StaffDataRightsCorrectionReceipt))!;

        Assert.True(member.FindProperty(nameof(StaffMember.Version))!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAdd, member.FindProperty(nameof(StaffMember.ProjectionOrdinal))!.ValueGenerated);
        Assert.Contains(member.GetIndexes(), index => index.IsUnique && index.Properties.Select(item => item.Name)
            .SequenceEqual([nameof(StaffMember.ScopeId), nameof(StaffMember.AuthSubjectId)]));
        Assert.Contains(member.GetIndexes(), index => index.IsUnique && index.Properties.Select(item => item.Name)
            .SequenceEqual([nameof(StaffMember.ScopeId), nameof(StaffMember.EmployeeNumberSearch)]));
        Assert.NotNull(assignment.FindProperty(nameof(StaffPropertyAssignment.ScopeId)));
        Assert.Contains(designMember.GetCheckConstraints(), item => item.Name == "CK_staff_members_lifecycle");
        Assert.Contains(designAssignment.GetCheckConstraints(), item => item.Name == "CK_staff_assignments_lifecycle");
        Assert.Equal(
            StaffDataRightsCorrectionReceipt.DigestLength,
            receipt.FindProperty(
                nameof(StaffDataRightsCorrectionReceipt.RequestSha256))!
                .GetMaxLength());
        Assert.Contains(receipt.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(StaffDataRightsCorrectionReceipt.ScopeId),
                nameof(StaffDataRightsCorrectionReceipt.ExecutionId)
            ]));
        Assert.Contains(
            designReceipt.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                    "CK_staff_data_rights_correction_receipts_versions");
    }

    [Fact]
    public async Task Correction_receipts_are_append_only()
    {
        await using StaffDbContext dbContext = CreateDbContext();
        StaffDataRightsCorrectionReceipt receipt =
            StaffDataRightsCorrectionReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                approvalRevision: 2,
                Guid.NewGuid(),
                selectedRecordVersion: 3,
                currentRecordVersion: 4,
                [StaffProfileField.DisplayName],
                new string('a', StaffDataRightsCorrectionReceipt.DigestLength),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new DateTimeOffset(
                    2026,
                    7,
                    28,
                    12,
                    0,
                    0,
                    TimeSpan.Zero)).Value;
        dbContext.DataRightsCorrectionReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();
        dbContext.Entry(receipt).State = EntityState.Modified;

        InvalidOperationException failure =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dbContext.SaveChangesAsync());

        Assert.Contains("append-only", failure.Message);
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
