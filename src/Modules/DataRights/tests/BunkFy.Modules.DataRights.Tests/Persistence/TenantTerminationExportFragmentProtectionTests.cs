namespace BunkFy.Modules.DataRights.Tests.Persistence;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.FileManagement;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationExportFragmentProtectionTests
{
    private const string CatalogSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FrozenSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string PolicySha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private static readonly DateTimeOffset GeneratedAt =
        new(2026, 7, 31, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt =
        GeneratedAt.AddHours(24);

    [Fact]
    public async Task Generator_stores_and_revalidates_a_bound_tenant_fragment()
    {
        byte[] content = Encoding.UTF8.GetBytes(
            """{"format":"bunkfy.tenant-termination.export-fragment"}""");
        InMemoryFileStorage storage = new();
        IOptions<DataRightsExportArtifactOptions> options =
            Options.Create(OptionsValue());
        AesGcmDataRightsExportEnvelopeProtector protector = new(options);
        ProtectedTenantTerminationExportFragmentGenerator generator = new(
            new StaticFragmentAssembler(content),
            new ProtectedDataRightsExportObjectWriter(
                protector,
                storage,
                options,
                new TestClock(GeneratedAt.AddMinutes(1))),
            options);

        TenantTerminationProtectedExportFragment fragment =
            await generator.GenerateAsync(
                GenerationRequest(),
                CancellationToken.None);

        Assert.Matches(
            "^data-rights/tenant-exports/scope-[a-f0-9]{16}/" +
            "process-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/" +
            "fragment-dddddddddddddddddddddddddddddddd[.]bftxf$",
            fragment.StorageKey);
        Assert.DoesNotContain(
            "tenant-a",
            fragment.StorageKey,
            StringComparison.Ordinal);
        Assert.Equal("workspaces", fragment.AssemblyResult.Owner.OwnerKey);
        Assert.Equal(11, fragment.AssemblyResult.Owner.SelectedProofRevision);
        Assert.Equal(fragment.EncryptedByteLength, storage.Content.LongLength);
        Assert.NotEqual(content, storage.Content);
        Assert.Equal("data-rights", storage.Properties!.Metadata["module"]);
        Assert.Equal("bftxf-1", storage.Properties.Metadata["format"]);
        Assert.Equal("1", storage.Properties.Metadata["key-version"]);
        CryptographicOperations.ZeroMemory(content);
    }

    [Fact]
    public async Task Tenant_fragment_envelope_rejects_other_formats_and_owner_drift()
    {
        byte[] content = RandomNumberGenerator.GetBytes(20_000);
        AesGcmDataRightsExportEnvelopeProtector protector = new(
            Options.Create(OptionsValue()));
        await using MemoryStream plaintext = new(content, writable: false);
        await using MemoryStream encrypted = new();
        _ = await protector.ProtectAsync(
            plaintext,
            content.Length,
            encrypted,
            FragmentContext(),
            CancellationToken.None);

        encrypted.Position = 0;
        await using MemoryStream subjectOutput = new();
        DataRightsExportGenerationException wrongFormat =
            await Assert.ThrowsAsync<DataRightsExportGenerationException>(
                () => protector.UnprotectAsync(
                    encrypted,
                    subjectOutput,
                    SubjectContext(),
                    CancellationToken.None));
        Assert.Equal("artifact-envelope-invalid", wrongFormat.Code);

        encrypted.Position = 0;
        await using MemoryStream wrongOwnerOutput = new();
        DataRightsExportGenerationException wrongOwner =
            await Assert.ThrowsAsync<DataRightsExportGenerationException>(
                () => protector.UnprotectAsync(
                    encrypted,
                    wrongOwnerOutput,
                    FragmentContext() with { OwnerKey = "inventory" },
                    CancellationToken.None));
        Assert.Equal("artifact-authentication-failed", wrongOwner.Code);
        CryptographicOperations.ZeroMemory(content);
    }

    private static TenantTerminationExportFragmentGenerationRequest
        GenerationRequest() => new(
            AssemblyRequest(),
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            GenerationAttempt: 1,
            ExpiresAt);

    private static TenantTerminationExportFragmentAssemblyRequest
        AssemblyRequest() => new(
            "tenant-a",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ApprovalRevision: 7,
            FreezeOperationRevision: 1,
            ExportOperationRevision: 2,
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            WorkspaceFenceRevision: 3,
            FrozenSha,
            PolicySha,
            ExecutingActorId: "system:tenant-termination-export",
            FrozenAtUtc: GeneratedAt.AddMinutes(-5),
            GeneratedAt,
            DeadlineUtc: GeneratedAt.AddHours(1),
            new TenantTerminationExportOwnerWork(
                "workspaces",
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                ContractVersion: 1,
                CatalogVersion: 1,
                CatalogSha),
            [
                new TenantTerminationExportOwnerCatalogEntry(
                    "workspaces",
                    ContractVersion: 1,
                    CatalogVersion: 1,
                    CatalogSha)
            ]);

    private static TenantTerminationExportFragmentProtectionContext
        FragmentContext() => new(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            "tenant-a",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ApprovalRevision: 7,
            FreezeOperationRevision: 1,
            ExportOperationRevision: 2,
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "workspaces",
            OwnerContractVersion: 1,
            CatalogVersion: 1,
            CatalogSha,
            FrozenSha,
            PolicySha,
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            GenerationAttempt: 1,
            ExpiresAt);

    private static DataRightsSubjectExportProtectionContext SubjectContext() =>
        new(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            "tenant-a",
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            DataRightsCaseType.StaffRights,
            PropertyId: null,
            DecisionRevision: 7,
            ExpiresAt);

    private static DataRightsExportArtifactOptions OptionsValue() => new()
    {
        ActiveKeyVersion = 1,
        Keys = new()
        {
            [1] = DataRightsExportArtifactOptions.DevelopmentKeyBase64
        },
        ChunkSizeBytes = 16 * 1024,
        MaximumPlaintextBytes = 1024 * 1024,
        ArtifactLifetime = TimeSpan.FromHours(24)
    };

    private sealed class StaticFragmentAssembler(byte[] content)
        : ITenantTerminationExportFragmentAssembler
    {
        public async Task<TenantTerminationExportFragmentAssemblyResult>
            AssembleAsync(
                TenantTerminationExportFragmentAssemblyRequest request,
                Stream destination,
                CancellationToken cancellationToken)
        {
            await destination.WriteAsync(content, cancellationToken);
            return new(
                request.FrozenRevisionSha256,
                new TenantTerminationExportOwnerResult(
                    request.OwnerWork.OwnerKey,
                    RecordCount: 1,
                    SelectedProofRevision: 11,
                    ResultingProofRevision: 11,
                    "workspaces.tenant-export.completed",
                    request.GeneratedAtUtc));
        }
    }

    private sealed class TestClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class InMemoryFileStorage : IFileStorage
    {
        public byte[] Content { get; private set; } = [];
        public FileStorageObjectProperties? Properties { get; private set; }

        public async Task<FileStorageObjectProperties> PutAsync(
            FileStorageWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            await using MemoryStream copy = new();
            await request.Content.CopyToAsync(copy, cancellationToken);
            this.Content = copy.ToArray();
            this.Properties = new FileStorageObjectProperties(
                request.Key,
                request.ContentLength,
                request.ContentType,
                request.FileName,
                ETag: "test-etag",
                LastModifiedUtc: GeneratedAt,
                request.Metadata);
            return this.Properties;
        }

        public Task<FileStorageReadResult?> OpenReadAsync(
            FileStorageObjectKey key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<FileStorageReadResult?>(
                this.Properties?.Key == key
                    ? new(
                        this.Properties,
                        async (destination, token) =>
                            await destination.WriteAsync(this.Content, token))
                    : null);

        public Task<FileStorageObjectProperties?> GetPropertiesAsync(
            FileStorageObjectKey key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                this.Properties?.Key == key
                    ? this.Properties
                    : null);

        public Task<bool> DeleteAsync(
            FileStorageObjectKey key,
            CancellationToken cancellationToken = default)
        {
            bool deleted = this.Properties?.Key == key;
            this.Properties = null;
            CryptographicOperations.ZeroMemory(this.Content);
            this.Content = [];
            return Task.FromResult(deleted);
        }
    }
}
