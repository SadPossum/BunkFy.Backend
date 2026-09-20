namespace BunkFy.Modules.Stations.Persistence;

using Gma.Framework.Domain;
/// <summary>Future termination owner closes this under the same mutation lock; no termination service is registered in P1.</summary>
internal sealed class StationsTenantLifecycleState : IScopedEntity
{
    private StationsTenantLifecycleState() { }
    public StationsTenantLifecycleState(string scopeId) => this.ScopeId = scopeId;
    public string ScopeId { get; private set; } = "";
    public bool Closed { get; private set; }
    public long Revision { get; private set; } = 1;
}
