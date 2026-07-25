namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;

internal sealed class MissingDataRightsRestoreScopeSource
    : IDataRightsRestoreScopeSource
{
    private const string ProviderName = "unavailable";
    private const string FailureCode =
        "data-rights.restore-scope-source.unavailable";

    public Task<DataRightsRestoreScopeSourceReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(new DataRightsRestoreScopeSourceReadiness(
            ProviderName,
            IsReady: false,
            IsProductionGrade: false,
            FailureCode));

    public Task<DataRightsRestoreScopeSnapshot> OpenSnapshotAsync(
        CancellationToken cancellationToken) =>
        Task.FromException<DataRightsRestoreScopeSnapshot>(Unavailable());

    public Task<DataRightsRestoreScopePage> ReadScopesAsync(
        DataRightsRestoreScopeSnapshot snapshot,
        string? afterScopeId,
        int pageSize,
        CancellationToken cancellationToken) =>
        Task.FromException<DataRightsRestoreScopePage>(Unavailable());

    public Task<bool> IsCurrentAsync(
        DataRightsRestoreScopeSnapshot snapshot,
        CancellationToken cancellationToken) =>
        Task.FromException<bool>(Unavailable());

    private static DataRightsRestoreScopeSourceException Unavailable() =>
        new(
            FailureCode,
            "The data-rights restore scope source is unavailable.");
}
