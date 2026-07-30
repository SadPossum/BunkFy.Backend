namespace BunkFy.Extensions.Operations.Notifications;

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;

internal static class OperationsNotificationsDataRightsExportSchema
{
    public const string ExportSchemaId =
        "operations-notifications.reservation-history-export";
    public const int ExportSchemaVersion = 1;

    private const string CatalogResourceName =
        "BunkFy.Extensions.Operations.Notifications.DataGovernance.personal-data-catalog.v1.json";
    private const string ExportPolicy =
        "include-in-authorized-guest-export";
    private const string ExportRetentionPolicy =
        "operations-notifications-data-rights-export-fragment";

    private static readonly JsonSerializerOptions ValueSerializerOptions =
        CreateSerializerOptions();
    private static readonly Lazy<SchemaState> State =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static DataRightsExportDescriptor Descriptor =>
        State.Value.Descriptor;

    public static void EnsureValid() => _ = State.Value;

    public static DataRightsExportRecord CreateRecord(
        ReservationNotificationHistoryDataRightsExport value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.NotificationId == Guid.Empty ||
            value.StreamSequence <= 0)
        {
            throw new InvalidDataException(
                "The Operations Notifications data-rights export record is invalid.");
        }

        IReadOnlyCollection<(string Member, object? Value)> values =
        [
            (nameof(value.NotificationId), value.NotificationId),
            (nameof(value.SourceModule), value.SourceModule),
            (nameof(value.NotificationName), value.NotificationName),
            (nameof(value.NotificationVersion), value.NotificationVersion),
            (nameof(value.Title), value.Title),
            (nameof(value.Body), value.Body),
            (nameof(value.Severity), value.Severity),
            (nameof(value.StreamSequence), value.StreamSequence),
            (nameof(value.OccurredAtUtc), value.OccurredAtUtc),
            (nameof(value.CreatedAtUtc), value.CreatedAtUtc),
            (nameof(value.Payload), value.Payload),
            (nameof(value.Tags), value.Tags),
            (nameof(value.DeliveryPolicy), value.DeliveryPolicy)
        ];
        DataRightsExportField[] fields = values
            .Select(item => CreateField(item.Member, item.Value))
            .OrderBy(field => field.FieldId, StringComparer.Ordinal)
            .ToArray();
        return new DataRightsExportRecord(
            OperationsNotificationsDataRightsCoordinates
                .NotificationCopyRecordType,
            value.NotificationId,
            value.StreamSequence,
            Array.AsReadOnly(fields));
    }

    private static DataRightsExportField CreateField(
        string member,
        object? value)
    {
        string key = MemberKey(member);
        if (!State.Value.FieldIdsByMember.TryGetValue(
                key,
                out string? fieldId))
        {
            throw new InvalidDataException(
                $"The Operations Notifications data-rights export member '{key}' is not catalogue-approved.");
        }

        JsonElement serialized = JsonSerializer.SerializeToElement(
            value,
            value?.GetType() ?? typeof(object),
            ValueSerializerOptions);
        if (Encoding.UTF8.GetByteCount(serialized.GetRawText()) >
            DataRightsExportLimits.MaxFieldValueBytes)
        {
            throw new InvalidDataException(
                $"The Operations Notifications data-rights export field '{fieldId}' exceeds its size limit.");
        }

        return new DataRightsExportField(fieldId, serialized);
    }

    private static SchemaState Load()
    {
        Assembly assembly =
            typeof(OperationsNotificationsDataRightsExportSchema).Assembly;
        using Stream stream =
            assembly.GetManifestResourceStream(CatalogResourceName) ??
            throw new InvalidDataException(
                "The Operations Notifications personal-data catalogue is unavailable.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        PersonalDataCatalogDocument catalog =
            PersonalDataCatalogJson.Parse(buffer.ToArray());
        if (!string.Equals(
                catalog.CatalogId,
                "operations-notifications.personal-data",
                StringComparison.Ordinal) ||
            !string.Equals(
                catalog.Module,
                OperationsNotificationsDataRightsCoordinates.Owner,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The Operations Notifications personal-data catalogue identity is invalid.");
        }

        Type sourceType =
            typeof(ReservationNotificationHistoryDataRightsExport);
        HashSet<string> expectedMembers = sourceType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => MemberKey(property.Name))
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
                         PersonalDataSurface.DataRightsExport))
            {
                string key = string.Join(
                    '|',
                    binding.Type,
                    binding.Member);
                if (!expectedMembers.Contains(key) ||
                    field.Id.Length >
                    DataRightsExportLimits.FieldIdMaxLength ||
                    !string.Equals(
                        field.AuthoritativeOwner,
                        OperationsNotificationsDataRightsCoordinates.Owner,
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
                        $"The Operations Notifications data-rights export binding '{key}' is invalid.");
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
                $"The Operations Notifications data-rights export catalogue is missing: {string.Join(", ", missing)}.");
        }

        string[] fieldIds = fieldIdsByMember.Values
            .Distinct(StringComparer.Ordinal)
            .OrderBy(fieldId => fieldId, StringComparer.Ordinal)
            .ToArray();
        DataRightsExportDescriptor descriptor = new(
            OperationsNotificationsDataRightsCoordinates.Owner,
            catalog.CatalogId,
            catalog.SchemaVersion,
            catalog.CatalogVersion,
            ExportSchemaId,
            ExportSchemaVersion,
            Array.AsReadOnly(fieldIds));
        return new SchemaState(descriptor, fieldIdsByMember);
    }

    private static string MemberKey(string member) =>
        string.Join(
            '|',
            typeof(ReservationNotificationHistoryDataRightsExport).FullName,
            member);

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options =
            new(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        return options;
    }

    private sealed record SchemaState(
        DataRightsExportDescriptor Descriptor,
        IReadOnlyDictionary<string, string> FieldIdsByMember);
}
