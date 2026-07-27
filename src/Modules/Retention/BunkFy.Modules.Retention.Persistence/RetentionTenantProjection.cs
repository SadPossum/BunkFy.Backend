namespace BunkFy.Modules.Retention.Persistence;

using Gma.Framework.Domain;

public sealed class RetentionTenantProjection : IScopedEntity
{
    private RetentionTenantProjection() { }

    public RetentionTenantProjection(
        string scopeId,
        Guid organizationId,
        bool isActive,
        long sourceVersion)
    {
        this.ScopeId = scopeId;
        this.OrganizationId = organizationId;
        this.IsActive = isActive;
        this.SourceVersion = sourceVersion;
    }

    public string ScopeId { get; private set; } = string.Empty;
    public Guid OrganizationId { get; private set; }
    public bool IsActive { get; private set; }
    public long SourceVersion { get; private set; }

    public void Apply(Guid organizationId, bool isActive, long sourceVersion)
    {
        if (sourceVersion <= this.SourceVersion)
        {
            return;
        }

        if (organizationId != this.OrganizationId)
        {
            throw new InvalidOperationException(
                "Retention.OrganizationCoordinateConflict");
        }

        this.IsActive = isActive;
        this.SourceVersion = sourceVersion;
    }
}
