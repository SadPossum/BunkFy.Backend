namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface IAdapterIngressTokenService
{
    AdapterIngressTokenIssue Issue(Guid credentialId);
    bool TryResolve(string token, out Guid credentialId, out byte[] candidateHash);
    bool Verify(ReadOnlySpan<byte> expectedHash, ReadOnlySpan<byte> candidateHash);
}

public sealed record AdapterIngressTokenIssue(
    string Token,
    string HashAlgorithm,
    byte[] SecretHash);
