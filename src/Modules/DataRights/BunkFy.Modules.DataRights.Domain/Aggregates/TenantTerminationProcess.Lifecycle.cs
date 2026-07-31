namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class TenantTerminationProcess
{
    public Result BeginPhase(
        TenantTerminationProcessPhase phase,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        string normalizedActor = NormalizeActor(actorId);
        if (phase == this.Phase &&
            this.Status == TenantTerminationProcessStatus.Running &&
            string.Equals(
                normalizedActor,
                this.LastChangedBy,
                StringComparison.Ordinal) &&
            nowUtc == this.LastChangedAtUtc)
        {
            return Result.Success();
        }

        Result ready = this.ValidateChange(expectedVersion, actorId, nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (phase != this.Phase ||
            this.Status != TenantTerminationProcessStatus.Pending ||
            phase is TenantTerminationProcessPhase.Unknown or
                TenantTerminationProcessPhase.Completed)
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationTransitionInvalid);
        }

        if (phase == TenantTerminationProcessPhase.Destroy &&
            string.Equals(normalizedActor, this.ApprovedBy, StringComparison.Ordinal))
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationExecutorInvalid);
        }

        this.Status = TenantTerminationProcessStatus.Running;
        this.OperationRevision++;
        this.ClearOutcome();
        this.CompleteChange(normalizedActor, nowUtc);
        return Result.Success();
    }

    public Result RequestCancellation(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        string normalizedActor = NormalizeActor(actorId);
        if (this.Phase == TenantTerminationProcessPhase.Restore &&
            this.Status == TenantTerminationProcessStatus.Pending &&
            string.Equals(
                normalizedActor,
                this.LastChangedBy,
                StringComparison.Ordinal) &&
            nowUtc == this.LastChangedAtUtc)
        {
            return Result.Success();
        }

        Result ready = this.ValidateChange(expectedVersion, actorId, nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        bool cancellable =
            (this.Phase == TenantTerminationProcessPhase.Export &&
                this.Status is
                    TenantTerminationProcessStatus.Pending or
                    TenantTerminationProcessStatus.Blocked or
                    TenantTerminationProcessStatus.Failed) ||
            (this.Phase == TenantTerminationProcessPhase.Destroy &&
                this.Status == TenantTerminationProcessStatus.Pending);
        if (!cancellable)
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationTransitionInvalid);
        }

        this.Phase = TenantTerminationProcessPhase.Restore;
        this.Status = TenantTerminationProcessStatus.Pending;
        this.ClearOutcome();
        this.CompleteChange(normalizedActor, nowUtc);
        return Result.Success();
    }

    public Result CompleteCancellation(
        long operationRevision,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        string normalizedActor = NormalizeActor(actorId);
        if (this.Phase == TenantTerminationProcessPhase.Restore &&
            this.Status == TenantTerminationProcessStatus.Cancelled &&
            operationRevision == this.OperationRevision &&
            string.Equals(
                normalizedActor,
                this.LastChangedBy,
                StringComparison.Ordinal) &&
            nowUtc == this.LastChangedAtUtc)
        {
            return Result.Success();
        }

        Result ready = this.ValidateChange(expectedVersion, actorId, nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (this.Phase != TenantTerminationProcessPhase.Restore ||
            this.Status != TenantTerminationProcessStatus.Running ||
            operationRevision != this.OperationRevision)
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationTransitionInvalid);
        }

        this.Status = TenantTerminationProcessStatus.Cancelled;
        this.ClearOutcome();
        this.CompleteChange(normalizedActor, nowUtc);
        return Result.Success();
    }

    public Result CompletePhase(
        TenantTerminationProcessPhase phase,
        long operationRevision,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        TenantTerminationProcessPhase nextPhase = this.GetNextPhase(phase);
        string normalizedActor = NormalizeActor(actorId);
        bool isReplayTarget = this.Status is
            TenantTerminationProcessStatus.Pending or
            TenantTerminationProcessStatus.Completed;
        if (operationRevision == this.OperationRevision &&
            nextPhase == this.Phase &&
            isReplayTarget &&
            string.Equals(
                normalizedActor,
                this.LastChangedBy,
                StringComparison.Ordinal) &&
            nowUtc == this.LastChangedAtUtc)
        {
            return Result.Success();
        }

        Result ready = this.ValidateChange(expectedVersion, actorId, nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (phase != this.Phase ||
            operationRevision != this.OperationRevision ||
            this.Status != TenantTerminationProcessStatus.Running ||
            phase == TenantTerminationProcessPhase.Restore)
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationTransitionInvalid);
        }

        if (nextPhase == TenantTerminationProcessPhase.Unknown)
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationTransitionInvalid);
        }

        this.ClearOutcome();
        this.Phase = nextPhase;
        this.Status = this.Phase == TenantTerminationProcessPhase.Completed
            ? TenantTerminationProcessStatus.Completed
            : TenantTerminationProcessStatus.Pending;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    public Result RecordBlocked(
        TenantTerminationProcessPhase phase,
        long operationRevision,
        string blockerCode,
        DateTimeOffset holdReviewAtUtc,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        string normalizedCode = blockerCode?.Trim() ?? string.Empty;
        string normalizedActor = NormalizeActor(actorId);
        if (phase == this.Phase &&
            operationRevision == this.OperationRevision &&
            this.Status == TenantTerminationProcessStatus.Blocked &&
            string.Equals(
                normalizedCode,
                this.OutcomeCode,
                StringComparison.Ordinal) &&
            holdReviewAtUtc == this.HoldReviewAtUtc &&
            string.Equals(
                normalizedActor,
                this.LastChangedBy,
                StringComparison.Ordinal) &&
            nowUtc == this.LastChangedAtUtc)
        {
            return Result.Success();
        }

        Result ready = this.ValidateRunningOutcome(
            phase,
            operationRevision,
            normalizedCode,
            expectedVersion,
            actorId,
            nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (holdReviewAtUtc == default || holdReviewAtUtc < nowUtc)
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationTransitionInvalid);
        }

        this.Status = TenantTerminationProcessStatus.Blocked;
        this.OutcomeCode = normalizedCode;
        this.HoldReviewAtUtc = holdReviewAtUtc;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    public Result RecordFailed(
        TenantTerminationProcessPhase phase,
        long operationRevision,
        string failureCode,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        string normalizedCode = failureCode?.Trim() ?? string.Empty;
        string normalizedActor = NormalizeActor(actorId);
        if (phase == this.Phase &&
            operationRevision == this.OperationRevision &&
            this.Status == TenantTerminationProcessStatus.Failed &&
            string.Equals(
                normalizedCode,
                this.OutcomeCode,
                StringComparison.Ordinal) &&
            string.Equals(
                normalizedActor,
                this.LastChangedBy,
                StringComparison.Ordinal) &&
            nowUtc == this.LastChangedAtUtc)
        {
            return Result.Success();
        }

        Result ready = this.ValidateRunningOutcome(
            phase,
            operationRevision,
            normalizedCode,
            expectedVersion,
            actorId,
            nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        this.Status = TenantTerminationProcessStatus.Failed;
        this.OutcomeCode = normalizedCode;
        this.HoldReviewAtUtc = null;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    public Result Requeue(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        Result ready = this.ValidateChange(expectedVersion, actorId, nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (this.Status is not TenantTerminationProcessStatus.Blocked and
            not TenantTerminationProcessStatus.Failed)
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationTransitionInvalid);
        }

        this.Status = TenantTerminationProcessStatus.Pending;
        this.ClearOutcome();
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    private Result ValidateRunningOutcome(
        TenantTerminationProcessPhase phase,
        long operationRevision,
        string outcomeCode,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        Result ready = this.ValidateChange(expectedVersion, actorId, nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        string normalizedCode = outcomeCode?.Trim() ?? string.Empty;
        return phase == this.Phase &&
            operationRevision == this.OperationRevision &&
            this.Status == TenantTerminationProcessStatus.Running &&
            IsStableCode(normalizedCode, OutcomeCodeMaxLength)
                ? Result.Success()
                : Result.Failure(
                    DataRightsDomainErrors.TenantTerminationTransitionInvalid);
    }

    private TenantTerminationProcessPhase GetNextPhase(
        TenantTerminationProcessPhase phase) =>
        phase switch
        {
            TenantTerminationProcessPhase.Freeze when this.ExportRequested =>
                TenantTerminationProcessPhase.Export,
            TenantTerminationProcessPhase.Freeze =>
                TenantTerminationProcessPhase.Destroy,
            TenantTerminationProcessPhase.Export =>
                TenantTerminationProcessPhase.Destroy,
            TenantTerminationProcessPhase.Destroy =>
                TenantTerminationProcessPhase.Verify,
            TenantTerminationProcessPhase.Verify =>
                TenantTerminationProcessPhase.Completed,
            _ => TenantTerminationProcessPhase.Unknown
        };

    private Result ValidateChange(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(DataRightsDomainErrors.VersionConflict);
        }

        return NormalizeActor(actorId).Length == 0 ||
            nowUtc == default ||
            nowUtc < this.LastChangedAtUtc
                ? Result.Failure(
                    DataRightsDomainErrors.TenantTerminationTransitionInvalid)
                : Result.Success();
    }

    private void CompleteChange(string actorId, DateTimeOffset nowUtc)
    {
        this.LastChangedBy = actorId.Trim();
        this.LastChangedAtUtc = nowUtc;
        this.Version++;
    }

    private void ClearOutcome()
    {
        this.OutcomeCode = null;
        this.HoldReviewAtUtc = null;
    }
}
