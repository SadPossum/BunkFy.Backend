namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class TenantTerminationExportFragment
{
    public Result MarkExpired(DateTimeOffset nowUtc)
    {
        if (nowUtc == default || nowUtc < this.ExpiresAtUtc)
        {
            return Result.Failure(DataRightsDomainErrors.TimestampInvalid);
        }

        if (this.State is TenantTerminationExportFragmentState.Expired or
            TenantTerminationExportFragmentState.Deleting or
            TenantTerminationExportFragmentState.Deleted)
        {
            return Result.Success();
        }

        if (this.State is not (
            TenantTerminationExportFragmentState.Requested or
            TenantTerminationExportFragmentState.Generating or
            TenantTerminationExportFragmentState.Available or
            TenantTerminationExportFragmentState.Failed))
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportFragmentTransitionInvalid);
        }

        this.State = TenantTerminationExportFragmentState.Expired;
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
                    .TenantTerminationExportFragmentDeletionInvalid);
        }

        if (this.State == TenantTerminationExportFragmentState.Deleted)
        {
            return Result.Success();
        }

        if (this.State == TenantTerminationExportFragmentState.Deleting)
        {
            return this.DeletionRunId == runId
                ? Result.Success()
                : Result.Failure(
                    DataRightsDomainErrors
                        .TenantTerminationExportFragmentDeletionInvalid);
        }

        if (this.State != TenantTerminationExportFragmentState.Expired)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportFragmentTransitionInvalid);
        }

        this.State = TenantTerminationExportFragmentState.Deleting;
        this.DeletionRunId = runId;
        this.DeletionStartedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    public Result MarkDeleted(Guid runId, DateTimeOffset nowUtc)
    {
        if (this.State == TenantTerminationExportFragmentState.Deleted)
        {
            return Result.Success();
        }

        if (runId == Guid.Empty ||
            this.State != TenantTerminationExportFragmentState.Deleting ||
            this.DeletionRunId != runId ||
            this.DeletionStartedAtUtc is not DateTimeOffset startedAtUtc ||
            nowUtc == default ||
            nowUtc < startedAtUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportFragmentDeletionInvalid);
        }

        this.State = TenantTerminationExportFragmentState.Deleted;
        this.DeletedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }
}
