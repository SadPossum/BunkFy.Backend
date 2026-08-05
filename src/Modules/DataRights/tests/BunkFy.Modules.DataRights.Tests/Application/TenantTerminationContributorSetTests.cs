namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationContributorSetTests
{
    private static readonly string Digest = new('a', 64);

    [Fact]
    public void Contributors_are_ordered_by_dependencies_then_stable_owner_key()
    {
        ITenantTerminationContributor[] contributors =
        [
            Stub("reservations", ["workspaces"]),
            Stub("workspaces"),
            Stub("guests", ["workspaces"]),
            Stub("retention", ["guests", "reservations"])
        ];

        Result<IReadOnlyList<ITenantTerminationContributor>> result =
            TenantTerminationContributorSet.OrderForPhase(
                contributors,
                TenantTerminationContributionPhase.Destroy);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["workspaces", "guests", "reservations", "retention"],
            result.Value.Select(item => item.Descriptor.OwnerKey));
    }

    [Fact]
    public void Each_phase_uses_its_own_dependency_graph()
    {
        ITenantTerminationContributor[] contributors =
        [
            StubWithPlans(
                "properties",
                new(TenantTerminationContributionPhase.Export, []),
                new(TenantTerminationContributionPhase.Destroy, ["inventory"])),
            StubWithPlans(
                "inventory",
                new(TenantTerminationContributionPhase.Export, ["properties"]),
                new(TenantTerminationContributionPhase.Destroy, []))
        ];

        Result<IReadOnlyList<ITenantTerminationContributor>> export =
            TenantTerminationContributorSet.OrderForPhase(
                contributors,
                TenantTerminationContributionPhase.Export);
        Result<IReadOnlyList<ITenantTerminationContributor>> destroy =
            TenantTerminationContributorSet.OrderForPhase(
                contributors,
                TenantTerminationContributionPhase.Destroy);

        Assert.True(export.IsSuccess);
        Assert.Equal(
            ["properties", "inventory"],
            export.Value.Select(item => item.Descriptor.OwnerKey));
        Assert.True(destroy.IsSuccess);
        Assert.Equal(
            ["inventory", "properties"],
            destroy.Value.Select(item => item.Descriptor.OwnerKey));
    }

    [Fact]
    public void Missing_dependency_duplicate_owner_and_cycle_fail_closed()
    {
        ITenantTerminationContributor[][] invalidSets =
        [
            [Stub("guests", ["missing"])],
            [Stub("guests"), Stub("guests")],
            [Stub("guests", ["reservations"]), Stub("reservations", ["guests"])]
        ];

        Assert.All(invalidSets, contributors =>
        {
            Result<IReadOnlyList<ITenantTerminationContributor>> result =
                TenantTerminationContributorSet.OrderForPhase(
                    contributors,
                    TenantTerminationContributionPhase.Destroy);
            Assert.True(result.IsFailure);
            Assert.Equal(
                DataRightsApplicationErrors
                    .TenantTerminationContributorCatalogInvalid,
                result.Error);
        });
    }

    [Fact]
    public void Dependency_that_does_not_support_the_same_phase_fails_closed()
    {
        ITenantTerminationContributor[] contributors =
        [
            StubWithPlans(
                "workspaces",
                new TenantTerminationContributorPhasePlan(
                    TenantTerminationContributionPhase.Freeze,
                    [])),
            StubWithPlans(
                "reservations",
                new TenantTerminationContributorPhasePlan(
                    TenantTerminationContributionPhase.Destroy,
                    ["workspaces"]))
        ];

        Result<IReadOnlyList<ITenantTerminationContributor>> result =
            TenantTerminationContributorSet.OrderForPhase(
                contributors,
                TenantTerminationContributionPhase.Destroy);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors
                .TenantTerminationContributorCatalogInvalid,
            result.Error);
    }

    [Fact]
    public void Cycle_in_another_declared_phase_invalidates_the_catalogue()
    {
        ITenantTerminationContributor[] contributors =
        [
            StubWithPlans(
                "inventory",
                new(TenantTerminationContributionPhase.Export, []),
                new(
                    TenantTerminationContributionPhase.Destroy,
                    ["properties"])),
            StubWithPlans(
                "properties",
                new(TenantTerminationContributionPhase.Export, ["inventory"]),
                new(
                    TenantTerminationContributionPhase.Destroy,
                    ["inventory"]))
        ];

        Assert.True(TenantTerminationContributorSet.OrderForPhase(
            contributors,
            TenantTerminationContributionPhase.Export).IsFailure);
        Assert.True(TenantTerminationContributorSet.ValidateProductionCatalog(
            contributors,
            ["inventory", "properties"]).IsFailure);
    }

    [Fact]
    public void Production_catalog_requires_exact_declared_mandatory_owner_set()
    {
        ITenantTerminationContributor[] contributors =
        [
            Stub("workspaces", mandatory: true),
            Stub("guests", ["workspaces"], mandatory: true),
            Stub("diagnostics", mandatory: false)
        ];

        Assert.True(TenantTerminationContributorSet.ValidateProductionCatalog(
            contributors,
            ["guests", "workspaces"]).IsSuccess);
        Assert.True(TenantTerminationContributorSet.ValidateProductionCatalog(
            contributors,
            ["workspaces"]).IsFailure);
        Assert.True(TenantTerminationContributorSet.ValidateProductionCatalog(
            contributors,
            ["guests", "workspaces", "diagnostics"]).IsFailure);
    }

    [Fact]
    public void Unsupported_phase_or_malformed_catalog_metadata_fails_closed()
    {
        StubContributor malformed = new(new(
            "guests",
            TenantTerminationContract.CurrentVersion,
            [new(TenantTerminationContributionPhase.Unknown, [])],
            MandatoryForProduction: true,
            CatalogVersion: 1,
            CatalogSha256: Digest));

        Assert.True(TenantTerminationContributorSet.OrderForPhase(
            [malformed],
            TenantTerminationContributionPhase.Destroy).IsFailure);
        Assert.True(TenantTerminationContributorSet.OrderForPhase(
            [Stub(
                "guests",
                phases: [TenantTerminationContributionPhase.Destroy])],
            TenantTerminationContributionPhase.Freeze).IsFailure);
    }

    [Fact]
    public void Global_control_execution_is_limited_to_destruction()
    {
        StubContributor malformed = StubWithPlans(
            "workspaces",
            new TenantTerminationContributorPhasePlan(
                TenantTerminationContributionPhase.Freeze,
                [],
                TenantTerminationExecutionBoundary.GlobalControlTask));

        Assert.True(TenantTerminationContributorSet.OrderForPhase(
            [malformed],
            TenantTerminationContributionPhase.Freeze).IsFailure);
    }

    private static StubContributor Stub(
        string ownerKey,
        IReadOnlyCollection<string>? dependencies = null,
        bool mandatory = true,
        IReadOnlyCollection<TenantTerminationContributionPhase>? phases = null) =>
        StubWithPlansAndMandatory(
            ownerKey,
            mandatory,
            (phases ?? [TenantTerminationContributionPhase.Destroy])
                .Select(phase => new TenantTerminationContributorPhasePlan(
                    phase,
                    dependencies ?? []))
                .ToArray());

    private static StubContributor StubWithPlans(
        string ownerKey,
        params TenantTerminationContributorPhasePlan[] phasePlans) =>
        StubWithPlansAndMandatory(ownerKey, mandatory: true, phasePlans);

    private static StubContributor StubWithPlansAndMandatory(
        string ownerKey,
        bool mandatory,
        params TenantTerminationContributorPhasePlan[] phasePlans) =>
        new(new(
            ownerKey,
            TenantTerminationContract.CurrentVersion,
            phasePlans,
            mandatory,
            CatalogVersion: 1,
            CatalogSha256: Digest));

    private sealed class StubContributor(
        TenantTerminationContributorDescriptor descriptor)
        : ITenantTerminationContributor
    {
        public TenantTerminationContributorDescriptor Descriptor { get; } =
            descriptor;

        public Task<TenantTerminationContributionResult> ExecuteAsync(
            TenantTerminationContributionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
