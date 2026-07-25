namespace BunkFy.Modules.DataRights.Application.Ports;

public interface IDataRightsRestoreScopeSource
{
    Task<DataRightsRestoreScopeSourceReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken);

    Task<DataRightsRestoreScopeSnapshot> OpenSnapshotAsync(
        CancellationToken cancellationToken);

    Task<DataRightsRestoreScopePage> ReadScopesAsync(
        DataRightsRestoreScopeSnapshot snapshot,
        string? afterScopeId,
        int pageSize,
        CancellationToken cancellationToken);

    Task<bool> IsCurrentAsync(
        DataRightsRestoreScopeSnapshot snapshot,
        CancellationToken cancellationToken);
}

public sealed record DataRightsRestoreScopeSnapshot(
    int ContractVersion,
    string SnapshotSha256,
    DateTimeOffset OpenedAtUtc)
{
    public const int CurrentContractVersion = 1;
}

public sealed record DataRightsRestoreScope(
    int ContractVersion,
    string ScopeId,
    DataRightsLedgerDeltaCheckpoint TrustedCheckpoint)
{
    public const int CurrentContractVersion = 1;
}

public sealed record DataRightsRestoreScopePage(
    int ContractVersion,
    IReadOnlyList<DataRightsRestoreScope> Scopes,
    string? NextScopeId,
    bool HasMore)
{
    public const int CurrentContractVersion = 1;
}

public sealed record DataRightsRestoreScopeSourceReadiness(
    string Provider,
    bool IsReady,
    bool IsProductionGrade,
    string? FailureCode);

public sealed class DataRightsRestoreScopeSourceException : Exception
{
    public const string SnapshotChangedCode =
        "data-rights.restore-scope-source.snapshot-changed";

    public DataRightsRestoreScopeSourceException(string code, string message)
        : base(message)
        => this.Code = code;

    public DataRightsRestoreScopeSourceException(
        string code,
        string message,
        Exception innerException)
        : base(message, innerException)
        => this.Code = code;

    public string Code { get; }
}
