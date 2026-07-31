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
            [TenantTerminationContributionPhase.Unknown],
            [],
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

    private static StubContributor Stub(
        string ownerKey,
        IReadOnlyCollection<string>? dependencies = null,
        bool mandatory = true,
        IReadOnlyCollection<TenantTerminationContributionPhase>? phases = null) =>
        new(new(
            ownerKey,
            TenantTerminationContract.CurrentVersion,
            phases ?? [TenantTerminationContributionPhase.Destroy],
            dependencies ?? [],
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
