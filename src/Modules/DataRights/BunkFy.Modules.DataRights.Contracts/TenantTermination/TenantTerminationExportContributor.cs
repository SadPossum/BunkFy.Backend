namespace BunkFy.Modules.DataRights.Contracts;

/// <summary>
/// Streams one module-owned tenant-termination export fragment. Data Rights
/// owns protection and storage, and must discard every record written to the
/// sink unless the returned contribution is complete and matches the requested
/// frozen revision.
/// </summary>
public interface ITenantTerminationExportContributor
{
    DataRightsExportDescriptor ExportDescriptor { get; }

    Task<TenantTerminationContributionResult> ExportAsync(
        TenantTerminationExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken);
}

public sealed record TenantTerminationExportRequest(
    TenantTerminationContributionRequest Contribution,
    long FreezeOperationRevision,
    long WorkspaceFenceRevision,
    string FrozenRevisionSha256,
    DateTimeOffset FrozenAtUtc);

public static class TenantTerminationExportContract
{
    public const int FormatVersion = 1;
    public const int MaximumRecordsPerFragment = 1_000_000;
}
