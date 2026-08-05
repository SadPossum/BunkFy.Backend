namespace BunkFy.Modules.DataRights.Contracts;

using System.Security.Cryptography;
using System.Text;

public static class DataRightsExportRecordIds
{
    public static Guid CreateDeterministicChild(
        Guid namespaceId,
        string discriminator)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(namespaceId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(discriminator);

        byte[] namespaceBytes = namespaceId.ToByteArray();
        SwapByteOrder(namespaceBytes);
        byte[] discriminatorBytes = Encoding.UTF8.GetBytes(discriminator);
        byte[] input = new byte[
            namespaceBytes.Length + discriminatorBytes.Length];
        byte[]? hash = null;
        try
        {
            namespaceBytes.CopyTo(input, 0);
            discriminatorBytes.CopyTo(input, namespaceBytes.Length);

            hash = SHA256.HashData(input);
            hash[6] = (byte)((hash[6] & 0x0f) | 0x80);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
            Span<byte> guidBytes = hash.AsSpan(0, 16);
            SwapByteOrder(guidBytes);
            return new Guid(guidBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(namespaceBytes);
            CryptographicOperations.ZeroMemory(discriminatorBytes);
            CryptographicOperations.ZeroMemory(input);
            if (hash is not null)
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }
    }

    private static void SwapByteOrder(Span<byte> bytes)
    {
        (bytes[0], bytes[3]) = (bytes[3], bytes[0]);
        (bytes[1], bytes[2]) = (bytes[2], bytes[1]);
        (bytes[4], bytes[5]) = (bytes[5], bytes[4]);
        (bytes[6], bytes[7]) = (bytes[7], bytes[6]);
    }
}
