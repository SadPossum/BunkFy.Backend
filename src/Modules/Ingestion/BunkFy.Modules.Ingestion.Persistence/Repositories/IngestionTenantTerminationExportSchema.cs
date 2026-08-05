namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Contracts;

internal static class IngestionTenantTerminationExportSchema
{
    private const string CatalogResourceName =
        "BunkFy.Modules.Ingestion.Persistence.DataGovernance." +
        "personal-data-catalog.v1.json";
    private const string ExportRetentionPolicy =
        "ingestion-tenant-termination-export-fragment";

    private static readonly Type[] SourceTypes =
    [
        typeof(IngestionAdapterConnectionTenantExport),
        typeof(IngestionAdapterCredentialTenantExport),
        typeof(IngestionAdapterIngressControlTenantExport),
        typeof(IngestionRunTenantExport),
        typeof(IngestionObservationReceiptTenantExport),
        typeof(IngestionReprocessingAttemptTenantExport),
        typeof(IngestionReprocessingOutputTenantExport),
        typeof(IngestionChangeProposalTenantExport),
        typeof(IngestionReservationSourceLinkTenantExport),
        typeof(IngestionReservationDispatchTenantExport),
        typeof(IngestionLegalHoldTenantExport),
        typeof(IngestionRetentionExecutionTenantExport),
        typeof(IngestionAnonymisationReceiptTenantExport),
        typeof(IngestionAnonymisationTombstoneTenantExport),
        typeof(IngestionLargeTextChunkTenantExport)
    ];

