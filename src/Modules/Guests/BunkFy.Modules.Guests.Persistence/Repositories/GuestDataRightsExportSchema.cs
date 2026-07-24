namespace BunkFy.Modules.Guests.Persistence.Repositories;

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;

internal static class GuestDataRightsExportSchema
{
    public const string ExportSchemaId = "guests.subject-export";
    public const int ExportSchemaVersion = 1;

    private const string CatalogResourceName =
        "BunkFy.Modules.Guests.Persistence.DataGovernance.personal-data-catalog.v1.json";
    private const string ExportPolicy = "include-in-authorized-guest-export";
    private const string ExportRetentionPolicy = "guest-data-rights-export-fragment";

    private static readonly JsonSerializerOptions ValueSerializerOptions = CreateSerializerOptions();
    private static readonly Lazy<SchemaState> State = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static DataRightsExportDescriptor Descriptor => State.Value.Descriptor;

    public static void EnsureValid() => _ = State.Value;

    public static DataRightsExportRecord CreateProfileRecord(
        GuestProfileDataRightsExport profile) =>
        CreateRecord(
            GuestDataRightsDiscoveryContributor.ProfileRecordType,
            profile.GuestId,
            profile.Version,
            typeof(GuestProfileDataRightsExport),
            [
                (nameof(profile.GuestId), profile.GuestId),
                (nameof(profile.OriginPropertyId), profile.OriginPropertyId),
                (nameof(profile.DisplayName), profile.DisplayName),
                (nameof(profile.LegalName), profile.LegalName),
                (nameof(profile.Email), profile.Email),
                (nameof(profile.Phone), profile.Phone),
                (nameof(profile.DateOfBirth), profile.DateOfBirth),
                (nameof(profile.NationalityCountryCode), profile.NationalityCountryCode),
                (nameof(profile.PreferredLanguageTag), profile.PreferredLanguageTag),
                (nameof(profile.Notes), profile.Notes),
                (nameof(profile.Status), profile.Status),
                (nameof(profile.Version), profile.Version),
                (nameof(profile.CreatedAtUtc), profile.CreatedAtUtc),
                (nameof(profile.LastChangedAtUtc), profile.LastChangedAtUtc),
                (nameof(profile.ArchivedAtUtc), profile.ArchivedAtUtc)
            ]);

    public static DataRightsExportRecord CreateStayRecord(
        GuestStayDataRightsExport stay) =>
        CreateRecord(
            GuestDataRightsExportContributor.StayRecordType,
            stay.ReservationId,
            stay.ReservationVersion,
            typeof(GuestStayDataRightsExport),
            [
                (nameof(stay.ReservationId), stay.ReservationId),
                (nameof(stay.PropertyId), stay.PropertyId),
                (nameof(stay.Role), stay.Role),
                (nameof(stay.Arrival), stay.Arrival),
                (nameof(stay.Departure), stay.Departure),
                (nameof(stay.Status), stay.Status),
                (nameof(stay.CheckedInBusinessDate), stay.CheckedInBusinessDate),
                (nameof(stay.NoShowBusinessDate), stay.NoShowBusinessDate),
                (nameof(stay.CheckedOutBusinessDate), stay.CheckedOutBusinessDate),
                (nameof(stay.IsCurrentParticipant), stay.IsCurrentParticipant),
                (nameof(stay.ReservationVersion), stay.ReservationVersion)
            ]);

    private static DataRightsExportRecord CreateRecord(
        string recordType,
        Guid recordId,
        long recordVersion,
        Type sourceType,
        IReadOnlyCollection<(string Member, object? Value)> values)
    {
        if (recordId == Guid.Empty ||
            recordVersion <= 0 ||
            string.IsNullOrWhiteSpace(recordType) ||
            recordType.Length > DataRightsExportLimits.RecordTypeMaxLength ||
            values.Count is <= 0 or > DataRightsExportLimits.MaxFieldsPerRecord)
        {
            throw new InvalidDataException("The Guests data-rights export record is invalid.");
        }

        DataRightsExportField[] fields = values
            .Select(value => CreateField(sourceType, value.Member, value.Value))
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
        if (!State.Value.FieldIdsByMember.TryGetValue(key, out string? fieldId))
        {
            throw new InvalidDataException(
                $"The Guests data-rights export member '{key}' is not catalogue-approved.");
        }

        JsonElement serialized = JsonSerializer.SerializeToElement(
            value,
            value?.GetType() ?? typeof(object),
            ValueSerializerOptions);
        if (Encoding.UTF8.GetByteCount(serialized.GetRawText()) >
            DataRightsExportLimits.MaxFieldValueBytes)
        {
            throw new InvalidDataException(
                $"The Guests data-rights export field '{fieldId}' exceeds its size limit.");
        }

        return new DataRightsExportField(fieldId, serialized);
    }

    private static SchemaState Load()
    {
        Assembly assembly = typeof(GuestDataRightsExportSchema).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(CatalogResourceName) ??
            throw new InvalidDataException("The Guests personal-data catalogue is unavailable.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        PersonalDataCatalogDocument catalog = PersonalDataCatalogJson.Parse(buffer.ToArray());
        if (!string.Equals(catalog.CatalogId, "guests.personal-data", StringComparison.Ordinal) ||
            !string.Equals(catalog.Module, GuestDataRightsDiscoveryContributor.Owner, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Guests personal-data catalogue identity is invalid.");
        }

        Type[] sourceTypes =
        [
            typeof(GuestProfileDataRightsExport),
            typeof(GuestStayDataRightsExport)
        ];
        HashSet<string> expectedMembers = sourceTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => MemberKey(type, property.Name)))
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> fieldIdsByMember = new(StringComparer.Ordinal);

        foreach (PersonalDataFieldDefinition field in catalog.Fields)
        {
            PersonalDataRightsPolicy rightsPolicy = catalog.RightsPolicies.Single(
                policy => string.Equals(policy.Id, field.RightsPolicy, StringComparison.Ordinal));
            foreach (PersonalDataMemberBinding binding in field.Bindings.Where(
                         binding => binding.Surface == PersonalDataSurface.DataRightsExport))
            {
                string key = string.Join('|', binding.Type, binding.Member);
                if (!expectedMembers.Contains(key) ||
                    field.Id.Length > DataRightsExportLimits.FieldIdMaxLength ||
                    !string.Equals(field.AuthoritativeOwner, GuestDataRightsDiscoveryContributor.Owner, StringComparison.Ordinal) ||
                    !string.Equals(rightsPolicy.Export, ExportPolicy, StringComparison.Ordinal) ||
                    !field.AllowedBoundaries.Contains(PersonalDataBoundary.CrossModule) ||
                    !string.Equals(binding.RetentionPolicy, ExportRetentionPolicy, StringComparison.Ordinal) ||
                    !fieldIdsByMember.TryAdd(key, field.Id))
                {
                    throw new InvalidDataException(
                        $"The Guests data-rights export binding '{key}' is invalid.");
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
                $"The Guests data-rights export catalogue is missing: {string.Join(", ", missing)}.");
        }

        string[] fieldIds = fieldIdsByMember.Values
            .Distinct(StringComparer.Ordinal)
            .OrderBy(fieldId => fieldId, StringComparer.Ordinal)
            .ToArray();
        DataRightsExportDescriptor descriptor = new(
            GuestDataRightsDiscoveryContributor.Owner,
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
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        return options;
    }

    private sealed record SchemaState(
        DataRightsExportDescriptor Descriptor,
        IReadOnlyDictionary<string, string> FieldIdsByMember);
}
