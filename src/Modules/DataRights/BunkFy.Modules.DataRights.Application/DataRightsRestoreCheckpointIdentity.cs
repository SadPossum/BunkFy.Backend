namespace BunkFy.Modules.DataRights.Application;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

internal static class DataRightsRestoreCheckpointIdentity
{
    private const string Domain =
        "bunkfy.data-rights.restore-checkpoint.v1";

    public static Guid Create(string scopeId)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] domainBytes = Encoding.UTF8.GetBytes(Domain);
        byte[] scopeBytes = Encoding.UTF8.GetBytes(scopeId);
        Span<byte> length = stackalloc byte[sizeof(int)];
        try
        {
            BinaryPrimitives.WriteInt32BigEndian(length, domainBytes.Length);
            hash.AppendData(length);
            hash.AppendData(domainBytes);
            BinaryPrimitives.WriteInt32BigEndian(length, scopeBytes.Length);
            hash.AppendData(length);
            hash.AppendData(scopeBytes);
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
        finally
        {
            CryptographicOperations.ZeroMemory(domainBytes);
            CryptographicOperations.ZeroMemory(scopeBytes);
        }
    }
}
