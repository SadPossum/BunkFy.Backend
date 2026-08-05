namespace BunkFy.Extensions.DataRights.Organizations;

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;

internal static class OrganizationsTenantTerminationExportSchema
{
    private const string CatalogId = "organizations.personal-data";
    private const string ExportPolicy =
        "include-in-authorized-tenant-portability-export";
    private const string ExportRetentionPolicy =
        "organizations-tenant-termination-export-fragment";

    private static readonly IReadOnlyDictionary<Type, string> RecordTypes =
        new Dictionary<Type, string>
        {
            [typeof(OrganizationTenantExport)] =
                OrganizationsTenantTerminationMetadata.OrganizationRecordType,
            [typeof(OrganizationMembershipTenantExport)] =
                OrganizationsTenantTerminationMetadata.MembershipRecordType,
            [typeof(OrganizationInvitationTenantExport)] =
                OrganizationsTenantTerminationMetadata.InvitationRecordType,
            [typeof(OrganizationEnrollmentLinkTenantExport)] =
                OrganizationsTenantTerminationMetadata.EnrollmentLinkRecordType,
            [typeof(OrganizationEnrollmentClaimTenantExport)] =
                OrganizationsTenantTerminationMetadata.EnrollmentClaimRecordType
        };

    private static readonly HashSet<string> AuthOwnedFieldIds =
        new(StringComparer.Ordinal)
        {
            "organizations.accepted-subject-id",
            "organizations.created-by",
            "organizations.creator-subject-id",
            "organizations.inviter-subject-id",
            "organizations.last-changed-by",
            "organizations.subject-id"
        };

    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();
    private static readonly Lazy<DataRightsExportDescriptor> DescriptorState =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static DataRightsExportDescriptor Descriptor =>
        DescriptorState.Value;

    public static void EnsureValid() => _ = DescriptorState.Value;

    public static DataRightsExportRecord CreateRecord(
        string recordType,
        Guid recordId,
        long recordVersion,
        object source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Type sourceType = source.GetType();
        if (!RecordTypes.TryGetValue(sourceType, out string? expectedType) ||
            !string.Equals(recordType, expectedType, StringComparison.Ordinal) ||
            recordId == Guid.Empty ||
            recordVersion <= 0)
        {
            throw new InvalidDataException(
                "The Organizations tenant-export record is invalid.");
        }

        DataRightsExportField[] fields = sourceType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => CreateField(
                property,
                property.GetValue(source)))
            .OrderBy(field => field.FieldId, StringComparer.Ordinal)
            .ToArray();
        if (fields.Length is <= 0 or >
                DataRightsExportLimits.MaxFieldsPerRecord ||
            fields.Select(field => field.FieldId)
                .Distinct(StringComparer.Ordinal).Count() != fields.Length)
        {
            throw new InvalidDataException(
                "The Organizations tenant-export fields are invalid.");
        }

