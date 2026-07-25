namespace BunkFy.Modules.DataRights.Domain.Models;

using BunkFy.Modules.DataRights.Domain.Entities;

public sealed record DataRightsRestoreCursor(
    long TenantSequence,
    string EntrySha256)
{
    public static readonly DataRightsRestoreCursor Genesis = new(
        TenantSequence: 0,
        DataRightsProcessingLedgerEntry.GenesisEntrySha256);

    public bool HasValidShape() =>
        this.TenantSequence >= 0 &&
        this.EntrySha256 is not null &&
        this.EntrySha256.Length == DataRightsProcessingLedgerEntry.Sha256Length &&
        this.EntrySha256.All(Uri.IsHexDigit) &&
        this.TenantSequence == 0 ==
        string.Equals(
            this.EntrySha256,
            DataRightsProcessingLedgerEntry.GenesisEntrySha256,
            StringComparison.Ordinal);
}
