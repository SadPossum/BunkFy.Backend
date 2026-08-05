namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using Microsoft.Extensions.Options;

internal sealed class ProtectedTenantTerminationExportFragmentGenerator(
    ITenantTerminationExportFragmentAssembler assembler,
    ProtectedDataRightsExportObjectWriter writer,
    IOptions<DataRightsExportArtifactOptions> options)
    : ITenantTerminationExportFragmentGenerator
{
    private readonly DataRightsExportArtifactOptions options = options.Value;

    public async Task<TenantTerminationProtectedExportFragment> GenerateAsync(
        TenantTerminationExportFragmentGenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.AssemblyRequest);
        TenantTerminationExportFragmentAssemblyRequest assemblyRequest =
            request.AssemblyRequest;

        await using FileStream plaintext = DataRightsExportTemporaryFile.Create();
        BoundedWriteStream boundedPlaintext = new(
            plaintext,
            this.options.MaximumPlaintextBytes);
        TenantTerminationExportFragmentAssemblyResult assemblyResult;
        try
        {
            assemblyResult = await assembler.AssembleAsync(
                assemblyRequest,
                boundedPlaintext,
                cancellationToken).ConfigureAwait(false);
            await boundedPlaintext.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DataRightsExportBufferLimitException exception)
        {
            throw new DataRightsExportGenerationException(
                "plaintext-limit-exceeded",
                exception);
        }

        long plaintextLength = plaintext.Length;
        if (plaintextLength is <= 0 ||
            plaintextLength > this.options.MaximumPlaintextBytes)
        {
            throw new DataRightsExportGenerationException(
                "plaintext-limit-exceeded");
        }

        plaintext.Position = 0;
        byte[] plaintextHash = await SHA256.HashDataAsync(
            plaintext,
            cancellationToken).ConfigureAwait(false);
        try
        {
            TenantTerminationExportOwnerWork owner =
                assemblyRequest.OwnerWork;
            DataRightsProtectedExportObject protectedObject =
                await writer.WriteAsync(
                    plaintext,
                    plaintextLength,
                    plaintextHash,
                    DataRightsExportStorageKey
                        .CreateTenantTerminationFragment(
                            assemblyRequest.TenantId,
                            assemblyRequest.ProcessId,
                            owner.WorkItemId),
                    new TenantTerminationExportFragmentProtectionContext(
                        owner.WorkItemId,
                        assemblyRequest.TenantId,
                        assemblyRequest.ProcessId,
                        assemblyRequest.CaseId,
                        assemblyRequest.ApprovalRevision,
                        assemblyRequest.FreezeOperationRevision,
                        assemblyRequest.ExportOperationRevision,
                        assemblyRequest.TerminationEpoch,
                        owner.OwnerKey,
                        owner.ContractVersion,
                        owner.CatalogVersion,
                        owner.CatalogSha256,
                        assemblyRequest.FrozenRevisionSha256,
                        assemblyRequest.PolicyEvidenceSha256,
                        request.GenerationRunId,
                        request.GenerationAttempt,
                        request.ExpiresAtUtc),
                    "bftxf-1",
                    cancellationToken).ConfigureAwait(false);
            return new TenantTerminationProtectedExportFragment(
                assemblyResult,
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
}
