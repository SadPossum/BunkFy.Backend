namespace BunkFy.Modules.DataRights.Persistence;

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Microsoft.Extensions.Options;

internal sealed class AesGcmDataRightsExportArtifactProtector(
    IOptions<DataRightsExportArtifactOptions> options)
    : IDataRightsExportArtifactProtector
{
    internal const int FormatVersion = 1;
    internal const int HeaderLength = 36;
    internal const int TagLength = 16;
    private const int NonceLength = 12;
    private const int NoncePrefixLength = 8;
    private const string BindingDomain =
        "bunkfy.data-rights.export-artifact.binding.v1";
    private const string EncryptionKeyDomain =
        "bunkfy.data-rights.export-artifact.encryption-key.v1";
    private static readonly byte[] Magic = "BFDRX001"u8.ToArray();
    private readonly DataRightsExportArtifactOptions options = options.Value;

    public async Task<DataRightsExportProtectionResult> ProtectAsync(
        Stream plaintext,
        long plaintextLength,
        Stream encrypted,
        DataRightsExportProtectionContext context,
        CancellationToken cancellationToken)
    {
        ValidateStreams(plaintext, encrypted, protect: true);
        ValidateContext(context);
        if (plaintextLength is <= 0 ||
            plaintextLength > this.options.MaximumPlaintextBytes)
        {
            throw Failure("plaintext-limit-exceeded");
        }

        int keyVersion = this.options.ActiveKeyVersion;
        byte[] masterKey = this.GetKey(keyVersion);
        byte[] encryptionKey = Derive(masterKey);
        byte[] noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixLength);
        byte[] header = CreateHeader(
            keyVersion,
            this.options.ChunkSizeBytes,
            plaintextLength,
            noncePrefix);
        byte[] binding = CreateBinding(context);
        byte[] plaintextBuffer = new byte[this.options.ChunkSizeBytes];
        byte[] ciphertextBuffer = new byte[this.options.ChunkSizeBytes];
        byte[] tag = new byte[TagLength];
        byte[] nonce = new byte[NonceLength];
        byte[] associatedData = new byte[binding.Length + header.Length + sizeof(uint)];

        long written = header.Length;
        try
        {
            await encrypted.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            binding.CopyTo(associatedData, 0);
            header.CopyTo(associatedData, binding.Length);
            noncePrefix.CopyTo(nonce, 0);
            using AesGcm aes = new(encryptionKey, TagLength);

            long remaining = plaintextLength;
            uint chunkIndex = 0;
            while (remaining > 0)
            {
                int chunkLength = (int)Math.Min(
                    this.options.ChunkSizeBytes,
                    remaining);
                await plaintext.ReadExactlyAsync(
                    plaintextBuffer.AsMemory(0, chunkLength),
                    cancellationToken).ConfigureAwait(false);
                BinaryPrimitives.WriteUInt32BigEndian(
                    nonce.AsSpan(NoncePrefixLength),
                    chunkIndex);
                BinaryPrimitives.WriteUInt32BigEndian(
                    associatedData.AsSpan(binding.Length + header.Length),
                    chunkIndex);
                aes.Encrypt(
                    nonce,
                    plaintextBuffer.AsSpan(0, chunkLength),
                    ciphertextBuffer.AsSpan(0, chunkLength),
                    tag,
                    associatedData);
                await encrypted.WriteAsync(
                    ciphertextBuffer.AsMemory(0, chunkLength),
                    cancellationToken).ConfigureAwait(false);
                await encrypted.WriteAsync(tag, cancellationToken)
                    .ConfigureAwait(false);
                written += chunkLength + tag.Length;
                remaining -= chunkLength;
                chunkIndex = checked(chunkIndex + 1);
            }

            byte[] probe = new byte[1];
            try
            {
                if (await plaintext.ReadAsync(probe, cancellationToken)
                    .ConfigureAwait(false) != 0)
                {
                    throw Failure("plaintext-length-mismatch");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(probe);
            }

            return new DataRightsExportProtectionResult(
                FormatVersion,
                keyVersion,
                plaintextLength,
                written);
        }
        catch (DataRightsExportGenerationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is CryptographicException or EndOfStreamException)
        {
            throw Failure("artifact-encryption-failed", exception);
        }
        finally
        {
            Zero(
                masterKey,
                encryptionKey,
                noncePrefix,
                header,
                binding,
                plaintextBuffer,
                ciphertextBuffer,
                tag,
                nonce,
                associatedData);
        }
    }

    public async Task<DataRightsExportProtectionResult> UnprotectAsync(
        Stream encrypted,
        Stream plaintext,
        DataRightsExportProtectionContext context,
        CancellationToken cancellationToken)
    {
        ValidateStreams(encrypted, plaintext, protect: false);
        ValidateContext(context);

        byte[] header = new byte[HeaderLength];
        byte[] masterKey = [];
        byte[] encryptionKey = [];
        byte[] binding = [];
        byte[] ciphertextBuffer = [];
        byte[] plaintextBuffer = [];
        byte[] tag = new byte[TagLength];
        byte[] nonce = new byte[NonceLength];
        byte[] associatedData = [];
        try
        {
            await encrypted.ReadExactlyAsync(header, cancellationToken)
                .ConfigureAwait(false);
            Header parsed = ParseHeader(header);
            if (parsed.PlaintextLength is <= 0 ||
                parsed.PlaintextLength > this.options.MaximumPlaintextBytes ||
                parsed.ChunkSize is < 16 * 1024 or > 1024 * 1024)
            {
                throw Failure("artifact-envelope-invalid");
            }

            masterKey = this.GetKey(parsed.KeyVersion);
            encryptionKey = Derive(masterKey);
            binding = CreateBinding(context);
            ciphertextBuffer = new byte[parsed.ChunkSize];
            plaintextBuffer = new byte[parsed.ChunkSize];
            associatedData =
                new byte[binding.Length + header.Length + sizeof(uint)];
            binding.CopyTo(associatedData, 0);
            header.CopyTo(associatedData, binding.Length);
            parsed.NoncePrefix.CopyTo(nonce, 0);

            long read = header.Length;
            long remaining = parsed.PlaintextLength;
            uint chunkIndex = 0;
            using AesGcm aes = new(encryptionKey, TagLength);
            while (remaining > 0)
            {
                int chunkLength = (int)Math.Min(parsed.ChunkSize, remaining);
                await encrypted.ReadExactlyAsync(
                    ciphertextBuffer.AsMemory(0, chunkLength),
                    cancellationToken).ConfigureAwait(false);
                await encrypted.ReadExactlyAsync(tag, cancellationToken)
                    .ConfigureAwait(false);
                BinaryPrimitives.WriteUInt32BigEndian(
                    nonce.AsSpan(NoncePrefixLength),
                    chunkIndex);
                BinaryPrimitives.WriteUInt32BigEndian(
                    associatedData.AsSpan(binding.Length + header.Length),
                    chunkIndex);
                aes.Decrypt(
                    nonce,
                    ciphertextBuffer.AsSpan(0, chunkLength),
                    tag,
                    plaintextBuffer.AsSpan(0, chunkLength),
                    associatedData);
                await plaintext.WriteAsync(
                    plaintextBuffer.AsMemory(0, chunkLength),
                    cancellationToken).ConfigureAwait(false);
                read += chunkLength + tag.Length;
                remaining -= chunkLength;
                chunkIndex = checked(chunkIndex + 1);
            }

            byte[] probe = new byte[1];
            try
            {
                if (await encrypted.ReadAsync(probe, cancellationToken)
                    .ConfigureAwait(false) != 0)
                {
                    throw Failure("artifact-envelope-invalid");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(probe);
            }

            return new DataRightsExportProtectionResult(
                FormatVersion,
                parsed.KeyVersion,
                parsed.PlaintextLength,
                read);
        }
        catch (DataRightsExportGenerationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is CryptographicException or EndOfStreamException)
        {
            throw Failure("artifact-authentication-failed", exception);
        }
        finally
        {
            Zero(
                header,
                masterKey,
                encryptionKey,
                binding,
                ciphertextBuffer,
                plaintextBuffer,
                tag,
                nonce,
                associatedData);
        }
    }

    private byte[] GetKey(int version)
    {
        if (version <= 0 ||
            !this.options.Keys.TryGetValue(version, out string? encodedKey) ||
            !DataRightsPseudonymisationOptionsValidator.TryDecode(
                encodedKey,
                out byte[] key))
        {
            throw Failure("encryption-key-unavailable");
        }

        return key;
    }

    private static byte[] Derive(byte[] masterKey) =>
        HMACSHA256.HashData(
            masterKey,
            Encoding.ASCII.GetBytes(EncryptionKeyDomain));

    private static byte[] CreateHeader(
        int keyVersion,
        int chunkSize,
        long plaintextLength,
        byte[] noncePrefix)
    {
        byte[] header = new byte[HeaderLength];
        Magic.CopyTo(header, 0);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(8), FormatVersion);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(12), keyVersion);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16), chunkSize);
        BinaryPrimitives.WriteInt64BigEndian(header.AsSpan(20), plaintextLength);
        noncePrefix.CopyTo(header, 28);
        return header;
    }

    private static Header ParseHeader(byte[] header)
    {
        if (header.Length != HeaderLength ||
            !header.AsSpan(0, Magic.Length).SequenceEqual(Magic) ||
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(8)) !=
                FormatVersion)
        {
            throw Failure("artifact-envelope-invalid");
        }

        int keyVersion =
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(12));
        int chunkSize =
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16));
        long plaintextLength =
            BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(20));
        return new Header(
            keyVersion,
            chunkSize,
            plaintextLength,
            header.AsSpan(28, NoncePrefixLength).ToArray());
    }

    private static byte[] CreateBinding(
        DataRightsExportProtectionContext context)
    {
        byte[] tenantBytes =
            Encoding.UTF8.GetBytes(context.TenantId.Trim().ToLowerInvariant());
        byte[] tenantDigest = SHA256.HashData(tenantBytes);
        try
        {
            StringBuilder canonical = new();
            Append(canonical, BindingDomain);
            Append(canonical, FormatVersion.ToString(CultureInfo.InvariantCulture));
            Append(canonical, context.ArtifactId.ToString("N"));
            Append(canonical, Convert.ToHexStringLower(tenantDigest));
            Append(canonical, context.CaseId.ToString("N"));
            Append(canonical, ((int)context.CaseType).ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, context.PropertyId?.ToString("N") ?? "tenant");
            Append(canonical, context.DecisionRevision.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, context.ExpiresAtUtc.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture));
            return SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        }
        finally
        {
            Zero(tenantBytes, tenantDigest);
        }
    }

    private static void ValidateContext(
        DataRightsExportProtectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        bool scopeValid = context.CaseType switch
        {
            DataRightsCaseType.GuestRights =>
                context.PropertyId is Guid propertyId &&
                propertyId != Guid.Empty,
            DataRightsCaseType.StaffRights => context.PropertyId is null,
            _ => false
        };
        if (context.ArtifactId == Guid.Empty ||
            string.IsNullOrWhiteSpace(context.TenantId) ||
            context.CaseId == Guid.Empty ||
            context.DecisionRevision <= 0 ||
            context.ExpiresAtUtc == default ||
            !scopeValid)
        {
            throw Failure("generation-coordinate-invalid");
        }
    }

    private static void ValidateStreams(
        Stream source,
        Stream destination,
        bool protect)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (!source.CanRead || !destination.CanWrite)
        {
            throw new ArgumentException(
                protect
                    ? "Plaintext must be readable and encrypted output writable."
                    : "Encrypted input must be readable and plaintext output writable.");
        }
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static DataRightsExportGenerationException Failure(
        string code,
        Exception? inner = null) =>
        inner is null
            ? new DataRightsExportGenerationException(code)
            : new DataRightsExportGenerationException(code, inner);

    private static void Zero(params byte[][] buffers)
    {
        foreach (byte[] buffer in buffers)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private sealed record Header(
        int KeyVersion,
        int ChunkSize,
        long PlaintextLength,
        byte[] NoncePrefix);

}
