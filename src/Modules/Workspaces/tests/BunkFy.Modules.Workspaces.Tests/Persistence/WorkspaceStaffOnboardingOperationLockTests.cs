namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Persistence;
using BunkFy.Modules.Workspaces.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffOnboardingOperationLockTests
{
    [Fact]
    public async Task Non_relational_application_lock_reports_exact_existence()
    {
        await using WorkspacesDbContext dbContext = CreateDbContext();
        WorkspaceStaffOnboarding application = CreateApplication();
        dbContext.StaffOnboardingApplications.Add(application);
        await dbContext.SaveChangesAsync();
        WorkspaceStaffOnboardingOperationLock operationLock = new(dbContext);

        bool existing = await operationLock.TryAcquireAsync(
            application.Id,
            CancellationToken.None);
        bool missing = await operationLock.TryAcquireAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(existing);
        Assert.False(missing);
    }

    [Fact]
    public async Task Valid_non_relational_source_and_applicant_coordinates_are_no_ops()
    {
        await using WorkspacesDbContext dbContext = CreateDbContext();
        WorkspaceStaffOnboarding application = CreateApplication();
        WorkspaceStaffOnboardingOperationLock operationLock = new(dbContext);

        await operationLock.AcquireSourceReadAsync(
            application.SourceId,
            CancellationToken.None);
        await operationLock.AcquireSourceWriteAsync(
            application.SourceId,
            CancellationToken.None);
        await operationLock.AcquireApplicantAsync(
            application.SourceId,
            application.SubjectId,
            CancellationToken.None);
    }

    [Fact]
    public async Task Invalid_coordinates_fail_closed()
    {
        await using WorkspacesDbContext dbContext = CreateDbContext();
        WorkspaceStaffOnboardingOperationLock operationLock = new(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            operationLock.AcquireSourceReadAsync(
                Guid.Empty,
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            operationLock.AcquireSourceWriteAsync(
                Guid.Empty,
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            operationLock.AcquireApplicantAsync(
                Guid.NewGuid(),
                " ",
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            operationLock.TryAcquireAsync(
                Guid.Empty,
                CancellationToken.None));
    }

    private static WorkspacesDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options,
        new TestScopeContext());

    private static WorkspaceStaffOnboarding CreateApplication() =>
        WorkspaceStaffOnboarding.Create(
            Guid.NewGuid(),
            "tenant-a",
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),
            "applicant@example.test",
            "Applicant",
            legalName: null,
            workEmail: null,
            workPhone: null,
            employeeNumber: null,
            jobTitle: null,
            department: null,
            new DateTimeOffset(2026, 8, 6, 14, 0, 0, TimeSpan.Zero)).Value;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
