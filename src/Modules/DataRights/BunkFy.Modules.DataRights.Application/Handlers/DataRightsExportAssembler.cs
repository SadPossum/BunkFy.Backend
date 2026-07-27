namespace BunkFy.Modules.DataRights.Application.Handlers;

using System.Text;
using System.Text.Json;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;

internal sealed class DataRightsExportAssembler(
    IEnumerable<IDataRightsSubjectExportContributor> contributors)
    : IDataRightsExportAssembler
{
    public const int FormatVersion = 1;
    public const int MaximumRecords = 50_000;

    public async Task<DataRightsExportAssemblyResult> AssembleAsync(
        DataRightsExportGenerationRequest request,
        Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "The export destination must be writable.",
                nameof(destination));
        }

        DataRightsSubjectCoordinate[] subjects = ValidateAndOrder(request);
        IReadOnlyDictionary<string, IDataRightsSubjectExportContributor> byOwner =
            this.ResolveContributors(request.CaseType, subjects);

        using Utf8JsonWriter writer = new(destination, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        });
        writer.WriteStartObject();
        writer.WriteString("format", "bunkfy.data-rights.export");
        writer.WriteNumber("formatVersion", FormatVersion);
        writer.WriteString("caseType", CaseType(request.CaseType));
        writer.WriteString(
            "scopeType",
            request.PropertyId.HasValue ? "property" : "tenant");
        writer.WriteNumber("decisionRevision", request.DecisionRevision);
        writer.WriteString("generatedAtUtc", request.GeneratedAtUtc);
        writer.WriteString("expiresAtUtc", request.ExpiresAtUtc);
        writer.WriteStartArray("subjects");

        int totalRecords = 0;
        foreach (DataRightsSubjectCoordinate subject in subjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IDataRightsSubjectExportContributor contributor =
                byOwner[subject.OwnerKey.Trim().ToLowerInvariant()];
            DataRightsExportDescriptor descriptor =
                ValidateDescriptor(contributor, subject.OwnerKey);

            writer.WriteStartObject();
            WriteCoordinate(writer, subject);
            WriteDescriptor(writer, descriptor);
            writer.WriteStartArray("records");

            JsonExportSink sink = new(
                writer,
                descriptor,
                () => totalRecords,
                count => totalRecords = count);
            DataRightsSubjectExportResult result;
            try
            {
                result = await contributor.ExportAsync(
                    new DataRightsSubjectExportRequest(
                        request.TenantId,
                        request.CaseType,
                        request.PropertyId,
                        subject),
                    sink,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is not DataRightsExportGenerationException)
            {
                throw new DataRightsExportGenerationException(
                    "owner-export-failed");
            }

            if (result is null ||
                result.Status != DataRightsSubjectExportStatus.Succeeded ||
                result.RecordCount != sink.SubjectRecordCount)
            {
                throw new DataRightsExportGenerationException(
                    StatusCode(result?.Status));
            }

            writer.WriteEndArray();
            writer.WriteNumber("recordCount", sink.SubjectRecordCount);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartObject("summary");
        writer.WriteNumber("subjectCount", subjects.Length);
        writer.WriteNumber("recordCount", totalRecords);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();

        return new DataRightsExportAssemblyResult(subjects.Length, totalRecords);
    }

    private IReadOnlyDictionary<string, IDataRightsSubjectExportContributor>
        ResolveContributors(
            DataRightsCaseType caseType,
            IReadOnlyCollection<DataRightsSubjectCoordinate> subjects)
    {
        var result = DataRightsExportContributorSet.Resolve(
            contributors,
            caseType,
            subjects);
        if (result.IsFailure)
        {
            throw new DataRightsExportGenerationException(
                "owner-unavailable");
        }

        return result.Value;
    }

    private static DataRightsSubjectCoordinate[] ValidateAndOrder(
        DataRightsExportGenerationRequest request)
    {
        if (request.ArtifactId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.TenantId) ||
            request.CaseId == Guid.Empty ||
            request.CaseType is not DataRightsCaseType.GuestRights and
                not DataRightsCaseType.StaffRights ||
            request.DecisionRevision <= 0 ||
            request.GeneratedAtUtc == default ||
            request.ExpiresAtUtc <= request.GeneratedAtUtc ||
            request.SelectedSubjects is null ||
            request.SelectedSubjects.Count is <= 0 or >
                BunkFy.Modules.DataRights.Domain.Aggregates
                    .DataRightsCase.MaxSelectedSubjects ||
            !IsValidScope(request.CaseType, request.PropertyId))
        {
            throw new DataRightsExportGenerationException(
                "generation-coordinate-invalid");
        }

        DataRightsSubjectCoordinate[] ordered = request.SelectedSubjects
            .OrderBy(subject => subject.OwnerKey, StringComparer.Ordinal)
            .ThenBy(subject => subject.RecordType, StringComparer.Ordinal)
            .ThenBy(subject => subject.RecordId)
            .ToArray();
        if (ordered.Any(subject =>
                string.IsNullOrWhiteSpace(subject.OwnerKey) ||
                subject.OwnerKey.Trim().Length >
                    DataRightsExportLimits.OwnerKeyMaxLength ||
                string.IsNullOrWhiteSpace(subject.RecordType) ||
                subject.RecordType.Trim().Length >
                    DataRightsExportLimits.RecordTypeMaxLength ||
                subject.RecordId == Guid.Empty ||
                subject.RecordVersion <= 0) ||
            ordered.GroupBy(subject => (
                    subject.OwnerKey.Trim().ToLowerInvariant(),
                    subject.RecordType.Trim().ToLowerInvariant(),
                    subject.RecordId))
                .Any(group => group.Count() != 1))
        {
            throw new DataRightsExportGenerationException(
                "subject-coordinate-invalid");
        }

        return ordered;
    }

    private static bool IsValidScope(
        DataRightsCaseType caseType,
        Guid? propertyId) =>
        caseType switch
        {
            DataRightsCaseType.GuestRights =>
                propertyId is Guid value && value != Guid.Empty,
            DataRightsCaseType.StaffRights => propertyId is null,
            _ => false
        };

    private static DataRightsExportDescriptor ValidateDescriptor(
        IDataRightsSubjectExportContributor contributor,
        string ownerKey)
    {
        DataRightsExportDescriptor descriptor = contributor.Descriptor;
        string normalizedOwner = ownerKey.Trim().ToLowerInvariant();
        if (!string.Equals(
                contributor.OwnerKey.Trim(),
                normalizedOwner,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                descriptor.OwnerKey?.Trim(),
                normalizedOwner,
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(descriptor.CatalogId) ||
            descriptor.CatalogSchemaVersion <= 0 ||
            descriptor.CatalogVersion <= 0 ||
            string.IsNullOrWhiteSpace(descriptor.ExportSchemaId) ||
            descriptor.ExportSchemaVersion <= 0 ||
            descriptor.FieldIds is null ||
            descriptor.FieldIds.Count is <= 0 or >
                DataRightsExportLimits.MaxFieldsPerRecord ||
            descriptor.FieldIds.Any(field =>
                string.IsNullOrWhiteSpace(field) ||
                field.Trim().Length > DataRightsExportLimits.FieldIdMaxLength) ||
            descriptor.FieldIds.Distinct(StringComparer.Ordinal).Count() !=
                descriptor.FieldIds.Count)
        {
            throw new DataRightsExportGenerationException(
                "owner-descriptor-invalid");
        }

        return descriptor;
    }

    private static void WriteCoordinate(
        Utf8JsonWriter writer,
        DataRightsSubjectCoordinate subject)
    {
        writer.WriteStartObject("coordinate");
        writer.WriteString(
            "owner",
            subject.OwnerKey.Trim().ToLowerInvariant());
        writer.WriteString(
            "recordType",
            subject.RecordType.Trim().ToLowerInvariant());
        writer.WriteString("recordId", subject.RecordId);
        writer.WriteNumber("recordVersion", subject.RecordVersion);
        writer.WriteEndObject();
    }

    private static void WriteDescriptor(
        Utf8JsonWriter writer,
        DataRightsExportDescriptor descriptor)
    {
        writer.WriteStartObject("ownerDescriptor");
        writer.WriteString("catalogId", descriptor.CatalogId);
        writer.WriteNumber("catalogSchemaVersion", descriptor.CatalogSchemaVersion);
        writer.WriteNumber("catalogVersion", descriptor.CatalogVersion);
        writer.WriteString("exportSchemaId", descriptor.ExportSchemaId);
        writer.WriteNumber("exportSchemaVersion", descriptor.ExportSchemaVersion);
        writer.WriteEndObject();
    }

    private static string CaseType(DataRightsCaseType caseType) =>
        caseType == DataRightsCaseType.GuestRights
            ? "guestRights"
            : "staffRights";

    private static string StatusCode(DataRightsSubjectExportStatus? status) =>
        status switch
        {
            DataRightsSubjectExportStatus.ScopeUnavailable =>
                "owner-scope-unavailable",
            DataRightsSubjectExportStatus.NotFound => "subject-not-found",
            DataRightsSubjectExportStatus.Stale => "subject-stale",
            _ => "owner-result-invalid"
        };

    private sealed class JsonExportSink(
        Utf8JsonWriter writer,
        DataRightsExportDescriptor descriptor,
        Func<int> totalCount,
        Action<int> setTotalCount)
        : IDataRightsExportSink
    {
        private readonly HashSet<string> allowedFields =
            descriptor.FieldIds.ToHashSet(StringComparer.Ordinal);

        public int SubjectRecordCount { get; private set; }

        public ValueTask WriteAsync(
            DataRightsExportRecord record,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.ValidateRecord(record);
            int nextTotal = checked(totalCount() + 1);
            if (nextTotal > MaximumRecords)
            {
                throw new DataRightsExportGenerationException(
                    "record-limit-exceeded");
            }

            writer.WriteStartObject();
            writer.WriteString("recordType", record.RecordType.Trim().ToLowerInvariant());
            writer.WriteString("recordId", record.RecordId);
            writer.WriteNumber("recordVersion", record.RecordVersion);
            writer.WriteStartObject("fields");
            foreach (DataRightsExportField field in record.Fields
                .OrderBy(item => item.FieldId, StringComparer.Ordinal))
            {
                writer.WritePropertyName(field.FieldId);
                field.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            this.SubjectRecordCount++;
            setTotalCount(nextTotal);
            return ValueTask.CompletedTask;
        }

        private void ValidateRecord(DataRightsExportRecord? record)
        {
            if (record is null ||
                string.IsNullOrWhiteSpace(record.RecordType) ||
                record.RecordType.Trim().Length >
                    DataRightsExportLimits.RecordTypeMaxLength ||
                record.RecordId == Guid.Empty ||
                record.RecordVersion <= 0 ||
                record.Fields is null ||
                record.Fields.Count is <= 0 or >
                    DataRightsExportLimits.MaxFieldsPerRecord ||
                record.Fields.Any(field =>
                    field is null ||
                    string.IsNullOrWhiteSpace(field.FieldId) ||
                    field.FieldId.Trim().Length >
                        DataRightsExportLimits.FieldIdMaxLength ||
                    !this.allowedFields.Contains(field.FieldId) ||
                    field.Value.ValueKind == JsonValueKind.Undefined ||
                    Encoding.UTF8.GetByteCount(field.Value.GetRawText()) >
                        DataRightsExportLimits.MaxFieldValueBytes) ||
                record.Fields.Select(field => field.FieldId)
                    .Distinct(StringComparer.Ordinal).Count() !=
                    record.Fields.Count)
            {
                throw new DataRightsExportGenerationException(
                    "owner-record-invalid");
            }
        }
    }
}
