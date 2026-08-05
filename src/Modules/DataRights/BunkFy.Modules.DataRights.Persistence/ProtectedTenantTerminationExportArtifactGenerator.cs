namespace BunkFy.Modules.DataRights.Persistence;

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Microsoft.Extensions.Options;

internal sealed class ProtectedTenantTerminationExportArtifactGenerator(
    ProtectedDataRightsExportObjectReader reader,
    ProtectedDataRightsExportObjectWriter writer,
    IOptions<DataRightsExportArtifactOptions> options)
    : ITenantTerminationExportArtifactGenerator
{
    private const string BundleSchema =
        "bunkfy.tenant-termination.export-bundle";
    private const int BundleFormatVersion = 1;
    private static readonly DateTimeOffset ZipTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly DataRightsExportArtifactOptions options = options.Value;

    public async Task<TenantTerminationProtectedExportArtifact> GenerateAsync(
        TenantTerminationExportArtifact artifact,
        IReadOnlyCollection<TenantTerminationExportFragment> fragments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(fragments);
        TenantTerminationExportFragmentManifestEntry[] exact =
            ValidateAndOrder(artifact, fragments, out long recordCount);

        await using FileStream bundle = DataRightsExportTemporaryFile.Create();
        try
        {
            BoundedSeekableWriteStream bounded = new(
                bundle,
                this.options.MaximumPlaintextBytes);
            using (ZipArchive archive = new(
                bounded,
                ZipArchiveMode.Create,
                leaveOpen: true,
                Encoding.UTF8))
            {
                await WriteManifestAsync(
                    archive,
                    artifact,
                    exact,
                    recordCount,
                    cancellationToken).ConfigureAwait(false);
                foreach (TenantTerminationExportFragmentManifestEntry fragment
                         in exact)
                {
                    await this.WriteFragmentAsync(
                        archive,
                        artifact,
                        fragment,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            await bounded.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DataRightsExportBufferLimitException exception)
        {
            throw new DataRightsExportGenerationException(
                "plaintext-limit-exceeded",
                exception);
        }

        long plaintextLength = bundle.Length;
        if (plaintextLength is <= 0 ||
            plaintextLength > this.options.MaximumPlaintextBytes)
        {
            throw new DataRightsExportGenerationException(
                "plaintext-limit-exceeded");
        }

        bundle.Position = 0;
        byte[] plaintextHash = await SHA256.HashDataAsync(
            bundle,
            cancellationToken).ConfigureAwait(false);
        try
        {
            DataRightsProtectedExportObject protectedObject =
                await writer.WriteAsync(
                    bundle,
                    plaintextLength,
                    plaintextHash,
                    DataRightsExportStorageKey
                        .CreateTenantTerminationArtifact(
                            artifact.ScopeId,
                            artifact.ProcessId,
                            artifact.Id),
                    ArtifactContext(artifact),
                    "bftxa-1",
                    cancellationToken).ConfigureAwait(false);
            return new(
                exact.Length,
                recordCount,
                artifact.FragmentSetSha256,
                protectedObject.StorageKey,
                protectedObject.EncryptedByteLength,
                protectedObject.PlaintextSha256,
                protectedObject.EncryptionKeyVersion,
                protectedObject.FormatVersion,
                protectedObject.AvailableAtUtc,
                protectedObject.ExpiresAtUtc);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextHash);
        }
    }

    private async Task WriteFragmentAsync(
        ZipArchive archive,
        TenantTerminationExportArtifact artifact,
        TenantTerminationExportFragmentManifestEntry fragment,
        CancellationToken cancellationToken)
    {
        await using FileStream plaintext = await reader.OpenVerifiedAsync(
            new DataRightsProtectedExportReadRequest(
                fragment.StorageKey,
                fragment.EncryptedByteLength,
                fragment.PlaintextSha256,
                fragment.EncryptionKeyVersion,
                fragment.FormatVersion,
                FragmentContext(artifact, fragment)),
            cancellationToken).ConfigureAwait(false);
        ZipArchiveEntry entry = CreateEntry(
            archive,
            EntryName(fragment));
        await using Stream destination = entry.Open();
        await plaintext.CopyToAsync(
            destination,
            64 * 1024,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteManifestAsync(
        ZipArchive archive,
        TenantTerminationExportArtifact artifact,
        TenantTerminationExportFragmentManifestEntry[] fragments,
        long recordCount,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = CreateEntry(archive, "manifest.json");
        await using Stream destination = entry.Open();
        using Utf8JsonWriter json = new(destination, new JsonWriterOptions
        {
            Indented = false
        });
        json.WriteStartObject();
        json.WriteString("schema", BundleSchema);
        json.WriteNumber("formatVersion", BundleFormatVersion);
        json.WriteString("artifactId", artifact.Id.ToString("N"));
        json.WriteString("processId", artifact.ProcessId.ToString("N"));
        json.WriteString("caseId", artifact.CaseId.ToString("N"));
        json.WriteNumber("approvalRevision", artifact.ApprovalRevision);
        json.WriteNumber(
            "freezeOperationRevision",
            artifact.FreezeOperationRevision);
        json.WriteNumber(
            "exportOperationRevision",
            artifact.ExportOperationRevision);
        json.WriteString(
            "terminationEpoch",
            artifact.TerminationEpoch.ToString("N"));
        json.WriteString(
            "frozenRevisionSha256",
            artifact.FrozenRevisionSha256);
        json.WriteString(
            "policyEvidenceSha256",
            artifact.PolicyEvidenceSha256);
        json.WriteString("fragmentSetSha256", artifact.FragmentSetSha256);
        json.WriteNumber("fragmentCount", fragments.Length);
        json.WriteNumber("recordCount", recordCount);
        json.WriteString(
            "generationRunId",
            artifact.GenerationRunId!.Value.ToString("N"));
        json.WriteNumber(
            "generationAttempt",
            artifact.GenerationAttempt!.Value);
        WriteTimestamp(json, "requestedAtUtc", artifact.RequestedAtUtc);
        WriteTimestamp(json, "expiresAtUtc", artifact.ExpiresAtUtc);
        json.WriteStartArray("fragments");
        foreach (TenantTerminationExportFragmentManifestEntry fragment in
                 fragments)
        {
            json.WriteStartObject();
            json.WriteString("ownerKey", fragment.OwnerKey);
            json.WriteString("entry", EntryName(fragment));
            json.WriteString(
                "fragmentId",
                fragment.FragmentId.ToString("N"));
            json.WriteNumber("fragmentVersion", fragment.FragmentVersion);
            json.WriteString(
                "fragmentIdempotencyKey",
                fragment.FragmentIdempotencyKey.ToString("N"));
            json.WriteNumber(
                "freezeOperationRevision",
                fragment.FreezeOperationRevision);
            json.WriteNumber(
                "exportOperationRevision",
                fragment.ExportOperationRevision);
            json.WriteNumber(
                "ownerContractVersion",
                fragment.OwnerContractVersion);
            json.WriteNumber("catalogVersion", fragment.CatalogVersion);
            json.WriteString("catalogSha256", fragment.CatalogSha256);
            json.WriteString(
                "frozenRevisionSha256",
                fragment.FrozenRevisionSha256);
            json.WriteNumber("recordCount", fragment.RecordCount);
            json.WriteNumber(
                "selectedProofRevision",
                fragment.SelectedProofRevision);
            json.WriteNumber(
                "resultingProofRevision",
                fragment.ResultingProofRevision);
            json.WriteString("resultCode", fragment.ResultCode);
            json.WriteString("storageKey", fragment.StorageKey);
            json.WriteNumber(
                "encryptedByteLength",
                fragment.EncryptedByteLength);
            json.WriteString("plaintextSha256", fragment.PlaintextSha256);
            json.WriteNumber(
                "encryptionKeyVersion",
                fragment.EncryptionKeyVersion);
            json.WriteNumber("formatVersion", fragment.FormatVersion);
            json.WriteString(
                "generationRunId",
                fragment.GenerationRunId.ToString("N"));
            json.WriteNumber(
                "generationAttempt",
                fragment.GenerationAttempt);
            WriteTimestamp(json, "availableAtUtc", fragment.AvailableAtUtc);
            WriteTimestamp(json, "expiresAtUtc", fragment.ExpiresAtUtc);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
        await json.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TenantTerminationExportFragmentManifestEntry[]
        ValidateAndOrder(
            TenantTerminationExportArtifact artifact,
            IReadOnlyCollection<TenantTerminationExportFragment> fragments,
            out long recordCount)
    {
        recordCount = 0;
        if (artifact.State != TenantTerminationExportArtifactState.Generating ||
            artifact.GenerationRunId is not Guid generationRunId ||
            generationRunId == Guid.Empty ||
            artifact.GenerationAttempt is not int generationAttempt ||
            generationAttempt <= 0 ||
            artifact.GenerationStartedAtUtc is null ||
            fragments.Count != artifact.ExpectedFragmentCount ||
            fragments.Count is <= 0 or >
                TenantTerminationExportArtifact.MaximumFragmentCount)
        {
            throw InvalidProof();
        }

        List<TenantTerminationExportFragmentManifestEntry> entries = [];
        HashSet<string> owners = new(StringComparer.Ordinal);
        try
        {
            foreach (TenantTerminationExportFragment fragment in fragments)
            {
                if (fragment is null ||
                    !FragmentMatches(artifact, fragment) ||
                    !owners.Add(fragment.OwnerKey) ||
                    !TenantTerminationExportFragmentManifestEntry.TryCreate(
                        fragment,
                        out TenantTerminationExportFragmentManifestEntry? entry))
                {
                    throw InvalidProof();
                }

                recordCount = checked(recordCount + entry.RecordCount);
                entries.Add(entry);
            }
        }
        catch (OverflowException exception)
        {
            throw new DataRightsExportGenerationException(
                "tenant-artifact-proof-invalid",
                exception);
        }

        if (recordCount > TenantTerminationExportArtifact.MaximumRecordCount)
        {
            throw InvalidProof();
        }

        TenantTerminationExportFragmentManifestEntry[] ordered = [..
            entries
                .OrderBy(entry => entry.OwnerKey, StringComparer.Ordinal)
                .ThenBy(entry => entry.FragmentId)];
        string digest = TenantTerminationExportFragmentSet.ComputeSha256(
            Coordinates(artifact),
            ordered);
        if (!string.Equals(
                digest,
                artifact.FragmentSetSha256,
                StringComparison.Ordinal))
        {
            throw InvalidProof();
        }

        return ordered;
    }

    private static bool FragmentMatches(
        TenantTerminationExportArtifact artifact,
        TenantTerminationExportFragment fragment) =>
        fragment.State == TenantTerminationExportFragmentState.Available &&
        string.Equals(fragment.ScopeId, artifact.ScopeId, StringComparison.Ordinal) &&
        fragment.ProcessId == artifact.ProcessId &&
        fragment.CaseId == artifact.CaseId &&
        fragment.ApprovalRevision == artifact.ApprovalRevision &&
        fragment.FreezeOperationRevision == artifact.FreezeOperationRevision &&
        fragment.ExportOperationRevision == artifact.ExportOperationRevision &&
        fragment.TerminationEpoch == artifact.TerminationEpoch &&
        string.Equals(
            fragment.FrozenRevisionSha256,
            artifact.FrozenRevisionSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            fragment.PolicyEvidenceSha256,
            artifact.PolicyEvidenceSha256,
            StringComparison.Ordinal) &&
        fragment.AvailableAtUtc <= artifact.RequestedAtUtc &&
        fragment.ExpiresAtUtc >= artifact.ExpiresAtUtc;

    private static TenantTerminationExportFragmentSetCoordinates Coordinates(
        TenantTerminationExportArtifact artifact) => new(
        artifact.ScopeId,
        artifact.ProcessId,
        artifact.CaseId,
        artifact.ApprovalRevision,
        artifact.ExportOperationRevision,
        artifact.TerminationEpoch,
        artifact.PolicyEvidenceSha256);

    private static TenantTerminationExportFragmentProtectionContext
        FragmentContext(
            TenantTerminationExportArtifact artifact,
            TenantTerminationExportFragmentManifestEntry fragment) => new(
        fragment.FragmentId,
        artifact.ScopeId,
        artifact.ProcessId,
        artifact.CaseId,
        artifact.ApprovalRevision,
        fragment.FreezeOperationRevision,
        fragment.ExportOperationRevision,
        artifact.TerminationEpoch,
        fragment.OwnerKey,
        fragment.OwnerContractVersion,
        fragment.CatalogVersion,
        fragment.CatalogSha256,
        fragment.FrozenRevisionSha256,
        artifact.PolicyEvidenceSha256,
        fragment.GenerationRunId,
        fragment.GenerationAttempt,
        fragment.ExpiresAtUtc);

    private static TenantTerminationExportArtifactProtectionContext
        ArtifactContext(TenantTerminationExportArtifact artifact) => new(
        artifact.Id,
        artifact.ScopeId,
        artifact.ProcessId,
        artifact.CaseId,
        artifact.ApprovalRevision,
        artifact.FreezeOperationRevision,
        artifact.ExportOperationRevision,
        artifact.TerminationEpoch,
        artifact.FrozenRevisionSha256,
        artifact.PolicyEvidenceSha256,
        artifact.ExpectedFragmentCount,
        artifact.FragmentSetSha256,
        artifact.GenerationRunId!.Value,
        artifact.GenerationAttempt!.Value,
        artifact.ExpiresAtUtc);

    private static ZipArchiveEntry CreateEntry(
        ZipArchive archive,
        string name)
    {
        ZipArchiveEntry entry = archive.CreateEntry(
            name,
            CompressionLevel.NoCompression);
        entry.LastWriteTime = ZipTimestamp;
        entry.ExternalAttributes = 0;
        return entry;
    }

    private static string EntryName(
        TenantTerminationExportFragmentManifestEntry fragment) =>
        $"owners/{fragment.OwnerKey}.json";

    private static void WriteTimestamp(
        Utf8JsonWriter writer,
        string propertyName,
        DateTimeOffset value) =>
        writer.WriteString(
            propertyName,
            value.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture));

    private static DataRightsExportGenerationException InvalidProof() =>
        new("tenant-artifact-proof-invalid");
}
