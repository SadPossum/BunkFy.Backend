namespace BunkFy.Extensions.DataRights.AccessControl;

using System.Reflection;
using BunkFy.DataGovernance;

internal static class AccessControlPersonalDataCatalog
{
    private const string ResourceName =
        "BunkFy.Extensions.DataRights.AccessControl.DataGovernance." +
        "personal-data-catalog.v1.json";

    private static readonly Lazy<PersonalDataCatalogDocument> State =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static PersonalDataCatalogDocument Current => State.Value;

    private static PersonalDataCatalogDocument Load()
    {
        Assembly assembly = typeof(AccessControlPersonalDataCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidDataException(
                "The Access Control personal-data catalogue is unavailable.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return PersonalDataCatalogJson.Parse(buffer.ToArray());
    }
}
