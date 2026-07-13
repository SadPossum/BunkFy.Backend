namespace BunkFy.AppHost.Composition;

public sealed record BunkFyBackendProjectPaths(
    string Api,
    string AdminApi,
    string Worker)
{
    public BunkFyBackendProjectPaths Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(this.Api);
        ArgumentException.ThrowIfNullOrWhiteSpace(this.AdminApi);
        ArgumentException.ThrowIfNullOrWhiteSpace(this.Worker);
        return this;
    }
}
