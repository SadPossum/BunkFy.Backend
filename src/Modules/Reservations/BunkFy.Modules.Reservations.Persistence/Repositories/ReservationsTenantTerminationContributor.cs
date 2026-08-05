namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using System.Data;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal sealed partial class ReservationsTenantTerminationContributor(
    ReservationsDbContext dbContext,
    IScopeContext scopeContext,
    ISystemClock clock,
    IWorkspaceTerminationFenceReader? terminationFences = null)
    : ITenantTerminationContributor,
      ITenantTerminationExportContributor
{
    public TenantTerminationContributorDescriptor Descriptor { get; } = new(
        ReservationsTenantTerminationMetadata.OwnerKey,
        TenantTerminationContract.CurrentVersion,
        [
            new(
                TenantTerminationContributionPhase.Export,
                [ReservationsTenantTerminationMetadata.DependencyOwnerKey]),
            new(
                TenantTerminationContributionPhase.Destroy,
                [])
        ],
        MandatoryForProduction: true,
        ReservationsTenantTerminationMetadata.CatalogVersion,
        ReservationsTenantTerminationMetadata.CatalogSha256);

    public DataRightsExportDescriptor ExportDescriptor =>
        ReservationsTenantTerminationExportSchema.Descriptor;

    public Task<TenantTerminationContributionResult> ExecuteAsync(
        TenantTerminationContributionRequest request,
        CancellationToken cancellationToken) =>
        request?.Phase == TenantTerminationContributionPhase.Destroy
            ? this.DestroyAsync(request, cancellationToken)
            : Task.FromResult(Failed(
                "reservations.termination.export-requires-fragment-assembler",
                clock.UtcNow));

    public async Task<TenantTerminationContributionResult> ExportAsync(
        TenantTerminationExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        DateTimeOffset startedAtUtc = clock.UtcNow;
        if (!IsValid(request, scopeContext, startedAtUtc))
        {
            return Failed(
                "reservations.termination.export-request-invalid",
                startedAtUtc);
        }

        _ = TenantIds.TryNormalize(
            request.Contribution.TenantId,
            out string? tenantId);
        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is not null)
        {
            return Failed(
                "reservations.termination.export-transaction-conflict",
                startedAtUtc);
        }

        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database
                    .BeginTransactionAsync(
                        IsolationLevel.RepeatableRead,
                        cancellationToken)
                    .ConfigureAwait(false);
                await ReservationsTenantMutationLock.AcquireExclusiveAsync(
                    dbContext,
                    tenantId!,
                    cancellationToken).ConfigureAwait(false);
            }

            WorkspaceTerminationFenceSnapshot? selectedFence =
                await this.ReadFenceAsync(cancellationToken)
                    .ConfigureAwait(false);
            if (!Matches(request, selectedFence))
            {
                return RetryRequired(
                    "reservations.termination.export-fence-unavailable",
                    clock.UtcNow);
            }

            long selectedRevision = await this.GetOrCreateRevisionAsync(
                tenantId!,
                cancellationToken).ConfigureAwait(false);
            long recordCount = await this.ExportRecordsAsync(
                tenantId!,
                sink,
                cancellationToken).ConfigureAwait(false);

            dbContext.ChangeTracker.Clear();
            long? resultingRevision = await dbContext.TenantRevisions
                .AsNoTracking()
                .Select(revision => (long?)revision.Revision)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            WorkspaceTerminationFenceSnapshot? resultingFence =
                await this.ReadFenceAsync(cancellationToken)
                    .ConfigureAwait(false);
            if (resultingRevision != selectedRevision ||
                !Matches(request, resultingFence) ||
                resultingFence!.Version != selectedFence!.Version)
            {
                return RetryRequired(
                    "reservations.termination.export-revision-changed",
                    clock.UtcNow);
            }

            DateTimeOffset completedAtUtc = clock.UtcNow;
            if (completedAtUtc > request.Contribution.DeadlineUtc)
            {
                return RetryRequired(
                    "reservations.termination.export-deadline-expired",
                    completedAtUtc);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return new TenantTerminationContributionResult(
                TenantTerminationContributionStatus.Completed,
                "reservations.termination.exported",
                recordCount,
                RetainedMinimumCount: 0,
                RemainingActiveCount: 0,
                HoldReviewAtUtc: null,
                selectedRevision,
                resultingRevision,
                ReservationsTenantTerminationMetadata.CatalogVersion,
                ReservationsTenantTerminationMetadata.CatalogSha256,
                completedAtUtc);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<long> GetOrCreateRevisionAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        ReservationsTenantRevision? revision = await dbContext.TenantRevisions
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (revision is null)
        {
            revision = ReservationsTenantRevision.Create(tenantId);
            dbContext.TenantRevisions.Add(revision);
            await dbContext.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return revision.Revision;
    }

    private async Task<WorkspaceTerminationFenceSnapshot?> ReadFenceAsync(
        CancellationToken cancellationToken)
    {
        if (terminationFences is null)
        {
            return null;
        }

        try
        {
            return await terminationFences
                .GetCurrentAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private static bool Matches(
        TenantTerminationExportRequest request,
        WorkspaceTerminationFenceSnapshot? fence) =>
        fence is not null &&
        fence.ProcessId == request.Contribution.ProcessId &&
        fence.TerminationEpoch == request.Contribution.TerminationEpoch &&
        fence.State == WorkspaceTerminationFenceState.Frozen &&
        fence.Version == request.WorkspaceFenceRevision;

    private static bool IsValid(
        TenantTerminationExportRequest request,
        IScopeContext scope,
        DateTimeOffset nowUtc)
    {
        TenantTerminationContributionRequest contribution =
            request.Contribution;
        return contribution.ContractVersion ==
                TenantTerminationContract.CurrentVersion &&
            TenantIds.TryNormalize(
                contribution.TenantId,
                out string? tenantId) &&
            scope.IsEnabled &&
            string.Equals(scope.ScopeId, tenantId, StringComparison.Ordinal) &&
            contribution.ProcessId != Guid.Empty &&
            contribution.CaseId != Guid.Empty &&
            contribution.ApprovalRevision > 0 &&
            request.FreezeOperationRevision > 0 &&
            contribution.OperationRevision >
                request.FreezeOperationRevision &&
            contribution.TerminationEpoch != Guid.Empty &&
            contribution.Phase ==
                TenantTerminationContributionPhase.Export &&
            contribution.WorkItemId != Guid.Empty &&
            contribution.IdempotencyKey != Guid.Empty &&
            IsSha256(contribution.PolicyEvidenceSha256) &&
            IsSha256(request.FrozenRevisionSha256) &&
            request.WorkspaceFenceRevision > 0 &&
            request.FrozenAtUtc != default &&
            request.FrozenAtUtc <= nowUtc &&
            contribution.DeadlineUtc > nowUtc &&
            IsActor(contribution.ExecutingActorId);
    }

    private static bool IsActor(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <=
            TenantTerminationContract.ActorIdMaxLength;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: TenantTerminationContract.Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static TenantTerminationContributionResult RetryRequired(
        string code,
        DateTimeOffset recordedAtUtc) =>
        Result(
            TenantTerminationContributionStatus.RetryRequired,
            code,
            recordedAtUtc);

    private static TenantTerminationContributionResult Failed(
        string code,
        DateTimeOffset recordedAtUtc) =>
        Result(
            TenantTerminationContributionStatus.Failed,
            code,
            recordedAtUtc);

    private static TenantTerminationContributionResult Result(
        TenantTerminationContributionStatus status,
        string code,
        DateTimeOffset recordedAtUtc) =>
        new(
            status,
            code,
            AffectedCount: 0,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 1,
            HoldReviewAtUtc: null,
            SelectedProofRevision: null,
            ResultingProofRevision: null,
            ReservationsTenantTerminationMetadata.CatalogVersion,
            ReservationsTenantTerminationMetadata.CatalogSha256,
            recordedAtUtc);
}
