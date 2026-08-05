namespace BunkFy.Extensions.DataRights.Organizations;

using System.Reflection;
using BunkFy.DataGovernance;

internal static class OrganizationsPersonalDataCatalog
{
    private const string ResourceName =
        "BunkFy.Extensions.DataRights.Organizations.DataGovernance." +
        "personal-data-catalog.v1.json";

    private static readonly Lazy<PersonalDataCatalogDocument> State =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static PersonalDataCatalogDocument Current => State.Value;

    private static PersonalDataCatalogDocument Load()
    {
        Assembly assembly = typeof(OrganizationsPersonalDataCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidDataException(
                "The Organizations personal-data catalogue is unavailable.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return PersonalDataCatalogJson.Parse(buffer.ToArray());
    }
}
