namespace BunkFy.Extensions.Operations.Notifications;

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;

internal sealed class
    OperationsNotificationsDataRightsExportSchemaDefinition<TValue>
{
    private static readonly JsonSerializerOptions ValueSerializerOptions =
        CreateSerializerOptions();

    private readonly string exportSchemaId;
    private readonly int exportSchemaVersion;
    private readonly string exportPolicy;
    private readonly string exportRetentionPolicy;
    private readonly Lazy<SchemaState> state;

    public OperationsNotificationsDataRightsExportSchemaDefinition(
        string exportSchemaId,
        int exportSchemaVersion,
        string exportPolicy,
        string exportRetentionPolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportSchemaId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            exportSchemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportPolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            exportRetentionPolicy);
        this.exportSchemaId = exportSchemaId;
        this.exportSchemaVersion = exportSchemaVersion;
        this.exportPolicy = exportPolicy;
        this.exportRetentionPolicy = exportRetentionPolicy;
        this.state = new Lazy<SchemaState>(
            this.Load,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public DataRightsExportDescriptor Descriptor =>
        this.state.Value.Descriptor;

    public void EnsureValid() => _ = this.state.Value;

    public DataRightsExportRecord CreateRecord(
        Guid notificationId,
        long streamSequence,
        IReadOnlyCollection<(string Member, object? Value)> values)
    {
        if (notificationId == Guid.Empty || streamSequence <= 0)
        {
            throw new InvalidDataException(
                "The Operations Notifications data-rights export record is invalid.");
        }

        ArgumentNullException.ThrowIfNull(values);
        DataRightsExportField[] fields = values
            .Select(item => this.CreateField(
                item.Member,
                item.Value))
            .OrderBy(field => field.FieldId, StringComparer.Ordinal)
            .ToArray();
        if (fields.Length != this.state.Value.FieldIdsByMember.Count)
        {
            throw new InvalidDataException(
                "The Operations Notifications data-rights export record is incomplete.");
        }

        return new DataRightsExportRecord(
            OperationsNotificationsDataRightsCoordinates
                .NotificationCopyRecordType,
            notificationId,
            streamSequence,
            Array.AsReadOnly(fields));
    }

    private DataRightsExportField CreateField(
        string member,
        object? value)
    {
        string key = MemberKey(member);
        if (!this.state.Value.FieldIdsByMember.TryGetValue(
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

    private SchemaState Load()
    {
        PersonalDataCatalogDocument catalog =
            OperationsNotificationsPersonalDataCatalog.Current.Document;

        Type sourceType = typeof(TValue);
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
                if (!expectedMembers.Contains(key))
                {
                    continue;
                }

                if (field.Id.Length >
                        DataRightsExportLimits.FieldIdMaxLength ||
                    !string.Equals(
                        field.AuthoritativeOwner,
                        OperationsNotificationsDataRightsCoordinates.Owner,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        rightsPolicy.Export,
                        this.exportPolicy,
                        StringComparison.Ordinal) ||
                    !field.AllowedBoundaries.Contains(
                        PersonalDataBoundary.CrossModule) ||
                    !string.Equals(
                        binding.RetentionPolicy,
                        this.exportRetentionPolicy,
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
            this.exportSchemaId,
            this.exportSchemaVersion,
            Array.AsReadOnly(fieldIds));
        return new SchemaState(descriptor, fieldIdsByMember);
    }

    private static string MemberKey(string member) =>
        string.Join('|', typeof(TValue).FullName, member);

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
