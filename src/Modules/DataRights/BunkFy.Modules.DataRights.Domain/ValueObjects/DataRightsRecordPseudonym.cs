namespace BunkFy.Modules.DataRights.Domain.ValueObjects;

using BunkFy.Modules.DataRights.Domain.Errors;
using Gma.Framework.Results;

public sealed record DataRightsRecordPseudonym
{
    public const int Sha256Length = 64;

    private DataRightsRecordPseudonym(int keyVersion, string sha256)
    {
        this.KeyVersion = keyVersion;
        this.Sha256 = sha256;
    }

    public int KeyVersion { get; }
    public string Sha256 { get; }

    public static Result<DataRightsRecordPseudonym> Create(
        int keyVersion,
        string sha256)
    {
        string digest = sha256?.Trim().ToLowerInvariant() ?? string.Empty;
        return keyVersion > 0 && IsSha256(digest)
            ? Result.Success(new DataRightsRecordPseudonym(keyVersion, digest))
            : Result.Failure<DataRightsRecordPseudonym>(
                DataRightsDomainErrors.RecordPseudonymInvalid);
    }

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
