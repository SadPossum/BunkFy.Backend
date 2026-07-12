namespace BunkFy.Adapter.Abstractions;

using System.Security.Cryptography;

public static class AdapterPayloadHash
{
    public static string ComputeSha256(ReadOnlySpan<byte> payload) =>
        Convert.ToHexStringLower(SHA256.HashData(payload));
}
