namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;

internal static class InventoryDataRightsExportSchema
{
    public const string ExportSchemaId = "inventory.subject-export";
    public const int ExportSchemaVersion = 1;

    private const string CatalogResourceName =
        "BunkFy.Modules.Inventory.Persistence.DataGovernance." +
        "personal-data-catalog.v1.json";
    private const string ExportPolicy =
        "include-in-authorized-reservation-or-tenant-export";
    private const string ExportRetentionPolicy =
        "inventory-data-rights-export-fragment";

    private static readonly Type[] SourceTypes =
    [
        typeof(InventoryAllocationDataRightsExport),
        typeof(InventoryAllocationAmendmentDecisionDataRightsExport)
    ];

    private static readonly JsonSerializerOptions ValueSerializerOptions =
        CreateSerializerOptions();
    private static readonly Lazy<SchemaState> State =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static DataRightsExportDescriptor Descriptor =>
        State.Value.Descriptor;

    public static void EnsureValid() => _ = State.Value;

    public static DataRightsExportRecord CreateRecord(
        string recordType,
        Guid recordId,
        long recordVersion,
        object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Type sourceType = source.GetType();
        if (!SourceTypes.Contains(sourceType) ||
            recordId == Guid.Empty ||
            recordVersion <= 0 ||
            string.IsNullOrWhiteSpace(recordType) ||
            recordType.Length >
                DataRightsExportLimits.RecordTypeMaxLength)
        {
            throw new InvalidDataException(
                "The Inventory data-rights export record is invalid.");
        }

        PropertyInfo[] properties = sourceType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);
        if (properties.Length is <= 0 or >
            DataRightsExportLimits.MaxFieldsPerRecord)
        {
            throw new InvalidDataException(
                "The Inventory export field count is invalid.");
        }

        DataRightsExportField[] fields = properties
            .Select(property => CreateField(
                sourceType,
                property.Name,
                property.GetValue(source)))
            .OrderBy(field => field.FieldId, StringComparer.Ordinal)
            .ToArray();
        return new DataRightsExportRecord(
            recordType,
            recordId,
            recordVersion,
            Array.AsReadOnly(fields));
    }

    private static DataRightsExportField CreateField(
        Type sourceType,
        string member,
        object? value)
    {
        string key = MemberKey(sourceType, member);
        if (!State.Value.FieldIdsByMember.TryGetValue(
                key,
                out string? fieldId))
        {
            throw new InvalidDataException(
                $"The Inventory data-rights export member '{key}' " +
                "is not catalogue-approved.");
        }

        JsonElement serialized = JsonSerializer.SerializeToElement(
            value,
            value?.GetType() ?? typeof(object),
            ValueSerializerOptions);
        if (Encoding.UTF8.GetByteCount(serialized.GetRawText()) >
            DataRightsExportLimits.MaxFieldValueBytes)
        {
            throw new InvalidDataException(
                $"The Inventory data-rights export field '{fieldId}' " +
                "exceeds its size limit.");
        }

        return new DataRightsExportField(fieldId, serialized);
    }

    private static SchemaState Load()
    {
        Assembly assembly = typeof(InventoryDataRightsExportSchema).Assembly;
        using Stream stream =
            assembly.GetManifestResourceStream(CatalogResourceName) ??
            throw new InvalidDataException(
                "The Inventory personal-data catalogue is unavailable.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        PersonalDataCatalogDocument catalog =
            PersonalDataCatalogJson.Parse(buffer.ToArray());
        if (!string.Equals(
                catalog.CatalogId,
                "inventory.personal-data",
                StringComparison.Ordinal) ||
            !string.Equals(
                catalog.Module,
                InventoryDataRightsDiscoveryContributor.Owner,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The Inventory personal-data catalogue identity is invalid.");
        }

        HashSet<string> expectedMembers = SourceTypes
            .SelectMany(type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => MemberKey(type, property.Name)))
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> fieldIdsByMember =
            new(StringComparer.Ordinal);

        foreach (PersonalDataFieldDefinition field in catalog.Fields)
        {
            PersonalDataRightsPolicy rightsPolicy =
                catalog.RightsPolicies.Single(policy =>
                    string.Equals(
                        policy.Id,
                        field.RightsPolicy,
                        StringComparison.Ordinal));
            foreach (PersonalDataMemberBinding binding in
                     field.Bindings.Where(binding =>
                         binding.Surface ==
                         PersonalDataSurface.DataRightsExport &&
                         expectedMembers.Contains(string.Join(
                             '|',
                             binding.Type,
                             binding.Member))))
            {
                string key = string.Join(
                    '|',
                    binding.Type,
                    binding.Member);
                if (field.Id.Length >
                        DataRightsExportLimits.FieldIdMaxLength ||
                    !string.Equals(
                        field.AuthoritativeOwner,
                        InventoryDataRightsDiscoveryContributor.Owner,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        rightsPolicy.Export,
                        ExportPolicy,
                        StringComparison.Ordinal) ||
                    !field.AllowedBoundaries.Contains(
                        PersonalDataBoundary.CrossModule) ||
                    !string.Equals(
                        binding.RetentionPolicy,
                        ExportRetentionPolicy,
                        StringComparison.Ordinal) ||
                    !fieldIdsByMember.TryAdd(key, field.Id))
                {
                    throw new InvalidDataException(
                        $"The Inventory data-rights export binding " +
                        $"'{key}' is invalid.");
                }
            }
        }

        string[] missing = expectedMembers
            .Except(fieldIdsByMember.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException(
                "The Inventory data-rights export catalogue is missing: " +
                string.Join(", ", missing) +
                ".");
        }

        string[] fieldIds = fieldIdsByMember.Values
            .Distinct(StringComparer.Ordinal)
            .OrderBy(fieldId => fieldId, StringComparer.Ordinal)
            .ToArray();
        DataRightsExportDescriptor descriptor = new(
            InventoryDataRightsDiscoveryContributor.Owner,
            catalog.CatalogId,
            catalog.SchemaVersion,
            catalog.CatalogVersion,
            ExportSchemaId,
            ExportSchemaVersion,
            Array.AsReadOnly(fieldIds));
        return new SchemaState(descriptor, fieldIdsByMember);
    }

    private static string MemberKey(Type sourceType, string member) =>
        string.Join('|', sourceType.FullName, member);

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options =
            new(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.KebabCaseLower));
        return options;
    }

    private sealed record SchemaState(
        DataRightsExportDescriptor Descriptor,
        IReadOnlyDictionary<string, string> FieldIdsByMember);
}
