namespace BunkFy.Extensions.DataRights.Organizations;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.Organizations.Application.Ports;

internal sealed partial class OrganizationsTenantTerminationContributor
{
    private static readonly OrganizationScopeExportStore[] ExportStores =
    [
        OrganizationScopeExportStore.Organization,
        OrganizationScopeExportStore.Memberships,
        OrganizationScopeExportStore.Invitations,
        OrganizationScopeExportStore.EnrollmentLinks,
        OrganizationScopeExportStore.EnrollmentClaims
    ];

    public async Task<TenantTerminationContributionResult> ExportAsync(
        TenantTerminationExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        DateTimeOffset startedAtUtc = clock.UtcNow;
        if (!IsValid(
                request,
                scopeContext,
                startedAtUtc,
                out Guid organizationId))
        {
            return Failed(
                "organizations.termination.export-request-invalid",
                startedAtUtc);
        }

        WorkspaceTerminationFenceSnapshot? selectedFence =
            await this.ReadFenceAsync(cancellationToken)
                .ConfigureAwait(false);
        if (!Matches(request, selectedFence))
        {
            return Retry(
                "organizations.termination.export-fence-unavailable",
                clock.UtcNow);
        }

        OrganizationScopeSnapshot selected = await lifecycle.GetSnapshotAsync(
                organizationId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!TrySelectExportRevision(
                selected,
                out long selectedRevision,
                out bool isMissing))
        {
            return selected?.Status switch
            {
                OrganizationScopeStatus.Closed => Failed(
                    "organizations.termination.export-scope-closed",
                    clock.UtcNow),
                _ => Failed(
                    "organizations.termination.export-scope-invalid",
                    clock.UtcNow)
            };
        }

        ScopeExportResult export = isMissing
            ? ScopeExportResult.Success(0)
            : await this.ExportStoresAsync(
                    organizationId,
                    selectedRevision,
                    request.Contribution.DeadlineUtc,
                    sink,
                    cancellationToken)
                .ConfigureAwait(false);
        if (export.Status == ScopeExportStatus.RetryRequired)
        {
            return Retry(export.Code, clock.UtcNow);
        }

        if (export.Status != ScopeExportStatus.Completed)
        {
            return Failed(export.Code, clock.UtcNow);
        }

        OrganizationScopeSnapshot resulting =
            await lifecycle.GetSnapshotAsync(
                    organizationId,
                    cancellationToken)
                .ConfigureAwait(false);
        WorkspaceTerminationFenceSnapshot? resultingFence =
            await this.ReadFenceAsync(cancellationToken)
                .ConfigureAwait(false);
        bool scopeStable = isMissing
            ? resulting is
            {
                Status: OrganizationScopeStatus.Missing,
                Revision: 0
            }
            : resulting is
            {
                Status: OrganizationScopeStatus.Open
            } && resulting.Revision == selectedRevision;
        if (!scopeStable ||
            !Matches(request, resultingFence) ||
            resultingFence!.Version != selectedFence!.Version)
        {
            return Retry(
                "organizations.termination.export-revision-changed",
                clock.UtcNow);
        }

        DateTimeOffset completedAtUtc = clock.UtcNow;
        if (completedAtUtc > request.Contribution.DeadlineUtc)
        {
            return Retry(
                "organizations.termination.export-deadline-expired",
                completedAtUtc);
        }

        return Completed(
            "organizations.termination.exported",
            export.RecordCount,
            selectedRevision,
            resulting.Revision,
            completedAtUtc);
    }

