namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.Domain;

public sealed class IngestionPropertyProjection : IScopedEntity
{
    public const int NameMaxLength = 200;
    public const int CodeMaxLength = 64;

    private IngestionPropertyProjection() { }

    private IngestionPropertyProjection(Guid id, string scopeId)
    {
        this.Id = id;
        this.ScopeId = scopeId;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public string? Name { get; private set; }
    public string? Code { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsKnown { get; private set; }
    public long SourceVersion { get; private set; }
    public long RetentionFenceVersion { get; private set; }

    internal static IngestionPropertyProjection Create(Guid propertyId, string scopeId) =>
        new(propertyId, scopeId);

    internal void Apply(string? name, string? code, bool isActive, long sourceVersion)
    {
        if (sourceVersion <= this.SourceVersion)
        {
            return;
        }

        this.Name = string.IsNullOrWhiteSpace(name) ? this.Name : name.Trim();
        this.Code = string.IsNullOrWhiteSpace(code) ? this.Code : code.Trim();
        this.IsActive = isActive;
        this.IsKnown = true;
        this.SourceVersion = sourceVersion;
    }

    internal void AdvanceRetentionFence() => this.RetentionFenceVersion++;
}
