namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Production;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationProductionCatalogTests
{
    private static readonly string Digest = new('a', 64);

    [Fact]
    public void Exact_topology_has_stable_evidence_and_terminal_owner()
    {
        StubContributor reservations = Owner(
            "reservations",
            new(TenantTerminationContributionPhase.Export, []),
            new(TenantTerminationContributionPhase.Destroy, []));
        StubContributor taskRuntime = Owner(
            "task-runtime",
            new TenantTerminationContributorPhasePlan(
                TenantTerminationContributionPhase.Destroy,
                ["reservations"],
                TenantTerminationExecutionBoundary.GlobalControlTask));
        StubContributor workspaces = Owner(
            "workspaces",
            new(TenantTerminationContributionPhase.Freeze, []),
            new(TenantTerminationContributionPhase.Export, []),
            new(
                TenantTerminationContributionPhase.Destroy,
                ["task-runtime"],
                TenantTerminationExecutionBoundary.GlobalControlTask),
            new(TenantTerminationContributionPhase.Restore, []));
        StubExportContributor reservationExport = Export("reservations");
        StubExportContributor workspaceExport = Export("workspaces");

        TenantTerminationProductionCatalog first = new(
            [reservations, taskRuntime, workspaces],
            [reservationExport, workspaceExport]);
        TenantTerminationProductionCatalog reordered = new(
            [workspaces, reservations, taskRuntime],
            [workspaceExport, reservationExport]);
        Result<TenantTerminationProductionCatalogEvidence> result =
            first.Validate(["reservations", "task-runtime", "workspaces"]);
        Result<TenantTerminationProductionCatalogEvidence> second =
            reordered.Validate(
                ["workspaces", "reservations", "task-runtime"]);

        Assert.True(result.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(3, result.Value.OwnerCount);
        Assert.Equal(2, result.Value.ExportOwnerCount);
        Assert.Equal("workspaces", result.Value.TerminalOwnerKey);
        Assert.Equal(result.Value.CatalogSha256, second.Value.CatalogSha256);
        Assert.Matches("^[0-9a-f]{64}$", result.Value.CatalogSha256);
    }

    [Fact]
    public void Missing_export_or_ambiguous_terminal_owner_fails_closed()
    {
        StubContributor workspaces = Owner(
            "workspaces",
            new(TenantTerminationContributionPhase.Freeze, []),
            new(TenantTerminationContributionPhase.Export, []),
            new(TenantTerminationContributionPhase.Destroy, []),
            new(TenantTerminationContributionPhase.Restore, []));
        StubContributor reservations = Owner(
            "reservations",
            new(TenantTerminationContributionPhase.Export, []),
            new(TenantTerminationContributionPhase.Destroy, []));

        Assert.True(new TenantTerminationProductionCatalog(
            [workspaces],
            []).Validate(["workspaces"]).IsFailure);
        Assert.True(new TenantTerminationProductionCatalog(
            [workspaces, reservations],
            [Export("workspaces"), Export("reservations")]).Validate(
                ["workspaces", "reservations"]).IsFailure);
    }

    private static StubContributor Owner(
        string ownerKey,
        params TenantTerminationContributorPhasePlan[] plans) =>
        new(new(
            ownerKey,
            TenantTerminationContract.CurrentVersion,
            plans,
            MandatoryForProduction: true,
            CatalogVersion: 1,
            CatalogSha256: Digest));

    private static StubExportContributor Export(string ownerKey) =>
        new(new(
            ownerKey,
            $"{ownerKey}.catalog",
            CatalogSchemaVersion: 1,
            CatalogVersion: 1,
            $"{ownerKey}.export",
            ExportSchemaVersion: 1,
            ["record.id"]));

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

    private sealed class StubExportContributor(
        DataRightsExportDescriptor descriptor)
        : ITenantTerminationExportContributor
    {
        public DataRightsExportDescriptor ExportDescriptor { get; } =
            descriptor;

        public Task<TenantTerminationContributionResult> ExportAsync(
            TenantTerminationExportRequest request,
            IDataRightsExportSink sink,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
