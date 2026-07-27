namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class DataRightsExportArtifact
{
    public Result MarkExpired(DateTimeOffset nowUtc)
    {
        if (nowUtc == default ||
            nowUtc < this.ExpiresAtUtc)
        {
            return Result.Failure(DataRightsDomainErrors.TimestampInvalid);
        }

        if (this.State is DataRightsExportArtifactState.Expired or
            DataRightsExportArtifactState.Deleting or
            DataRightsExportArtifactState.Deleted)
        {
            return Result.Success();
        }

        if (this.State is not (
            DataRightsExportArtifactState.Requested or
            DataRightsExportArtifactState.Generating or
            DataRightsExportArtifactState.Available or
            DataRightsExportArtifactState.Failed))
        {
            return Result.Failure(
                DataRightsDomainErrors.ExportArtifactTransitionInvalid);
        }

        this.State = DataRightsExportArtifactState.Expired;
        this.FailureCode = null;
        this.Version++;
        return Result.Success();
    }

    public Result BeginDeletion(
        Guid runId,
        DateTimeOffset nowUtc)
    {
        if (runId == Guid.Empty ||
            nowUtc == default ||
            nowUtc < this.ExpiresAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors.ExportArtifactDeletionInvalid);
        }

        if (this.State == DataRightsExportArtifactState.Deleted)
        {
            return Result.Success();
        }

        if (this.State == DataRightsExportArtifactState.Deleting)
        {
            if (this.DeletionRunId == runId)
            {
                return Result.Success();
            }

            this.DeletionRunId = runId;
            this.Version++;
            return Result.Success();
        }

        if (this.State != DataRightsExportArtifactState.Expired)
        {
            return Result.Failure(
                DataRightsDomainErrors.ExportArtifactTransitionInvalid);
        }

        this.State = DataRightsExportArtifactState.Deleting;
        this.DeletionRunId = runId;
        this.DeletionStartedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    public Result MarkDeleted(
        Guid runId,
        DateTimeOffset nowUtc)
    {
        if (this.State == DataRightsExportArtifactState.Deleted)
        {
            return Result.Success();
        }

        if (runId == Guid.Empty ||
            this.State != DataRightsExportArtifactState.Deleting ||
            this.DeletionRunId != runId ||
            this.DeletionStartedAtUtc is not DateTimeOffset startedAtUtc ||
            nowUtc == default ||
            nowUtc < startedAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors.ExportArtifactDeletionInvalid);
        }

        this.State = DataRightsExportArtifactState.Deleted;
        this.DeletedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }
}
