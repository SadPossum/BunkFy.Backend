namespace BunkFy.Modules.Ingestion.Application.Credentials;

using System.Security.Cryptography;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Credentials;

internal sealed class AdapterIngressTokenService : IAdapterIngressTokenService
{
    private const string Prefix = "bfi_v1_";
    private const int SecretBytes = 32;

    public AdapterIngressTokenIssue Issue(Guid credentialId)
    {
        if (credentialId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty credential id is required.", nameof(credentialId));
        }

        byte[] secret = RandomNumberGenerator.GetBytes(SecretBytes);
        try
        {
            byte[] hash = SHA256.HashData(secret);
            string token = $"{Prefix}{credentialId:N}_{Base64UrlEncode(secret)}";
            return new AdapterIngressTokenIssue(
                token,
                AdapterIngressCredential.Sha256HashAlgorithm,
                hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public bool TryResolve(string token, out Guid credentialId, out byte[] candidateHash)
    {
        credentialId = Guid.Empty;
        candidateHash = [];
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128 ||
            !token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> remainder = token.AsSpan(Prefix.Length);
        int separator = remainder.IndexOf('_');
        if (separator != 32 ||
            !Guid.TryParseExact(remainder[..separator], "N", out credentialId) ||
            !remainder[..separator].SequenceEqual(credentialId.ToString("N")))
        {
            credentialId = Guid.Empty;
            return false;
        }

        ReadOnlySpan<char> encodedSecret = remainder[(separator + 1)..];
        if (encodedSecret.Length != 43 || !IsBase64Url(encodedSecret))
        {
            credentialId = Guid.Empty;
            return false;
        }

        byte[] secret;
        try
        {
            secret = Base64UrlDecode(encodedSecret);
        }
        catch (FormatException)
        {
            credentialId = Guid.Empty;
            return false;
        }

        try
        {
            if (secret.Length != SecretBytes ||
                !encodedSecret.SequenceEqual(Base64UrlEncode(secret)))
            {
                credentialId = Guid.Empty;
                return false;
            }

            candidateHash = SHA256.HashData(secret);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public bool Verify(ReadOnlySpan<byte> expectedHash, ReadOnlySpan<byte> candidateHash) =>
        expectedHash.Length == AdapterIngressCredential.SecretHashLength &&
        candidateHash.Length == AdapterIngressCredential.SecretHashLength &&
        CryptographicOperations.FixedTimeEquals(expectedHash, candidateHash);

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(ReadOnlySpan<char> value)
    {
        string encoded = value.ToString().Replace('-', '+').Replace('_', '/');
        encoded = encoded.PadRight(encoded.Length + ((4 - (encoded.Length % 4)) % 4), '=');
        return Convert.FromBase64String(encoded);
    }

    private static bool IsBase64Url(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }
}
