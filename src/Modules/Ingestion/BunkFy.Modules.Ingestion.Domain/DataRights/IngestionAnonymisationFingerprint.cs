namespace BunkFy.Modules.Ingestion.Domain.DataRights;

using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class IngestionAnonymisationFingerprint : ScopedEntity<Guid>
{
    public const int Sha256Length = 64;

    private IngestionAnonymisationFingerprint() { }

    private IngestionAnonymisationFingerprint(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid TombstoneId { get; private set; }
    public IngestionAnonymisationFingerprintPurpose Purpose { get; private set; }
    public int KeyVersion { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<IngestionAnonymisationFingerprint> Create(
        Guid id,
        string tenantId,
        Guid tombstoneId,
        IngestionAnonymisationFingerprintPurpose purpose,
        int keyVersion,
        string sha256,
        DateTimeOffset createdAtUtc)
    {
        string digest = sha256?.Trim().ToLowerInvariant() ?? string.Empty;
        if (id == Guid.Empty ||
            tombstoneId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            !Enum.IsDefined(purpose) ||
            purpose == IngestionAnonymisationFingerprintPurpose.Unknown ||
            keyVersion <= 0 ||
            createdAtUtc == default ||
            !IsSha256(digest))
        {
            return Result.Failure<IngestionAnonymisationFingerprint>(
                IngestionDomainErrors.AnonymisationFingerprintInvalid);
        }

        return Result.Success(new IngestionAnonymisationFingerprint(id, scopeId)
        {
            TombstoneId = tombstoneId,
            Purpose = purpose,
            KeyVersion = keyVersion,
            Sha256 = digest,
            CreatedAtUtc = createdAtUtc.ToUniversalTime()
        });
    }

    public bool Matches(
        IngestionAnonymisationFingerprintPurpose purpose,
        int keyVersion,
        string sha256) =>
        this.Purpose == purpose &&
        this.KeyVersion == keyVersion &&
        string.Equals(
            this.Sha256,
            sha256?.Trim(),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}

public enum IngestionAnonymisationFingerprintPurpose
{
    Unknown = 0,
    SourceLink = 1,
    ObservationReceipt = 2
}
