namespace BunkFy.Modules.Ingestion.Domain.Credentials;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Domain.Errors;

public sealed class AdapterIngressCredential : ScopedAggregateRoot<Guid>
{
    public const int LabelMaxLength = 100;
    public const int ActorMaxLength = 200;
    public const int HashAlgorithmMaxLength = 32;
    public const int SecretHashLength = 32;
    public const int MaximumActiveCredentialsPerConnection = 5;
    public const string Sha256HashAlgorithm = "sha256-v1";
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(90);
    public static readonly TimeSpan MinimumLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromDays(366);

    private AdapterIngressCredential() { }

    private AdapterIngressCredential(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid ConnectionId { get; private set; }
    public int Slot { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string SecretHashAlgorithm { get; private set; } = string.Empty;
    public byte[] SecretHash { get; private set; } = [];
    public AdapterIngressCredentialState State { get; private set; } = AdapterIngressCredentialState.Active;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string? RevokedBy { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public DateTimeOffset? LastAuthenticatedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<AdapterIngressCredential> Create(
        Guid id,
        string scopeId,
        Guid connectionId,
        int slot,
        string label,
        string secretHashAlgorithm,
        byte[] secretHash,
        DateTimeOffset expiresAtUtc,
        string createdBy,
        DateTimeOffset nowUtc)
    {
        string normalizedLabel = label?.Trim() ?? string.Empty;
        string normalizedAlgorithm = secretHashAlgorithm?.Trim().ToLowerInvariant() ?? string.Empty;
        string normalizedActor = createdBy?.Trim() ?? string.Empty;
        if (id == Guid.Empty || connectionId == Guid.Empty || string.IsNullOrWhiteSpace(scopeId) ||
            slot is <= 0 or > MaximumActiveCredentialsPerConnection)
        {
            return Result.Failure<AdapterIngressCredential>(IngestionDomainErrors.IngressCredentialIdentityInvalid);
        }

        if (normalizedLabel.Length is 0 or > LabelMaxLength)
        {
            return Result.Failure<AdapterIngressCredential>(IngestionDomainErrors.IngressCredentialLabelInvalid);
        }

        if (normalizedAlgorithm != Sha256HashAlgorithm || secretHash is null || secretHash.Length != SecretHashLength)
        {
            return Result.Failure<AdapterIngressCredential>(IngestionDomainErrors.IngressCredentialSecretInvalid);
        }

        if (normalizedActor.Length is 0 or > ActorMaxLength)
        {
            return Result.Failure<AdapterIngressCredential>(IngestionDomainErrors.IngressCredentialActorInvalid);
        }

        TimeSpan lifetime = expiresAtUtc - nowUtc;
        if (lifetime < MinimumLifetime || lifetime > MaximumLifetime)
        {
            return Result.Failure<AdapterIngressCredential>(IngestionDomainErrors.IngressCredentialExpiryInvalid);
        }

        return Result.Success(new AdapterIngressCredential(id, scopeId.Trim())
        {
            ConnectionId = connectionId,
            Slot = slot,
            Label = normalizedLabel,
            SecretHashAlgorithm = normalizedAlgorithm,
            SecretHash = secretHash.ToArray(),
            ExpiresAtUtc = expiresAtUtc,
            CreatedBy = normalizedActor,
            CreatedAtUtc = nowUtc
        });
    }

    public bool CanAuthenticate(DateTimeOffset nowUtc) =>
        this.State == AdapterIngressCredentialState.Active && this.ExpiresAtUtc > nowUtc;

    public Result Revoke(long expectedVersion, string revokedBy, DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(IngestionDomainErrors.VersionConflict);
        }

        if (this.State == AdapterIngressCredentialState.Revoked)
        {
            return Result.Failure(IngestionDomainErrors.IngressCredentialAlreadyRevoked);
        }

        string normalizedActor = revokedBy?.Trim() ?? string.Empty;
        if (normalizedActor.Length is 0 or > ActorMaxLength)
        {
            return Result.Failure(IngestionDomainErrors.IngressCredentialActorInvalid);
        }

        this.State = AdapterIngressCredentialState.Revoked;
        this.RevokedBy = normalizedActor;
        this.RevokedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }
}
