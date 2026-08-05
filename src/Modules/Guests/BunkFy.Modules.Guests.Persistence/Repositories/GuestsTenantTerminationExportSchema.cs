namespace BunkFy.Modules.Guests.Persistence.Repositories;

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Contracts;

internal static class GuestsTenantTerminationExportSchema
{
    private const string CatalogResourceName =
        "BunkFy.Modules.Guests.Persistence.DataGovernance." +
        "personal-data-catalog.v1.json";
    private const string ExportRetentionPolicy =
        "guests-tenant-termination-export-fragment";

    private static readonly Type[] SourceTypes =
    [
        typeof(GuestProfileTenantExport),
        typeof(GuestDataRightsCorrectionReceiptTenantExport),
        typeof(GuestProcessingRestrictionTenantExport),
        typeof(GuestProcessingRestrictionReceiptTenantExport),
        typeof(GuestDataHoldTenantExport),
        typeof(GuestDataHoldReceiptTenantExport),
        typeof(GuestAnonymisationReceiptTenantExport),
        typeof(GuestAnonymisationTombstoneTenantExport),
        typeof(GuestAnonymisationRestoreReceiptTenantExport),
        typeof(GuestRetentionExecutionTenantExport),
        typeof(GuestRetentionAnonymisationReceiptTenantExport)
    ];

    private static readonly SensitiveBinding[] SensitiveBindings =
    [
        Binding<GuestProfileTenantExport>(
            nameof(GuestProfileTenantExport.ProfileState),
            "guests.profile-state",
            "include-in-authorized-guest-or-tenant-export"),
        Staff<GuestProfileTenantExport>(),
        Binding<GuestDataRightsCorrectionReceiptTenantExport>(
            nameof(GuestDataRightsCorrectionReceiptTenantExport
                .DataRightsProof),
            "guests.data-rights-proof",
            "include-minimum-coordinate-in-authorized-case-ledger-or-tenant-export"),
        Restriction<GuestProcessingRestrictionTenantExport>(),
        Staff<GuestProcessingRestrictionTenantExport>(),
        Restriction<GuestProcessingRestrictionReceiptTenantExport>(),
        Staff<GuestProcessingRestrictionReceiptTenantExport>(),
        Hold<GuestDataHoldTenantExport>(),
        Staff<GuestDataHoldTenantExport>(),
        Hold<GuestDataHoldReceiptTenantExport>(),
        Staff<GuestDataHoldReceiptTenantExport>(),
        Anonymisation<GuestAnonymisationReceiptTenantExport>(),
        Staff<GuestAnonymisationReceiptTenantExport>(),
        Anonymisation<GuestAnonymisationTombstoneTenantExport>(),
        Anonymisation<GuestAnonymisationRestoreReceiptTenantExport>(),
        Binding<GuestRetentionExecutionTenantExport>(
            nameof(GuestRetentionExecutionTenantExport.RetentionExecution),
            "guests.retention-execution",
            "include-in-controller-authorized-tenant-export"),
        Binding<GuestRetentionAnonymisationReceiptTenantExport>(
            nameof(GuestRetentionAnonymisationReceiptTenantExport
                .RetentionProof),
            "guests.retention-proof",
            "include-minimum-retention-proof-in-controller-authorized-tenant-export"),
        Staff<GuestRetentionAnonymisationReceiptTenantExport>()
    ];

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
        if (!SourceTypes.Contains(sourceType) ||
            !GuestsTenantTerminationMetadata.RecordTypes.Contains(
                recordType,
                StringComparer.Ordinal) ||
            recordId == Guid.Empty ||
            recordVersion <= 0)
        {
            throw new InvalidDataException(
                "The Guests tenant-export record is invalid.");
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
                "The Guests tenant-export fields are invalid.");
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
        GuestsTenantExportFieldAttribute attribute =
            property.GetCustomAttribute<GuestsTenantExportFieldAttribute>() ??
            throw new InvalidDataException(
                $"Guests tenant-export member '{property.Name}' " +
                "has no field identifier.");
        JsonElement serialized = JsonSerializer.SerializeToElement(
            value,
            value?.GetType() ?? typeof(object),
            SerializerOptions);
        if (Encoding.UTF8.GetByteCount(serialized.GetRawText()) >
            DataRightsExportLimits.MaxFieldValueBytes)
        {
            throw new InvalidDataException(
                $"Guests tenant-export field '{attribute.FieldId}' " +
                "exceeds its size limit.");
        }

