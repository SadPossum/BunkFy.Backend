namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class TenantTerminationExportArtifact
{
    public Result MarkExpired(DateTimeOffset nowUtc)
    {
        if (nowUtc == default || nowUtc < this.ExpiresAtUtc)
        {
            return Result.Failure(DataRightsDomainErrors.TimestampInvalid);
        }

        if (this.State is TenantTerminationExportArtifactState.Expired or
            TenantTerminationExportArtifactState.Deleting or
            TenantTerminationExportArtifactState.Deleted)
        {
            return Result.Success();
        }

        if (this.State is not (
            TenantTerminationExportArtifactState.Requested or
            TenantTerminationExportArtifactState.Generating or
            TenantTerminationExportArtifactState.Available or
            TenantTerminationExportArtifactState.Failed))
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportArtifactTransitionInvalid);
        }

        this.State = TenantTerminationExportArtifactState.Expired;
        this.FailureCode = null;
        this.Version++;
        return Result.Success();
    }

    public Result BeginDeletion(Guid runId, DateTimeOffset nowUtc)
    {
        if (runId == Guid.Empty ||
            nowUtc == default ||
            nowUtc < this.ExpiresAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportArtifactDeletionInvalid);
        }

        if (this.State == TenantTerminationExportArtifactState.Deleted)
        {
            return Result.Success();
        }

        if (this.State == TenantTerminationExportArtifactState.Deleting)
        {
            return this.DeletionRunId == runId
                ? Result.Success()
                : Result.Failure(
                    DataRightsDomainErrors
                        .TenantTerminationExportArtifactDeletionInvalid);
        }

        if (this.State != TenantTerminationExportArtifactState.Expired)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportArtifactTransitionInvalid);
        }

        this.State = TenantTerminationExportArtifactState.Deleting;
        this.DeletionRunId = runId;
        this.DeletionStartedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    public Result MarkDeleted(Guid runId, DateTimeOffset nowUtc)
    {
        if (this.State == TenantTerminationExportArtifactState.Deleted)
        {
            return Result.Success();
        }

        if (runId == Guid.Empty ||
            this.State != TenantTerminationExportArtifactState.Deleting ||
            this.DeletionRunId != runId ||
            this.DeletionStartedAtUtc is not DateTimeOffset startedAtUtc ||
            nowUtc == default ||
            nowUtc < startedAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportArtifactDeletionInvalid);
        }

        this.State = TenantTerminationExportArtifactState.Deleted;
        this.DeletedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }
}
