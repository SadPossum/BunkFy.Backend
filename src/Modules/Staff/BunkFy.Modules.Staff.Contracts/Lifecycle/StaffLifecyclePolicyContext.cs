namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.Naming;

public sealed record StaffLifecyclePolicyContext
{
    public StaffLifecyclePolicyContext(
        Guid transitionId,
        string scopeId,
        Guid staffMemberId,
        string? authSubjectId,
        StaffLifecycleTransition transition,
        StaffStatus previousStatus,
        StaffStatus targetStatus,
        DateOnly effectiveOn,
        long expectedVersion,
        long targetVersion,
        string actorId)
    {
        if (transitionId == Guid.Empty || staffMemberId == Guid.Empty)
        {
            throw new ArgumentException("Lifecycle transition identity is invalid.");
        }

        if (transition == StaffLifecycleTransition.Unknown || !Enum.IsDefined(transition) ||
            previousStatus == StaffStatus.Unknown || !Enum.IsDefined(previousStatus) ||
            targetStatus == StaffStatus.Unknown || !Enum.IsDefined(targetStatus) ||
            effectiveOn == default || expectedVersion < 1 || targetVersion != expectedVersion + 1)
        {
            throw new ArgumentException("Lifecycle transition state is invalid.");
        }

        string? normalizedSubject = string.IsNullOrWhiteSpace(authSubjectId)
            ? null
            : authSubjectId.Trim();
        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedSubject?.Length > StaffContractLimits.AuthSubjectIdMaxLength ||
            normalizedActor.Length is 0 or > StaffContractLimits.ActorIdMaxLength)
        {
            throw new ArgumentException("Lifecycle transition subject or actor is invalid.");
        }

        this.TransitionId = transitionId;
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.StaffMemberId = staffMemberId;
        this.AuthSubjectId = normalizedSubject;
        this.Transition = transition;
        this.PreviousStatus = previousStatus;
        this.TargetStatus = targetStatus;
        this.EffectiveOn = effectiveOn;
        this.ExpectedVersion = expectedVersion;
        this.TargetVersion = targetVersion;
        this.ActorId = normalizedActor;
    }

    public Guid TransitionId { get; }
    public string ScopeId { get; }
    public Guid StaffMemberId { get; }
    public string? AuthSubjectId { get; }
    public StaffLifecycleTransition Transition { get; }
    public StaffStatus PreviousStatus { get; }
    public StaffStatus TargetStatus { get; }
    public DateOnly EffectiveOn { get; }
    public long ExpectedVersion { get; }
    public long TargetVersion { get; }
    public string ActorId { get; }
}