        return new DataRightsExportRecord(
            recordType,
            recordId,
            recordVersion,
            Array.AsReadOnly(fields));
    }

    private static DataRightsExportField CreateField(
        PropertyInfo property,
        object? value)
    {
        string fieldId = property.GetCustomAttribute<
                OrganizationsTenantExportFieldAttribute>()?.FieldId ??
            throw new InvalidDataException(
                $"Organizations tenant-export member '{property.Name}' " +
                "has no field identifier.");
        JsonElement serialized = JsonSerializer.SerializeToElement(
            value,
            value?.GetType() ?? typeof(object),
            SerializerOptions);
        if (Encoding.UTF8.GetByteCount(serialized.GetRawText()) >
            DataRightsExportLimits.MaxFieldValueBytes)
        {
            throw new InvalidDataException(
                $"Organizations tenant-export field '{fieldId}' exceeds " +
                "its size limit.");
        }

        return new DataRightsExportField(fieldId, serialized);
    }

    private static DataRightsExportDescriptor Load()
    {
        ExportBinding[] bindings = RecordTypes.Keys
            .SelectMany(type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => new ExportBinding(
                    type,
                    property,
                    property.GetCustomAttribute<
                            OrganizationsTenantExportFieldAttribute>()
                        ?.FieldId ?? throw new InvalidDataException(
                            $"Organizations tenant-export member " +
                            $"'{type.FullName}.{property.Name}' has no field " +
                            "identifier."))))
            .ToArray();
        string[] discoveredFieldIds = bindings
            .Select(binding => binding.FieldId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(fieldId => fieldId, StringComparer.Ordinal)
            .ToArray();
        string[] declaredFieldIds =
            OrganizationsTenantTerminationMetadata.ExportFieldIds
                .OrderBy(fieldId => fieldId, StringComparer.Ordinal)
                .ToArray();
        if (!discoveredFieldIds.SequenceEqual(
                declaredFieldIds,
                StringComparer.Ordinal) ||
            discoveredFieldIds.Any(fieldId =>
                fieldId.Length is <= 0 or >
                    DataRightsExportLimits.FieldIdMaxLength) ||
            !RecordTypes.Values.Order(StringComparer.Ordinal).SequenceEqual(
                OrganizationsTenantTerminationMetadata.RecordTypes
                    .Order(StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "The Organizations tenant-export catalogue is invalid.");
        }

        PersonalDataCatalogDocument catalog =
            OrganizationsPersonalDataCatalog.Current;
        if (catalog.CatalogVersion !=
                OrganizationsTenantTerminationMetadata
                    .PersonalDataCatalogVersion ||
            !string.Equals(
                catalog.CatalogId,
                CatalogId,
                StringComparison.Ordinal) ||
            !string.Equals(
                catalog.Module,
                OrganizationsTenantTerminationMetadata.OwnerKey,
                StringComparison.Ordinal) ||
            !catalog.Fields.Select(field => field.Id)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(discoveredFieldIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "The Organizations personal-data catalogue identity is invalid.");
        }

        foreach (ExportBinding expected in bindings)
        {
            PersonalDataFieldDefinition field = catalog.Fields.Single(
                candidate => string.Equals(
                    candidate.Id,
                    expected.FieldId,
                    StringComparison.Ordinal));
            PersonalDataRightsPolicy policy = catalog.RightsPolicies.Single(
                candidate => string.Equals(
                    candidate.Id,
                    field.RightsPolicy,
                    StringComparison.Ordinal));
            string authoritativeOwner = AuthOwnedFieldIds.Contains(
                expected.FieldId)
                ? "auth"
                : OrganizationsTenantTerminationMetadata.OwnerKey;
            bool valid = string.Equals(
                    field.AuthoritativeOwner,
                    authoritativeOwner,
                    StringComparison.Ordinal) &&
                string.Equals(
                    policy.Export,
                    ExportPolicy,
                    StringComparison.Ordinal) &&
                field.AllowedSurfaces.Contains(
                    PersonalDataSurface.DataRightsExport) &&
                field.AllowedBoundaries.Contains(
                    PersonalDataBoundary.CrossModule) &&
                field.Bindings.Any(binding =>
                    string.Equals(
                        binding.Assembly,
                        expected.Type.Assembly.GetName().Name,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        binding.Type,
                        expected.Type.FullName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        binding.Member,
                        expected.Property.Name,
                        StringComparison.Ordinal) &&
                    binding.Surface ==
                        PersonalDataSurface.DataRightsExport &&
                    string.Equals(
                        binding.RetentionPolicy,
                        ExportRetentionPolicy,
                        StringComparison.Ordinal));
            if (!valid)
            {
                throw new InvalidDataException(
                    $"The Organizations tenant-export binding " +
                    $"'{expected.Type.FullName}.{expected.Property.Name}' " +
                    "is invalid.");
            }
        }

        return new DataRightsExportDescriptor(
            OrganizationsTenantTerminationMetadata.OwnerKey,
            OrganizationsTenantTerminationMetadata.ExportCatalogId,
            OrganizationsTenantTerminationMetadata.ExportCatalogSchemaVersion,
            OrganizationsTenantTerminationMetadata.CatalogVersion,
            OrganizationsTenantTerminationMetadata.ExportSchemaId,
            OrganizationsTenantTerminationMetadata.ExportSchemaVersion,
            Array.AsReadOnly(discoveredFieldIds));
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        return options;
    }

    private sealed record ExportBinding(
        Type Type,
        PropertyInfo Property,
        string FieldId);
}