    private async Task<ScopeExportResult> ExportStoresAsync(
        Guid organizationId,
        long selectedRevision,
        DateTimeOffset deadlineUtc,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        long count = 0;
        foreach (OrganizationScopeExportStore store in ExportStores)
        {
            string? cursor = null;
            while (true)
            {
                if (clock.UtcNow > deadlineUtc)
                {
                    return ScopeExportResult.Retry(
                        "organizations.termination.export-deadline-expired");
                }

                OrganizationScopeExportPage page = await lifecycle.ExportAsync(
                        new OrganizationScopeExportRequest(
                            organizationId,
                            selectedRevision,
                            store,
                            cursor,
                            OrganizationScopeLifecycleLimits.MaximumPageSize),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (page is null)
                {
                    return ScopeExportResult.Failed(
                        "organizations.termination.export-response-invalid");
                }

                if (page.Status != OrganizationScopeExportStatus.Completed)
                {
                    return page.Status switch
                    {
                        OrganizationScopeExportStatus.Missing or
                        OrganizationScopeExportStatus.Closed or
                        OrganizationScopeExportStatus.Stale =>
                            ScopeExportResult.Retry(
                                "organizations.termination.export-revision-changed"),
                        _ => ScopeExportResult.Failed(
                            "organizations.termination.export-response-invalid")
                    };
                }

                if (!IsValidPage(page, store, selectedRevision, cursor))
                {
                    return ScopeExportResult.Failed(
                        "organizations.termination.export-response-invalid");
                }

                try
                {
                    foreach (OrganizationScopeExportRecord record in
                             page.Records)
                    {
                        await sink.WriteAsync(
                                Map(organizationId, store, record),
                                cancellationToken)
                            .ConfigureAwait(false);
                        count = checked(count + 1);
                        if (count >
                            TenantTerminationExportContract
                                .MaximumRecordsPerFragment)
                        {
                            return ScopeExportResult.Failed(
                                "organizations.termination.export-volume-exceeded");
                        }
                    }
                }
                catch (InvalidDataException)
                {
                    return ScopeExportResult.Failed(
                        "organizations.termination.export-record-invalid");
                }
                catch (OverflowException)
                {
                    return ScopeExportResult.Failed(
                        "organizations.termination.export-volume-exceeded");
                }

                if (!page.HasMore)
                {
                    break;
                }

                cursor = page.NextCursor;
            }
        }

        return ScopeExportResult.Success(count);
    }

    private static bool IsValidPage(
        OrganizationScopeExportPage page,
        OrganizationScopeExportStore store,
        long selectedRevision,
        string? cursor)
    {
        if (page.ScopeRevision != selectedRevision ||
            page.Store != store ||
            page.Records is null ||
            page.Records.Count >
                OrganizationScopeLifecycleLimits.MaximumPageSize ||
            (page.NextCursor is not null &&
             (page.NextCursor.Length is <= 0 or >
                OrganizationScopeLifecycleLimits.MaximumCursorLength ||
              page.NextCursor.Any(char.IsControl))))
        {
            return false;
        }

        if (page.Records.Count == 0)
        {
            return !page.HasMore &&
                string.Equals(
                    page.NextCursor,
                    cursor,
                    StringComparison.Ordinal);
        }

        return page.NextCursor is not null &&
            !string.Equals(
                page.NextCursor,
                cursor,
                StringComparison.Ordinal) &&
            (!page.HasMore ||
             page.Records.Count ==
                OrganizationScopeLifecycleLimits.MaximumPageSize);
    }

    private static bool TrySelectExportRevision(
        OrganizationScopeSnapshot? snapshot,
        out long revision,
        out bool isMissing)
    {
        revision = 0;
        isMissing = false;
        if (snapshot is null)
        {
            return false;
        }

        if (snapshot.Status == OrganizationScopeStatus.Missing &&
            snapshot.Revision == 0)
        {
            isMissing = true;
            return true;
        }

        if (snapshot.Status != OrganizationScopeStatus.Open ||
            snapshot.Revision is < 0 or long.MaxValue)
        {
            return false;
        }

        revision = snapshot.Revision;
        return true;
    }

    private enum ScopeExportStatus
    {
        Failed = 0,
        Completed = 1,
        RetryRequired = 2
    }

    private sealed record ScopeExportResult(
        ScopeExportStatus Status,
        string Code,
        long RecordCount)
    {
        public static ScopeExportResult Success(long count) =>
            new(ScopeExportStatus.Completed, string.Empty, count);

        public static ScopeExportResult Retry(string code) =>
            new(ScopeExportStatus.RetryRequired, code, 0);

        public static ScopeExportResult Failed(string code) =>
            new(ScopeExportStatus.Failed, code, 0);
    }
}