    private static readonly SensitiveBinding[] SensitiveBindings =
    [
        Binding<IngestionAdapterConnectionTenantExport>(
            nameof(IngestionAdapterConnectionTenantExport.Connection),
            "ingestion.tenant.adapter-connection",
            "include-with-authorized-export"),
        Binding<IngestionAdapterCredentialTenantExport>(
            nameof(IngestionAdapterCredentialTenantExport
                .CredentialMetadata),
            "ingestion.tenant.adapter-credential-metadata",
            "include-non-secret-metadata-in-controller-authorized-tenant-export"),
        Binding<IngestionAdapterIngressControlTenantExport>(
            nameof(IngestionAdapterIngressControlTenantExport.IngressControl),
            "ingestion.tenant.adapter-ingress-control",
            "include-with-authorized-operational-audit-or-tenant-export"),
        Binding<IngestionRunTenantExport>(
            nameof(IngestionRunTenantExport.Run),
            "ingestion.tenant.run",
            "include-with-authorized-export"),
        Binding<IngestionObservationReceiptTenantExport>(
            nameof(IngestionObservationReceiptTenantExport
                .ObservationEvidence),
            "ingestion.tenant.observation-evidence",
            "include-through-authorized-evidence-export"),
        Binding<IngestionReprocessingAttemptTenantExport>(
            nameof(IngestionReprocessingAttemptTenantExport
                .ReprocessingAttempt),
            "ingestion.tenant.reprocessing-attempt",
            "include-through-authorized-evidence-export"),
        Binding<IngestionReprocessingOutputTenantExport>(
            nameof(IngestionReprocessingOutputTenantExport
                .ReprocessingOutput),
            "ingestion.tenant.reprocessing-output",
            "include-through-authorized-evidence-export"),
        Binding<IngestionChangeProposalTenantExport>(
            nameof(IngestionChangeProposalTenantExport.ChangeProposal),
            "ingestion.tenant.change-proposal",
            "include-with-authorized-export"),
        Binding<IngestionReservationSourceLinkTenantExport>(
            nameof(IngestionReservationSourceLinkTenantExport.SourceLink),
            "ingestion.tenant.reservation-source-link",
            "include-with-authorized-export"),
        Binding<IngestionReservationDispatchTenantExport>(
            nameof(IngestionReservationDispatchTenantExport.Dispatch),
            "ingestion.tenant.reservation-dispatch",
            "include-with-authorized-export"),
        Binding<IngestionLegalHoldTenantExport>(
            nameof(IngestionLegalHoldTenantExport.LegalHold),
            "ingestion.tenant.legal-hold",
            "include-with-authorized-export"),
        Binding<IngestionRetentionExecutionTenantExport>(
            nameof(IngestionRetentionExecutionTenantExport
                .RetentionExecution),
            "ingestion.tenant.retention-execution",
            "include-with-authorized-export"),
        Binding<IngestionAnonymisationReceiptTenantExport>(
            nameof(IngestionAnonymisationReceiptTenantExport
                .AnonymisationProof),
            "ingestion.tenant.anonymisation-proof",
            "include-minimum-owner-proof-in-controller-authorized-tenant-export-excluding-plans-and-keyed-fingerprints"),
        Binding<IngestionAnonymisationTombstoneTenantExport>(
            nameof(IngestionAnonymisationTombstoneTenantExport
                .AnonymisationProof),
            "ingestion.tenant.anonymisation-proof",
            "include-minimum-owner-proof-in-controller-authorized-tenant-export-excluding-plans-and-keyed-fingerprints"),
        Binding<IngestionLargeTextChunkTenantExport>(
            nameof(IngestionLargeTextChunkTenantExport.LargeTextMetadata),
            "ingestion.tenant.large-text-metadata",
            "include-with-authorized-export"),
        Binding<IngestionLargeTextChunkTenantExport>(
            nameof(IngestionLargeTextChunkTenantExport.Content),
            "ingestion.tenant.large-text-content",
            "include-with-authorized-export")
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
            !IngestionTenantTerminationMetadata.RecordTypes.Contains(
                recordType,
                StringComparer.Ordinal) ||
            recordId == Guid.Empty ||
            recordVersion <= 0)
        {
            throw new InvalidDataException(
                "The Ingestion tenant-export record is invalid.");
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
                "The Ingestion tenant-export fields are invalid.");
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
        IngestionTenantExportFieldAttribute attribute =
            property.GetCustomAttribute<
                IngestionTenantExportFieldAttribute>() ??
            throw new InvalidDataException(
                $"Ingestion tenant-export member '{property.Name}' " +
                "has no field identifier.");
        JsonElement serialized = JsonSerializer.SerializeToElement(
            value,
            value?.GetType() ?? typeof(object),
            SerializerOptions);
        if (Encoding.UTF8.GetByteCount(serialized.GetRawText()) >
            DataRightsExportLimits.MaxFieldValueBytes)
        {
            throw new InvalidDataException(
                $"Ingestion tenant-export field '{attribute.FieldId}' " +
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
                IngestionTenantExportFieldAttribute>()?.FieldId ??
                throw new InvalidDataException(
                    $"Ingestion tenant-export member '{property.Name}' " +
                    "has no field identifier."))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(fieldId => fieldId, StringComparer.Ordinal)
            .ToArray();
        string[] declaredFieldIds = IngestionTenantTerminationMetadata
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
                "The Ingestion tenant-export field catalogue is invalid.");
        }

        ValidateSensitiveBindings();
        return new DataRightsExportDescriptor(
            IngestionTenantTerminationMetadata.OwnerKey,
            IngestionTenantTerminationMetadata.ExportCatalogId,
            IngestionTenantTerminationMetadata.ExportCatalogSchemaVersion,
            IngestionTenantTerminationMetadata.CatalogVersion,
            IngestionTenantTerminationMetadata.ExportSchemaId,
            IngestionTenantTerminationMetadata.ExportSchemaVersion,
            Array.AsReadOnly(discoveredFieldIds));
    }

    private static void ValidateSensitiveBindings()
    {
        Assembly assembly = typeof(IngestionTenantTerminationExportSchema)
            .Assembly;
        using Stream stream = assembly.GetManifestResourceStream(
                CatalogResourceName) ??
            throw new InvalidDataException(
                "The Ingestion personal-data catalogue is unavailable.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        PersonalDataCatalogDocument catalog =
            PersonalDataCatalogJson.Parse(buffer.ToArray());
        if (catalog.CatalogVersion !=
                IngestionTenantTerminationMetadata.PersonalDataCatalogVersion ||
            !string.Equals(
                catalog.CatalogId,
                "ingestion.personal-data",
                StringComparison.Ordinal) ||
            !string.Equals(
                catalog.Module,
                IngestionTenantTerminationMetadata.OwnerKey,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The Ingestion personal-data catalogue identity is invalid.");
        }

        ValidateExportBindings(catalog, assembly);
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
                    "The Ingestion tenant-export binding member is missing.");
            string? actualFieldId = property.GetCustomAttribute<
                IngestionTenantExportFieldAttribute>()?.FieldId;
            bool valid = string.Equals(
                    actualFieldId,
                    expected.FieldId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    field.AuthoritativeOwner,
                    IngestionTenantTerminationMetadata.OwnerKey,
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
                    $"The Ingestion tenant-export binding " +
                    $"'{expected.Type.FullName}.{expected.Member}' " +
                    "is invalid.");
            }
        }
    }

    private static void ValidateExportBindings(
        PersonalDataCatalogDocument catalog,
        Assembly assembly)
    {
        foreach (Type sourceType in SourceTypes)
        {
            foreach (PropertyInfo property in sourceType.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public))
            {
                string fieldId = property.GetCustomAttribute<
                        IngestionTenantExportFieldAttribute>()?.FieldId ??
                    throw new InvalidDataException(
                        $"Ingestion tenant-export member " +
                        $"'{sourceType.FullName}.{property.Name}' has no " +
                        "field identifier.");
                PersonalDataFieldDefinition field = catalog.Fields.Single(
                    candidate => string.Equals(
                        candidate.Id,
                        fieldId,
                        StringComparison.Ordinal));
                bool valid = string.Equals(
                        field.AuthoritativeOwner,
                        IngestionTenantTerminationMetadata.OwnerKey,
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
                            sourceType.FullName,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            binding.Member,
                            property.Name,
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
                        $"The Ingestion tenant-export binding " +
                        $"'{sourceType.FullName}.{property.Name}' is " +
                        "invalid.");
                }
            }
        }
    }

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