        return new DataRightsExportField(attribute.FieldId, serialized);
    }

    private static DataRightsExportDescriptor Load()
    {
        string[] discoveredFieldIds = SourceTypes
            .SelectMany(type => type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public))
            .Select(property => property.GetCustomAttribute<
                GuestsTenantExportFieldAttribute>()?.FieldId ??
                throw new InvalidDataException(
                    $"Guests tenant-export member '{property.Name}' " +
                    "has no field identifier."))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(fieldId => fieldId, StringComparer.Ordinal)
            .ToArray();
        string[] declaredFieldIds = GuestsTenantTerminationMetadata
            .ExportFieldIds
            .OrderBy(fieldId => fieldId, StringComparer.Ordinal)
            .ToArray();
        if (!discoveredFieldIds.SequenceEqual(
                declaredFieldIds,
                StringComparer.Ordinal) ||
            discoveredFieldIds.Length >
                DataRightsExportLimits.MaxFieldsPerRecord ||
            discoveredFieldIds.Any(fieldId =>
                fieldId.Length is <= 0 or >
                    DataRightsExportLimits.FieldIdMaxLength))
        {
            throw new InvalidDataException(
                "The Guests tenant-export field catalogue is invalid.");
        }

        ValidateSensitiveBindings();
        return new DataRightsExportDescriptor(
            GuestsTenantTerminationMetadata.OwnerKey,
            GuestsTenantTerminationMetadata.ExportCatalogId,
            GuestsTenantTerminationMetadata.ExportCatalogSchemaVersion,
            GuestsTenantTerminationMetadata.CatalogVersion,
            GuestsTenantTerminationMetadata.ExportSchemaId,
            GuestsTenantTerminationMetadata.ExportSchemaVersion,
            Array.AsReadOnly(discoveredFieldIds));
    }

    private static void ValidateSensitiveBindings()
    {
        Assembly assembly = typeof(GuestsTenantTerminationExportSchema)
            .Assembly;
        using Stream stream = assembly.GetManifestResourceStream(
                CatalogResourceName) ??
            throw new InvalidDataException(
                "The Guests personal-data catalogue is unavailable.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        PersonalDataCatalogDocument catalog =
            PersonalDataCatalogJson.Parse(buffer.ToArray());
        if (catalog.CatalogVersion !=
                GuestsTenantTerminationMetadata.PersonalDataCatalogVersion ||
            !string.Equals(
                catalog.CatalogId,
                "guests.personal-data",
                StringComparison.Ordinal) ||
            !string.Equals(
                catalog.Module,
                GuestsTenantTerminationMetadata.OwnerKey,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The Guests personal-data catalogue identity is invalid.");
        }

        foreach (SensitiveBinding expected in SensitiveBindings)
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
            PropertyInfo property = expected.Type.GetProperty(
                    expected.Member,
                    BindingFlags.Instance | BindingFlags.Public) ??
                throw new InvalidDataException(
                    "The Guests tenant-export binding member is missing.");
            string? actualFieldId = property.GetCustomAttribute<
                GuestsTenantExportFieldAttribute>()?.FieldId;
            bool valid = string.Equals(
                    actualFieldId,
                    expected.FieldId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    field.AuthoritativeOwner,
                    GuestsTenantTerminationMetadata.OwnerKey,
                    StringComparison.Ordinal) &&
                string.Equals(
                    policy.Export,
                    expected.ExportPolicy,
                    StringComparison.Ordinal) &&
                field.AllowedSurfaces.Contains(
                    PersonalDataSurface.DataRightsExport) &&
                field.AllowedBoundaries.Contains(
                    PersonalDataBoundary.CrossModule) &&
                field.Bindings.Any(binding =>
                    string.Equals(
                        binding.Assembly,
                        assembly.GetName().Name,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        binding.Type,
                        expected.Type.FullName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        binding.Member,
                        expected.Member,
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
                    $"The Guests tenant-export binding " +
                    $"'{expected.Type.FullName}.{expected.Member}' " +
                    "is invalid.");
            }
        }
    }

    private static SensitiveBinding Staff<T>() =>
        Binding<T>(
            "StaffAttribution",
            "guests.staff-attribution",
            "include-in-authorized-staff-or-tenant-export");

    private static SensitiveBinding Restriction<T>() =>
        Binding<T>(
            "ProcessingRestriction",
            "guests.processing-restriction",
            "include-current-state-in-authorized-guest-or-tenant-export");

    private static SensitiveBinding Hold<T>() =>
        Binding<T>(
            "DataHold",
            "guests.data-hold",
            "include-minimum-coordinate-in-authorized-case-ledger-or-tenant-export");

    private static SensitiveBinding Anonymisation<T>() =>
        Binding<T>(
            "AnonymisationProof",
            "guests.anonymisation-proof",
            "include-minimum-proof-in-authorized-case-ledger-or-tenant-export");

    private static SensitiveBinding Binding<T>(
        string member,
        string fieldId,
        string exportPolicy) =>
        new(typeof(T), member, fieldId, exportPolicy);

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options =
            new(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        return options;
    }

    private sealed record SensitiveBinding(
        Type Type,
        string Member,
        string FieldId,
        string ExportPolicy);
}
