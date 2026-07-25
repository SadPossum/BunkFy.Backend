namespace BunkFy.Modules.DataRights.Application.Ports;

public interface IDataRightsRestoreReadiness
{
    DataRightsRestoreReadinessSnapshot Snapshot { get; }
}

public sealed record DataRightsRestoreReadinessSnapshot(
    bool IsReady,
    string StatusCode,
    DateTimeOffset? LastVerifiedAtUtc,
    string? SnapshotSha256);
