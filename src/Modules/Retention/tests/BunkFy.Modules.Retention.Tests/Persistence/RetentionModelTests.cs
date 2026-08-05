namespace BunkFy.Modules.Retention.Tests.Persistence;

using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Domain.Aggregates;
using BunkFy.Modules.Retention.Domain.Models;
using BunkFy.Modules.Retention.Persistence;
using BunkFy.Modules.Retention.Persistence.Repositories;
using BunkFy.Modules.Retention.Persistence.TenantTermination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RetentionModelTests
{
    [Fact]
    public void Tenant_owned_state_and_revision_are_scope_filtered()
    {
        using RetentionDbContext context = CreateContext();

        AssertScopeFiltered<RetentionExecution>(context);
        AssertScopeFiltered<RetentionScheduleState>(context);
        AssertScopeFiltered<RetentionTenantRevision>(context);
        AssertScopeFiltered<RetentionTenantDestroyOperation>(context);
        AssertScopeFiltered<RetentionTenantDestroyReceipt>(context);
    }

    [Fact]
    public void Tenant_revision_is_scope_keyed_and_concurrency_guarded()
    {
        using RetentionDbContext context = CreateContext();
        IEntityType revision = context.Model.FindEntityType(
            typeof(RetentionTenantRevision))!;
        IEntityType designRevision = context
            .GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(RetentionTenantRevision))!;

        Assert.Equal(
            [nameof(RetentionTenantRevision.ScopeId)],
            revision.FindPrimaryKey()!.Properties.Select(
                property => property.Name));
        Assert.True(
            revision.FindProperty(nameof(RetentionTenantRevision.Revision))!
                .IsConcurrencyToken);
        Assert.Contains(
            designRevision.GetCheckConstraints(),
            constraint => string.Equals(
                constraint.Name,
                "CK_retention_tenant_revision_positive",
                StringComparison.Ordinal));
        Assert.Contains(
            designRevision.GetCheckConstraints(),
            constraint => string.Equals(
                constraint.Name,
                "CK_retention_tenant_revision_lifecycle",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Tenant_destruction_progress_is_bounded_and_receipt_is_unique_per_scope()
    {
        using RetentionDbContext context = CreateContext();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IEntityType operation = model.FindEntityType(
            typeof(RetentionTenantDestroyOperation))!;
        IEntityType receipt = model.FindEntityType(
            typeof(RetentionTenantDestroyReceipt))!;

        Assert.Contains(
            operation.GetCheckConstraints(),
            constraint => string.Equals(
                constraint.Name,
                "CK_retention_tenant_destroy_operation_batch",
                StringComparison.Ordinal));
        Assert.Contains(
            receipt.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(
                property => property.Name).SequenceEqual(
                    [nameof(RetentionTenantDestroyReceipt.ScopeId)]));
    }

    [Fact]
    public async Task Health_projection_exposes_the_exact_task_run_coordinate()
    {
        await using RetentionDbContext context = CreateContext();
        Guid runId = Guid.NewGuid();
        DateTimeOffset startedAtUtc =
            new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
        RetentionExecution execution = RetentionExecution.Start(
            runId,
            "tenant-a",
            "retention",
            "execution-history",
            RetentionExecutionTargetKind.Tenant,
            propertyId: null,
            executionPolicyVersion: 1,
            attempt: 1,
            startedAtUtc,
            startedAtUtc.AddMinutes(5)).Value;
        context.ScheduleStates.Add(new(
            execution,
            startedAtUtc.AddHours(1)));
        await context.SaveChangesAsync();

        RetentionScheduleStateSnapshot snapshot = Assert.Single(
            await new RetentionScheduleStateRepository(context)
                .ListAsync(CancellationToken.None));

        Assert.Equal(runId, snapshot.LastExecutionId);
    }

    private static void AssertScopeFiltered<TEntity>(
        RetentionDbContext context)
    {
        IEntityType entity = context.Model.FindEntityType(typeof(TEntity))!;
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());
    }

    private static RetentionDbContext CreateContext()
    {
        DbContextOptions<RetentionDbContext> options =
            new DbContextOptionsBuilder<RetentionDbContext>()
                .UseInMemoryDatabase(
                    $"retention-model-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
