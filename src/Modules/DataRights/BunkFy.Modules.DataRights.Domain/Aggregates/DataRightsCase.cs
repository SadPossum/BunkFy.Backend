namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class DataRightsCase : ScopedAggregateRoot<Guid>
{
    public const int ActorIdMaxLength = 200;

    private DataRightsCase() { }

    private DataRightsCase(Guid id, string scopeId) : base(id, scopeId) { }

    public Guid? PropertyId { get; private set; }
    public DataRightsCaseKind Kind { get; private set; }
    public DataRightsCaseOperation RequestedOperations { get; private set; }
    public DataRightsRequesterRelation RequesterRelationship { get; private set; }
    public DataRightsVerificationState VerificationStatus { get; private set; }
    public DataRightsRoutingState RoutingStatus { get; private set; }
    public DataRightsCaseState Status { get; private set; } = DataRightsCaseState.Draft;
    public DateTimeOffset? DueAtUtc { get; private set; }
    public long Version { get; private set; } = 1;
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string LastChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public static Result<DataRightsCase> Create(
        Guid id,
        string tenantId,
        DataRightsCaseRequest request,
        string actorId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (id == Guid.Empty)
        {
            return Result.Failure<DataRightsCase>(DataRightsDomainErrors.CaseIdRequired);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<DataRightsCase>(DataRightsDomainErrors.TenantInvalid);
        }

        Result<string> actor = NormalizeActor(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure<DataRightsCase>(actor.Error);
        }

        if (nowUtc == default)
        {
            return Result.Failure<DataRightsCase>(DataRightsDomainErrors.TimestampInvalid);
        }

        bool requesterNeedsVerification = request.RequesterRelationship is
            DataRightsRequesterRelation.DataSubject or
            DataRightsRequesterRelation.AuthorizedRepresentative;
        DataRightsCase dataRightsCase = new(id, scopeId)
        {
            PropertyId = request.PropertyId,
            Kind = request.Kind,
            RequestedOperations = request.RequestedOperations,
            RequesterRelationship = request.RequesterRelationship,
            VerificationStatus = requesterNeedsVerification
                ? DataRightsVerificationState.Pending
                : DataRightsVerificationState.NotRequired,
            RoutingStatus = requesterNeedsVerification
                ? DataRightsRoutingState.Pending
                : DataRightsRoutingState.NotRequired,
            CreatedBy = actor.Value,
            CreatedAtUtc = nowUtc,
            LastChangedBy = actor.Value,
            LastChangedAtUtc = nowUtc
        };
        return Result.Success(dataRightsCase);
    }

    public Result RecordRequesterVerification(
        bool verified,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        Result ready = this.EnsureTransition(
            expectedVersion,
            actorId,
            nowUtc,
            DataRightsCaseState.Draft);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (this.VerificationStatus != DataRightsVerificationState.Pending)
        {
            return Result.Failure(DataRightsDomainErrors.TransitionInvalid);
        }

        this.VerificationStatus = verified
            ? DataRightsVerificationState.Verified
            : DataRightsVerificationState.Failed;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    public Result RecordControllerRouting(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        Result ready = this.EnsureTransition(
            expectedVersion,
            actorId,
            nowUtc,
            DataRightsCaseState.Draft);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (this.RoutingStatus != DataRightsRoutingState.Pending)
        {
            return Result.Failure(DataRightsDomainErrors.TransitionInvalid);
        }

        this.RoutingStatus = DataRightsRoutingState.Routed;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    public Result BeginDiscovery(long expectedVersion, string actorId, DateTimeOffset nowUtc)
    {
        Result ready = this.EnsureTransition(
            expectedVersion,
            actorId,
            nowUtc,
            DataRightsCaseState.Draft);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (this.VerificationStatus is not DataRightsVerificationState.Verified
            and not DataRightsVerificationState.NotRequired)
        {
            return Result.Failure(DataRightsDomainErrors.VerificationRequired);
        }

        if (this.RoutingStatus is not DataRightsRoutingState.Routed
            and not DataRightsRoutingState.NotRequired)
        {
            return Result.Failure(DataRightsDomainErrors.ControllerRoutingRequired);
        }

        this.Status = DataRightsCaseState.Discovery;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    public Result RequireReview(long expectedVersion, string actorId, DateTimeOffset nowUtc)
    {
        Result ready = this.EnsureTransition(
            expectedVersion,
            actorId,
            nowUtc,
            DataRightsCaseState.Discovery);
        if (ready.IsFailure)
        {
            return ready;
        }

        this.Status = DataRightsCaseState.ReviewRequired;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    public Result Cancel(long expectedVersion, string actorId, DateTimeOffset nowUtc)
    {
        Result ready = this.EnsureVersionActorAndTime(expectedVersion, actorId, nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (this.Status is not DataRightsCaseState.Draft
            and not DataRightsCaseState.Discovery
            and not DataRightsCaseState.ReviewRequired
            and not DataRightsCaseState.DecisionPending
            and not DataRightsCaseState.Blocked)
        {
            return Result.Failure(DataRightsDomainErrors.TransitionInvalid);
        }

        this.Status = DataRightsCaseState.Canceled;
        this.CompleteChange(actorId, nowUtc);
        return Result.Success();
    }

    private Result EnsureTransition(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc,
        DataRightsCaseState expectedState)
    {
        Result ready = this.EnsureVersionActorAndTime(expectedVersion, actorId, nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        return this.Status == expectedState
            ? Result.Success()
            : Result.Failure(DataRightsDomainErrors.TransitionInvalid);
    }

    private Result EnsureVersionActorAndTime(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(DataRightsDomainErrors.VersionConflict);
        }

        Result<string> actor = NormalizeActor(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure(actor.Error);
        }

        return nowUtc == default || nowUtc < this.CreatedAtUtc
            ? Result.Failure(DataRightsDomainErrors.TimestampInvalid)
            : Result.Success();
    }

    private void CompleteChange(string actorId, DateTimeOffset nowUtc)
    {
        this.LastChangedBy = actorId.Trim();
        this.LastChangedAtUtc = nowUtc;
        this.Version++;
    }

    private static Result<string> NormalizeActor(string actorId)
    {
        string normalized = actorId?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= ActorIdMaxLength
            ? Result.Success(normalized)
            : Result.Failure<string>(DataRightsDomainErrors.ActorInvalid);
    }
}
