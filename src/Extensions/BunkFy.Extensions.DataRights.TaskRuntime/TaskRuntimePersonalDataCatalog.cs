namespace BunkFy.Extensions.DataRights.TaskRuntime;

using System.Reflection;
using BunkFy.DataGovernance;

internal static class TaskRuntimePersonalDataCatalog
{
    private const string ResourceName =
        "BunkFy.Extensions.DataRights.TaskRuntime.DataGovernance." +
        "personal-data-catalog.v1.json";

    private static readonly Lazy<PersonalDataCatalogDocument> State =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static PersonalDataCatalogDocument Current => State.Value;

    private static PersonalDataCatalogDocument Load()
    {
        Assembly assembly = typeof(TaskRuntimePersonalDataCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidDataException(
                "The Task Runtime personal-data catalogue is unavailable.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return PersonalDataCatalogJson.Parse(buffer.ToArray());
    }
}
