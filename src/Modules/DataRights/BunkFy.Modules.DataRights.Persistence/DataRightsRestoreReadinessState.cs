namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;

internal sealed class DataRightsRestoreReadinessState
    : IDataRightsRestoreReadiness
{
    private DataRightsRestoreReadinessSnapshot snapshot = new(
        IsReady: false,
        StatusCode: "data-rights.restore.starting",
        LastVerifiedAtUtc: null,
        SnapshotSha256: null);

    public DataRightsRestoreReadinessSnapshot Snapshot =>
        Volatile.Read(ref this.snapshot);

    public void MarkStarting() =>
        Volatile.Write(
            ref this.snapshot,
            new(
                IsReady: false,
                StatusCode: "data-rights.restore.reconciling",
                LastVerifiedAtUtc: null,
                SnapshotSha256: null));

    public void MarkReady(
        DateTimeOffset verifiedAtUtc,
        string snapshotSha256) =>
        Volatile.Write(
            ref this.snapshot,
            new(
                IsReady: true,
                StatusCode: "data-rights.restore.ready",
                verifiedAtUtc.ToUniversalTime(),
                snapshotSha256));

    public void MarkFailed(string statusCode) =>
        Volatile.Write(
            ref this.snapshot,
            new(
                IsReady: false,
                StatusCode: statusCode,
                LastVerifiedAtUtc: null,
                SnapshotSha256: null));
}
