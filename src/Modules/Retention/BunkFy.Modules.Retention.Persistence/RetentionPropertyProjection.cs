namespace BunkFy.Modules.Retention.Persistence;

using Gma.Framework.Domain;

public sealed class RetentionPropertyProjection : IScopedEntity
{
    private RetentionPropertyProjection() { }

    public RetentionPropertyProjection(
        string scopeId,
        Guid id,
        bool isActive,
        long topologySourceVersion)
    {
        this.ScopeId = scopeId;
        this.Id = id;
        this.IsKnown = topologySourceVersion > 0;
        this.IsActive = isActive;
        this.TopologySourceVersion = topologySourceVersion;
    }

    public string ScopeId { get; private set; } = string.Empty;
    public Guid Id { get; private set; }
    public bool IsKnown { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsProcessingEnabled { get; private set; }
    public int? RetentionPolicyVersion { get; private set; }
    public long TopologySourceVersion { get; private set; }
    public long PolicySourceVersion { get; private set; }
    public bool IsSchedulable =>
        this.IsKnown &&
        this.IsActive &&
        this.IsProcessingEnabled &&
        this.RetentionPolicyVersion > 0;

    public void ApplyTopology(bool isActive, long sourceVersion)
    {
        if (sourceVersion <= this.TopologySourceVersion)
        {
            return;
        }

        this.IsKnown = true;
        this.IsActive = isActive;
        this.TopologySourceVersion = sourceVersion;
    }

    public void ApplyPolicy(
        bool isProcessingEnabled,
        int retentionPolicyVersion,
        long sourceVersion)
    {
        if (sourceVersion <= this.PolicySourceVersion)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retentionPolicyVersion);

        this.IsKnown = true;
        this.IsProcessingEnabled = isProcessingEnabled;
        this.RetentionPolicyVersion = retentionPolicyVersion;
        this.PolicySourceVersion = sourceVersion;
    }
}
