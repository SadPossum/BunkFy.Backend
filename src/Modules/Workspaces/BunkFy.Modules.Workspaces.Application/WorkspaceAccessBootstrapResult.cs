namespace BunkFy.Modules.Workspaces.Application;

public sealed record WorkspaceAccessBootstrapResult(
    int SeedVersion,
    int SeedProfileCount,
    int MigratedMemberCount);

public sealed record WorkspaceAccessBootstrapStatus(
    int SeedVersion,
    int ExpectedSeedProfileCount,
    int ActiveSeedProfileCount,
    int DriftedSeedProfileCount,
    int ArchivedSeedProfileCount,
    int LegacyMemberCount,
    int MarkerMemberCount)
{
    public bool RequiresBackfill =>
        this.ActiveSeedProfileCount != this.ExpectedSeedProfileCount ||
        this.DriftedSeedProfileCount != 0 ||
        this.ArchivedSeedProfileCount != 0 ||
        this.LegacyMemberCount != 0;
}
