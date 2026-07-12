namespace BunkFy.Modules.Ingestion.Tests.Domain;

using BunkFy.Modules.Ingestion.Domain.Credentials;
using BunkFy.Modules.Ingestion.Domain.Errors;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterIngressCredentialTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Credential_enforces_bounded_expiry_and_copies_its_digest()
    {
        byte[] hash = Enumerable.Repeat((byte)7, AdapterIngressCredential.SecretHashLength).ToArray();
        var created = AdapterIngressCredential.Create(
            Guid.NewGuid(), "tenant-a", Guid.NewGuid(), 1, " booking webhook ",
            AdapterIngressCredential.Sha256HashAlgorithm, hash,
            Now.AddDays(30), "user:operator", Now);

        Assert.True(created.IsSuccess);
        hash[0] = 99;
        Assert.Equal(7, created.Value.SecretHash[0]);
        Assert.Equal("booking webhook", created.Value.Label);
        Assert.True(created.Value.CanAuthenticate(Now.AddDays(29)));
        Assert.False(created.Value.CanAuthenticate(Now.AddDays(30)));
        Assert.Equal(IngestionDomainErrors.IngressCredentialExpiryInvalid,
            AdapterIngressCredential.Create(
                Guid.NewGuid(), "tenant-a", Guid.NewGuid(), 1, "short",
                AdapterIngressCredential.Sha256HashAlgorithm,
                new byte[AdapterIngressCredential.SecretHashLength],
                Now.AddMinutes(4), "user:operator", Now).Error);
    }

    [Fact]
    public void Revocation_is_optimistic_terminal_and_preserves_audit_actor()
    {
        AdapterIngressCredential credential = AdapterIngressCredential.Create(
            Guid.NewGuid(), "tenant-a", Guid.NewGuid(), 1, "mailbox",
            AdapterIngressCredential.Sha256HashAlgorithm,
            new byte[AdapterIngressCredential.SecretHashLength],
            Now.AddDays(30), "user:creator", Now).Value;

        Assert.Equal(IngestionDomainErrors.VersionConflict,
            credential.Revoke(2, "user:operator", Now.AddMinutes(1)).Error);
        Assert.True(credential.Revoke(1, " user:operator ", Now.AddMinutes(1)).IsSuccess);
        Assert.False(credential.CanAuthenticate(Now.AddMinutes(2)));
        Assert.Equal("user:operator", credential.RevokedBy);
        Assert.Equal(2, credential.Version);
        Assert.Equal(IngestionDomainErrors.IngressCredentialAlreadyRevoked,
            credential.Revoke(2, "user:operator", Now.AddMinutes(2)).Error);
    }
}
