namespace BunkFy.Modules.DataRights.Tests.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.DataRights.Persistence.Repositories;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsResponseDeadlineAlertRepositoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Claim_emits_current_state_once_and_ignores_terminal_cases()
    {
        InMemoryDatabaseRoot root = new();
        string databaseName = $"data-rights-deadline-alerts-{Guid.NewGuid():N}";
        Guid dueSoonId = Guid.NewGuid();
        Guid overdueId = Guid.NewGuid();
        Guid canceledId = Guid.NewGuid();
        await using (DataRightsDbContext seed = CreateDbContext(
                         databaseName,
                         root))
        {
            DataRightsCase dueSoon = CreateCase(
                dueSoonId,
                Guid.NewGuid(),
                Now.AddDays(-1),
                Now.AddHours(24));
            DataRightsCase overdue = CreateCase(
                overdueId,
                Guid.NewGuid(),
                Now.AddDays(-5),
                Now.AddHours(-1));
            DataRightsCase canceled = CreateCase(
                canceledId,
                Guid.NewGuid(),
                Now.AddDays(-1),
                Now.AddHours(12));
            Assert.True(canceled.Cancel(
                canceled.Version,
                "user:privacy",
                Now.AddMinutes(-1)).IsSuccess);
            seed.Cases.AddRange(dueSoon, overdue, canceled);
            await seed.SaveChangesAsync();
        }

        SequenceIdGenerator ids = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        await using DataRightsDbContext dbContext = CreateDbContext(
            databaseName,
            root);
        DataRightsResponseDeadlineAlertRepository repository = new(
            dbContext,
            ids);
        string[] scheduleScopeIds = await repository
            .StreamScheduleScopeIdsAsync(CancellationToken.None)
            .ToArrayAsync(CancellationToken.None);

        Assert.Equal(["tenant-a"], scheduleScopeIds);
        DataRightsResponseDeadlineAlertClaimResult first = await repository
            .ClaimAsync(
                Now,
                Now.AddHours(48),
                batchSize: 100,
                CancellationToken.None);
        await dbContext.SaveChangesAsync();

        Assert.Equal(2, first.ProcessedCount);
        Assert.Collection(
            first.Dispatches,
            dispatch =>
            {
                Assert.Equal(overdueId, dispatch.CaseId);
                Assert.Equal(
                    DataRightsResponseDeadlineAlertKind.Overdue,
                    dispatch.AlertKind);
            },
            dispatch =>
            {
                Assert.Equal(dueSoonId, dispatch.CaseId);
                Assert.Equal(
                    DataRightsResponseDeadlineAlertKind.DueSoon,
                    dispatch.AlertKind);
            });
        Assert.DoesNotContain(
            first.Dispatches,
            dispatch => dispatch.CaseId == canceledId);

        DataRightsResponseDeadlineAlertClaimResult duplicate = await repository
            .ClaimAsync(
                Now,
                Now.AddHours(48),
                batchSize: 100,
                CancellationToken.None);
        Assert.Empty(duplicate.Dispatches);

        DateTimeOffset afterDue = Now.AddHours(25);
        DataRightsResponseDeadlineAlertClaimResult overdueTransition =
            await repository.ClaimAsync(
                afterDue,
                afterDue.AddHours(48),
                batchSize: 100,
                CancellationToken.None);
        await dbContext.SaveChangesAsync();

        DataRightsResponseDeadlineAlertDispatch transitioned = Assert.Single(
            overdueTransition.Dispatches);
        Assert.Equal(dueSoonId, transitioned.CaseId);
        Assert.Equal(
            DataRightsResponseDeadlineAlertKind.Overdue,
            transitioned.AlertKind);
        Assert.Equal(3, await dbContext.ResponseDeadlineAlertDispatches.CountAsync());
    }

    private static DataRightsCase CreateCase(
        Guid caseId,
        Guid propertyId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset dueAtUtc)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.DataSubject).Value;
        DataRightsResponseDeadlinePolicyEvidence evidence =
            DataRightsResponseDeadlinePolicyEvidence.Create(
                propertyId,
                7,
                8,
                "GB",
                "development-hostel-example",
                2,
                new string('a', 64),
                DataRightsResponseRight.Export,
                "development-example",
                0,
                0,
                2,
                "Europe/London",
                new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero),
                createdAtUtc,
                createdAtUtc,
                dueAtUtc).Value;
        return DataRightsCase.Create(
            caseId,
            "tenant-a",
            request,
            "user:privacy",
            createdAtUtc,
            evidence).Value;
    }

    private static DataRightsDbContext CreateDbContext(
        string databaseName,
        InMemoryDatabaseRoot root)
    {
        DbContextOptions<DataRightsDbContext> options =
            new DbContextOptionsBuilder<DataRightsDbContext>()
                .UseInMemoryDatabase(databaseName, root)
                .Options;
        return new(options, new TestScopeContext("tenant-a"));
    }

    private sealed class SequenceIdGenerator(params Guid[] ids) : IIdGenerator
    {
        private int index;
        public Guid NewId() => ids[this.index++];
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
