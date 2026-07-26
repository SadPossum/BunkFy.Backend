namespace BunkFy.Modules.DataRights.Application;

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Domain.Entities;

internal static class DataRightsExecutionIdentity
{
    private const string WorkItemDomain =
        "bunkfy.data-rights.execution-work-item.v1";

    public static Guid CreateWorkItemIdempotencyKey(
        Guid batchIdempotencyKey,
        DataRightsSubjectCoordinate subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, WorkItemDomain);
        Append(hash, batchIdempotencyKey.ToString("N"));
        Append(hash, subject.OwnerKey);
        Append(hash, subject.RecordType);
        Append(hash, subject.RecordId.ToString("N"));
        Append(hash, subject.RecordVersion.ToString(CultureInfo.InvariantCulture));
        byte[] digest = hash.GetHashAndReset();
        try
        {
            return new Guid(digest.AsSpan(0, 16));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static void Append(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        try
        {
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
