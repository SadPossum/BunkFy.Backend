namespace BunkFy.Modules.Ingestion.Application.Handlers;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

internal static class IngestionMutationFingerprint
{
    public static string Compute(params string[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (string value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
