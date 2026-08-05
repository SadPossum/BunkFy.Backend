namespace BunkFy.Host.Migrations.Tests;

public sealed class BunkFyMigrationPlanTests
{
    [Fact]
    public void Compatible_partial_history_produces_a_resumable_plan()
    {
        BunkFyMigrationPlan plan = BunkFyMigrationPlan.Create(
        [
            History("first", ["001", "002"], ["001"]),
            History("second", ["010"], [])
        ]);

        Assert.Equal(3, plan.TargetMigrationCount);
        Assert.Equal(1, plan.AppliedMigrationCount);
        Assert.Equal(2, plan.PendingMigrationCount);
        Assert.Equal(["002"], plan.Modules[0].PendingMigrations);
        Assert.Equal(["010"], plan.Modules[1].PendingMigrations);
        Assert.Matches("^[a-f0-9]{64}$", plan.TargetCatalogSha256);
        Assert.Matches("^[a-f0-9]{64}$", plan.CurrentStateSha256);
        Assert.Matches("^[a-f0-9]{64}$", plan.PendingPlanSha256);
    }

    [Fact]
    public void Target_digest_is_stable_across_forward_progress()
    {
        BunkFyMigrationPlan before = BunkFyMigrationPlan.Create(
        [
            History("first", ["001", "002"], ["001"]),
            History("second", ["010"], [])
        ]);
        BunkFyMigrationPlan after = BunkFyMigrationPlan.Create(
        [
            History("first", ["001", "002"], ["001", "002"]),
            History("second", ["010"], [])
        ]);

        Assert.Equal(before.TargetCatalogSha256, after.TargetCatalogSha256);
        Assert.NotEqual(before.CurrentStateSha256, after.CurrentStateSha256);
        Assert.NotEqual(before.PendingPlanSha256, after.PendingPlanSha256);
    }

    [Fact]
    public void Module_or_migration_order_changes_the_target_digest()
    {
        BunkFyMigrationPlan baseline = BunkFyMigrationPlan.Create(
        [
            History("first", ["001", "002"], []),
            History("second", ["010"], [])
        ]);
        BunkFyMigrationPlan moduleOrderChanged = BunkFyMigrationPlan.Create(
        [
            History("second", ["010"], []),
            History("first", ["001", "002"], [])
        ]);
        BunkFyMigrationPlan migrationOrderChanged = BunkFyMigrationPlan.Create(
        [
            History("first", ["002", "001"], []),
            History("second", ["010"], [])
        ]);

        Assert.NotEqual(
            baseline.TargetCatalogSha256,
            moduleOrderChanged.TargetCatalogSha256);
        Assert.NotEqual(
            baseline.TargetCatalogSha256,
            migrationOrderChanged.TargetCatalogSha256);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("reordered")]
    [InlineData("ahead")]
    public void Unknown_or_reordered_history_fails_before_apply(string shape)
    {
        BunkFyMigrationHistory history = shape switch
        {
            "unknown" => History(
                "module",
                ["001", "002"],
                ["001", "999"]),
            "reordered" => History("module", ["001", "002"], ["002"]),
            "ahead" => History("module", ["001"], ["001", "002"]),
            _ => throw new InvalidOperationException("Unknown test shape.")
        };
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => BunkFyMigrationPlan.Create([history]));

        Assert.Contains("exact prefix", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_module_registration_fails_closed()
    {
        Assert.Throws<InvalidOperationException>(
            () => BunkFyMigrationPlan.Create(
            [
                History("same", ["001"], []),
                History("same", ["002"], [])
            ]));
    }

    private static BunkFyMigrationHistory History(
        string module,
        IReadOnlyList<string> target,
        IReadOnlyList<string> applied) =>
        new(module, target, applied);
}
