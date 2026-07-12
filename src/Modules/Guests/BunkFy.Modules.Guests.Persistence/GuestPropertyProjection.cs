namespace BunkFy.Modules.Guests.Persistence;

using BunkFy.Modules.Properties.Contracts;

public sealed class GuestPropertyProjection
{
    private GuestPropertyProjection() { }

    public GuestPropertyProjection(
        string scopeId,
        Guid id,
        string? name,
        PropertyStatus status,
        long version)
    {
        this.ScopeId = scopeId;
        this.Id = id;
        this.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        this.Status = status;
        this.Version = version;
    }

    public string ScopeId { get; private set; } = string.Empty;
    public Guid Id { get; private set; }
    public string? Name { get; private set; }
    public PropertyStatus Status { get; private set; }
    public long Version { get; private set; }

    public void Apply(string? name, PropertyStatus status, long version)
    {
        if (version <= this.Version)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            this.Name = name.Trim();
        }

        this.Status = status;
        this.Version = version;
    }
}
