namespace BunkFy.Modules.Properties.Application.Ports;

public sealed record PropertiesVisibilityScope(bool IncludesAllProperties, IReadOnlyCollection<Guid> PropertyIds)
{
    public static PropertiesVisibilityScope All { get; } = new(true, []);

    public static PropertiesVisibilityScope Restricted(IEnumerable<Guid> propertyIds)
    {
        ArgumentNullException.ThrowIfNull(propertyIds);
        return new(false, propertyIds.Distinct().ToArray());
    }
}
