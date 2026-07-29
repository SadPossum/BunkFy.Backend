namespace BunkFy.Modules.DataRights.Domain.ValueObjects;

using BunkFy.Modules.DataRights.Domain.Errors;
using Gma.Framework.Results;

public sealed record DataRightsApprovalEvidenceBinding
{
    private DataRightsApprovalEvidenceBinding(
        string key,
        long version,
        string sha256)
    {
        this.Key = key;
        this.Version = version;
        this.Sha256 = sha256;
    }

    public string Key { get; }

    public long Version { get; }

    public string Sha256 { get; }

    public static Result<DataRightsApprovalEvidenceBinding> Create(
        string key,
        long version,
        string sha256)
    {
        string normalizedKey = key?.Trim().ToLowerInvariant() ?? string.Empty;
        string digest = sha256?.Trim().ToLowerInvariant() ?? string.Empty;
        return IsKey(normalizedKey) &&
            version >= 0 &&
            IsSha256(digest)
            ? Result.Success(
                new DataRightsApprovalEvidenceBinding(
                    normalizedKey,
                    version,
                    digest))
            : Result.Failure<DataRightsApprovalEvidenceBinding>(
                DataRightsDomainErrors.ApprovalPolicyEvidenceInvalid);
    }

    internal static bool IsKey(string value) =>
        value.Length is > 0 and <=
            DataRightsApprovalPolicyEvidence.KeyMaxLength &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character =>
            character is (>= 'a' and <= 'z') or
                (>= '0' and <= '9') or '.' or '-' or '_');

    internal static bool IsSha256(string value) =>
        value.Length ==
            DataRightsApprovalPolicyEvidence.ContentSha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
